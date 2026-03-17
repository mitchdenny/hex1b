using System.Text;
using Hex1b;

namespace AudioDemo;

/// <summary>
/// Presentation adapter that wraps <see cref="ConsolePresentationAdapter"/> and intercepts
/// audio escape sequences (ESC_A) from the output stream. Audio commands are dispatched
/// to the <see cref="AudioMixer"/> for playback, while all other output is forwarded
/// to the inner adapter for display.
/// </summary>
/// <remarks>
/// Also monitors input for mouse position reports to update the listener position.
/// </remarks>
public sealed class AudioPresentationAdapter : IHex1bTerminalPresentationAdapter
{
    private readonly IHex1bTerminalPresentationAdapter _inner;
    private readonly AudioMixer _mixer;

    public AudioPresentationAdapter(IHex1bTerminalPresentationAdapter inner, AudioMixer mixer)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _mixer = mixer ?? throw new ArgumentNullException(nameof(mixer));

        _inner.Resized += (w, h) => Resized?.Invoke(w, h);
        _inner.Disconnected += () => Disconnected?.Invoke();
    }

    public int Width => _inner.Width;
    public int Height => _inner.Height;
    public TerminalCapabilities Capabilities => _inner.Capabilities;
    public event Action<int, int>? Resized;
    public event Action? Disconnected;

    public async ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var text = Encoding.UTF8.GetString(data.Span);

        // Scan for ESC_A sequences
        var cleaned = ExtractAndProcessAudioSequences(text);

        if (cleaned.Length > 0)
        {
            var bytes = Encoding.UTF8.GetBytes(cleaned);
            await _inner.WriteOutputAsync(bytes, ct);
        }
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        var input = await _inner.ReadInputAsync(ct);

        // Parse mouse position from SGR mouse reports: ESC[<btn;col;row M/m
        if (input.Length > 0)
        {
            var text = Encoding.UTF8.GetString(input.Span);
            TryExtractMousePosition(text);
        }

        return input;
    }

    public ValueTask FlushAsync(CancellationToken ct = default) => _inner.FlushAsync(ct);
    public ValueTask EnterRawModeAsync(CancellationToken ct = default) => _inner.EnterRawModeAsync(ct);
    public ValueTask ExitRawModeAsync(CancellationToken ct = default) => _inner.ExitRawModeAsync(ct);
    public (int Row, int Column) GetCursorPosition() => _inner.GetCursorPosition();
    public ValueTask DisposeAsync() => _inner.DisposeAsync();

    /// <summary>
    /// Scans the output text for ESC_A sequences, processes them for audio,
    /// and returns the cleaned text with audio sequences removed.
    /// </summary>
    private string ExtractAndProcessAudioSequences(string text)
    {
        var sb = new StringBuilder(text.Length);
        var i = 0;

        while (i < text.Length)
        {
            // Check for APC start: ESC _ A
            if (i + 2 < text.Length && text[i] == '\x1b' && text[i + 1] == '_' && text[i + 2] == 'A')
            {
                // Find the ST terminator: ESC \ or 0x9C
                var start = i;
                var dataStart = i + 3; // Skip ESC _ A
                var endIndex = -1;

                for (var j = dataStart; j < text.Length; j++)
                {
                    if (j + 1 < text.Length && text[j] == '\x1b' && text[j + 1] == '\\')
                    {
                        endIndex = j + 2; // Past ESC \
                        break;
                    }
                    if (text[j] == '\x9c')
                    {
                        endIndex = j + 1;
                        break;
                    }
                }

                if (endIndex > 0)
                {
                    // Extract and process the audio sequence
                    var content = text.Substring(dataStart, endIndex - dataStart - (text[endIndex - 1] == '\\' ? 2 : 1));
                    ProcessAudioSequence(content);
                    i = endIndex;
                    continue;
                }
            }

            sb.Append(text[i]);
            i++;
        }

        return sb.ToString();
    }

    private void ProcessAudioSequence(string content)
    {
        // Split on ';' - control data before, payload after
        var semicolonIndex = content.IndexOf(';');
        var controlData = semicolonIndex >= 0 ? content[..semicolonIndex] : content;
        var payload = semicolonIndex >= 0 ? content[(semicolonIndex + 1)..] : "";

        var command = AudioCommand.Parse(controlData);

        switch (command.Action)
        {
            case AudioAction.Transmit:
                HandleTransmit(command, payload);
                break;
            case AudioAction.Place:
                _mixer.PlaceProducer(command.ClipId, command.PlacementId,
                    command.Column, command.Row, command.Volume, command.Loop == 1);
                _mixer.UpdateAllVolumes();
                break;
            case AudioAction.Stop:
                _mixer.StopProducer(command.ClipId, command.PlacementId);
                break;
            case AudioAction.Delete:
                if (command.DeleteTarget == AudioDeleteTarget.All)
                    _mixer.DeleteAll();
                else
                    _mixer.DeleteClip(command.ClipId);
                break;
        }
    }

    // Chunked transfer state for multi-chunk transmits
    private AudioCommand? _chunkedCommand;
    private readonly List<byte> _chunkedData = new();

    private void HandleTransmit(AudioCommand command, string base64Payload)
    {
        byte[] decodedData;
        try
        {
            decodedData = Convert.FromBase64String(base64Payload);
        }
        catch (FormatException)
        {
            return;
        }

        if (command.MoreData == 1 || _chunkedCommand is not null)
        {
            if (_chunkedCommand is null)
                _chunkedCommand = command;

            _chunkedData.AddRange(decodedData);

            if (command.MoreData == 0)
            {
                var completeData = _chunkedData.ToArray();
                _mixer.StoreClip(_chunkedCommand.ClipId, completeData);
                _chunkedCommand = null;
                _chunkedData.Clear();
            }
            return;
        }

        _mixer.StoreClip(command.ClipId, decodedData);
    }

    /// <summary>
    /// Attempt to extract mouse position from SGR mouse reports.
    /// Format: ESC [ &lt; btn ; col ; row M  (or m for release)
    /// </summary>
    private void TryExtractMousePosition(string text)
    {
        var i = 0;
        while (i < text.Length)
        {
            // Look for CSI < (SGR mouse protocol)
            if (i + 2 < text.Length && text[i] == '\x1b' && text[i + 1] == '[' && text[i + 2] == '<')
            {
                var start = i + 3;
                var end = start;
                while (end < text.Length && text[end] != 'M' && text[end] != 'm')
                    end++;

                if (end < text.Length)
                {
                    var parts = text[start..end].Split(';');
                    if (parts.Length >= 3 &&
                        int.TryParse(parts[1], out var col) &&
                        int.TryParse(parts[2], out var row))
                    {
                        // SGR mouse is 1-based, convert to 0-based
                        _mixer.ListenerCol = col - 1;
                        _mixer.ListenerRow = row - 1;
                        _mixer.UpdateAllVolumes();
                    }
                    i = end + 1;
                    continue;
                }
            }
            i++;
        }
    }
}
