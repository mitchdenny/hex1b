namespace Hex1b;

/// <summary>
/// Records a single OSC 133 shell-integration marker (prompt, command-line, executing, or
/// finished) anchored to a specific row in the terminal's text history.
/// </summary>
/// <remarks>
/// <para>
/// This is history — a sequence of past markers — unlike <see cref="TerminalShellIntegration"/>,
/// which reflects only the current phase. Marks are captured in a bounded, most-recent-first
/// list; older marks are evicted as new ones arrive.
/// </para>
/// <para>
/// A mark's row anchor (<see cref="TextGeneration"/>, <see cref="TextRowId"/>) becomes stale
/// once the terminal's text-row identity space is invalidated (for example by <c>RIS</c> or a
/// resize-driven reflow) — a mark whose <see cref="TextGeneration"/> no longer matches the
/// terminal's current generation cannot be resolved back to a row.
/// </para>
/// </remarks>
public sealed record TerminalCommandMark
{
    private IReadOnlyDictionary<string, string>? _parameters;

    internal TerminalCommandMark(TerminalShellIntegrationPhase phase, int? exitCode, string? rawParameters,
        long textGeneration, long textRowId)
    {
        Phase = phase;
        ExitCode = exitCode;
        RawParameters = rawParameters;
        TextGeneration = textGeneration;
        TextRowId = textRowId;
    }

    /// <summary>Gets the shell-integration phase this mark reports.</summary>
    public TerminalShellIntegrationPhase Phase { get; }

    /// <summary>Gets the reported exit code, or null when this mark is not a finished marker
    /// with a reported code.</summary>
    public int? ExitCode { get; }

    /// <summary>
    /// Gets the raw, verbatim <c>key=value[;key=value...]</c> string trailing the marker, or
    /// null when none was present. This is captured as-is; it is the caller's responsibility
    /// to sanitize it for whatever context it is rendered or logged in.
    /// </summary>
    public string? RawParameters { get; }

    /// <summary>
    /// Gets the text-history generation this mark was captured in. A mark can only be resolved
    /// to a row while the terminal's current text generation still matches this value.
    /// </summary>
    public long TextGeneration { get; }

    /// <summary>Gets the stable text-row identifier this mark is anchored to.</summary>
    public long TextRowId { get; }

    /// <summary>
    /// Gets <see cref="RawParameters"/> split into key/value pairs, parsed and cached on first
    /// access. Returns an empty dictionary when <see cref="RawParameters"/> is null.
    /// </summary>
    /// <remarks>
    /// Parameter keys are whatever the shell or prompt integration emitted; this is not a
    /// fixed vocabulary and unrecognized keys are preserved as-is. Values are not decoded
    /// (e.g. percent-decoding is left to the consumer for keys that use it, such as
    /// <c>cmdline_url</c>).
    /// </remarks>
    public IReadOnlyDictionary<string, string> Parameters => _parameters ??= ParseParameters(RawParameters);

    /// <summary>Gets whether a <c>cmdline_url</c> parameter was present on this marker.</summary>
    /// <remarks>
    /// This is a Contour-originated, non-universal extension to OSC 133;C. Most terminals and
    /// shell integrations do not emit it.
    /// </remarks>
    public bool HasCmdlineUrl => Parameters.ContainsKey("cmdline_url");

    /// <summary>
    /// Gets the raw (still percent-encoded) <c>cmdline_url</c> value, or null when not present.
    /// </summary>
    public string? CmdlineUrl => Parameters.TryGetValue("cmdline_url", out var value) ? value : null;

    private static IReadOnlyDictionary<string, string> ParseParameters(string? rawParameters)
    {
        if (string.IsNullOrEmpty(rawParameters))
            return EmptyParameters;

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var segment in rawParameters.Split(';'))
        {
            var separator = segment.IndexOf('=');
            if (separator < 0)
                continue;
            result[segment[..separator]] = segment[(separator + 1)..];
        }
        return result;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyParameters =
        new Dictionary<string, string>(0, StringComparer.Ordinal);
}
