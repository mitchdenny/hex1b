using Hex1b.Tokens;

namespace Hex1b.Kgp;

/// <summary>
/// Presentation filter that diverts KGP APC sequences from stdout to
/// a named pipe side-channel. This is needed on Windows where ConPTY
/// strips APC sequences.
/// </summary>
/// <remarks>
/// Automatically installed when HEX1B_KGP_PIPE env var is detected.
/// Intercepts UnrecognizedSequenceToken containing KGP APC data and
/// writes them to the pipe instead of letting them reach stdout.
/// </remarks>
internal sealed class KgpPipePresentationFilter : IHex1bTerminalPresentationFilter, IDisposable
{
    private readonly KgpPipeClient _client;

    public KgpPipePresentationFilter(KgpPipeClient client)
    {
        _client = client;
    }

    public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(IReadOnlyList<AppliedToken> appliedTokens, TimeSpan elapsed, CancellationToken ct = default)
    {
        if (!_client.IsConnected)
        {
            return ValueTask.FromResult<IReadOnlyList<AnsiToken>>(
                appliedTokens.Select(t => t.Token).ToList());
        }

        var passThrough = new List<AnsiToken>();

        for (int i = 0; i < appliedTokens.Count; i++)
        {
            var applied = appliedTokens[i];

            // Intercept CursorPositionToken that precedes KGP APC — divert to pipe
            // so it doesn't leak to ConPTY and displace text rendering
            if (applied.Token is CursorPositionToken cup && i + 1 < appliedTokens.Count)
            {
                var next = appliedTokens[i + 1].Token;
                if ((next is UnrecognizedSequenceToken nextUnrec && IsKgpApc(nextUnrec.Sequence)) ||
                    next is KgpToken)
                {
                    _client.Write($"\x1b[{cup.Row};{cup.Column}H");
                    continue;
                }
            }

            if (applied.Token is UnrecognizedSequenceToken unrec && IsKgpApc(unrec.Sequence))
            {
                // If no preceding CursorPositionToken was diverted, send cursor now
                if (i == 0 || appliedTokens[i - 1].Token is not CursorPositionToken)
                {
                    _client.Write($"\x1b[{applied.CursorYBefore + 1};{applied.CursorXBefore + 1}H");
                }
                _client.Write(unrec.Sequence);
            }
            else if (applied.Token is KgpToken kgp)
            {
                if (i == 0 || appliedTokens[i - 1].Token is not CursorPositionToken)
                {
                    _client.Write($"\x1b[{applied.CursorYBefore + 1};{applied.CursorXBefore + 1}H");
                }
                var serialized = AnsiTokenSerializer.Serialize([kgp]);
                _client.Write(serialized);
            }
            else
            {
                passThrough.Add(applied.Token);
            }
        }

        return ValueTask.FromResult<IReadOnlyList<AnsiToken>>(passThrough);
    }

    public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Checks if a sequence is a KGP APC (starts with ESC _ G or 0x9F G).
    /// </summary>
    private static bool IsKgpApc(string sequence)
    {
        if (sequence.Length < 4) return false;

        // ESC _ G ...
        if (sequence[0] == '\x1b' && sequence[1] == '_' && sequence[2] == 'G')
            return true;

        // 8-bit APC: 0x9F G ...
        if (sequence[0] == '\x9f' && sequence[1] == 'G')
            return true;

        return false;
    }

    /// <summary>
    /// Attempts to create and install a KGP pipe filter from the environment.
    /// Returns null if HEX1B_KGP_PIPE is not set or connection fails.
    /// </summary>
    public static KgpPipePresentationFilter? TryCreate()
    {
        var client = KgpPipeClient.TryCreateFromEnvironment();
        if (client == null) return null;
        return new KgpPipePresentationFilter(client);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
