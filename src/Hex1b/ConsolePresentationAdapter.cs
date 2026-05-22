using System.Runtime.InteropServices;
using System.Text;
using Hex1b.Input;
using Hex1b.Reflow;

namespace Hex1b;

/// <summary>
/// Console presentation adapter using platform-specific raw mode for proper input handling.
/// </summary>
/// <remarks>
/// This adapter uses raw terminal mode (termios on Unix, SetConsoleMode on Windows)
/// to properly capture mouse events, escape sequences, and control characters.
/// </remarks>
public sealed class ConsolePresentationAdapter : IHex1bTerminalPresentationAdapter, ITerminalReflowProvider
{
    private const uint KgpProbeImageId = 2147483647u;
    private static readonly byte[] KgpProbeQuery = Encoding.ASCII.GetBytes(
        $"\x1b_Gi={KgpProbeImageId},s=1,v=1,a=q,t=d,f=24;AAAA\x1b\\");
    // OSC 11 with "?" payload asks the terminal to report its current default
    // background colour. The response comes back as ESC ] 11 ; rgb:RRRR/GGGG/BBBB
    // ST (where ST is either ESC \ or BEL). Most modern terminals support this
    // (xterm, iTerm2, kitty, WezTerm, Alacritty, Windows Terminal, VS Code).
    private static readonly byte[] BackgroundProbeQuery = Encoding.ASCII.GetBytes("\x1b]11;?\x1b\\");
    private static readonly TimeSpan DefaultKgpProbeTimeout = TimeSpan.FromMilliseconds(150);

    private readonly IConsoleDriver _driver;
    private readonly bool _enableMouse;
    private readonly bool _preserveOPost;
    private readonly TimeSpan _kgpProbeTimeout;
    private readonly CancellationTokenSource _disposeCts = new();
    private ITerminalReflowProvider _reflowStrategy;
    private TerminalCapabilities _capabilities;
    private byte[] _prefetchedInput = [];
    private Encoding? _inputEncoding;
    private Decoder? _inputDecoder;
    private bool _kgpProbeCompleted;
    private bool _backgroundProbeCompleted;
    private bool _reflowEnabled;
    private bool _disposed;
    private bool _inRawMode;

    /// <summary>
    /// Creates a new console presentation adapter with raw mode support.
    /// </summary>
    /// <param name="enableMouse">Whether to enable mouse tracking.</param>
    /// <param name="preserveOPost">
    /// If true, preserve output post-processing (LF→CRLF conversion) in raw mode.
    /// This is useful for WithProcess scenarios where child programs expect normal output handling.
    /// Defaults to false for full raw mode (required for terminal emulators and Hex1bApp).
    /// </param>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown if raw mode is not supported on the current platform.
    /// </exception>
    public ConsolePresentationAdapter(bool enableMouse = false, bool preserveOPost = false)
        : this(CreateConsoleDriver(), enableMouse, preserveOPost)
    {
    }

    internal ConsolePresentationAdapter(
        IConsoleDriver driver,
        bool enableMouse = false,
        bool preserveOPost = false,
        TimeSpan? kgpProbeTimeout = null)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _enableMouse = enableMouse;
        _preserveOPost = preserveOPost;
        _kgpProbeTimeout = kgpProbeTimeout ?? DefaultKgpProbeTimeout;
        _capabilities = CreateCapabilities(supportsKgp: false);
        
        // Wire up resize events
        _driver.Resized += (w, h) => Resized?.Invoke(w, h);
        
        // Auto-detect terminal emulator reflow strategy (not enabled by default)
        _reflowStrategy = DetectReflowStrategy();
    }

    /// <summary>
    /// Enables reflow using the auto-detected strategy for the current terminal emulator.
    /// By default, reflow is disabled and resize uses standard crop behavior.
    /// </summary>
    /// <returns>This adapter for fluent chaining.</returns>
    public ConsolePresentationAdapter WithReflow()
    {
        _reflowEnabled = true;
        return this;
    }

    /// <summary>
    /// Enables reflow with a specific strategy, overriding auto-detection.
    /// By default, reflow is disabled and resize uses standard crop behavior.
    /// </summary>
    /// <param name="strategy">The reflow strategy to use during resize operations.</param>
    /// <returns>This adapter for fluent chaining.</returns>
    public ConsolePresentationAdapter WithReflow(ITerminalReflowProvider strategy)
    {
        _reflowStrategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _reflowEnabled = true;
        return this;
    }

    /// <inheritdoc/>
    public bool ReflowEnabled => _reflowEnabled;

    /// <inheritdoc/>
    public bool ShouldClearSoftWrapOnAbsolutePosition => _reflowStrategy.ShouldClearSoftWrapOnAbsolutePosition;

    /// <inheritdoc/>
    public ReflowResult Reflow(ReflowContext context) => _reflowStrategy.Reflow(context);

    /// <summary>
    /// Detects the current terminal emulator and returns the appropriate reflow strategy.
    /// </summary>
    private static ITerminalReflowProvider DetectReflowStrategy()
    {
        return AutoReflowStrategy.Detect();
    }

    /// <inheritdoc />
    public int Width => _driver.Width;

    /// <inheritdoc />
    public int Height => _driver.Height;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => _capabilities;

    private static IConsoleDriver CreateConsoleDriver()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return new UnixConsoleDriver();
        }

        if (OperatingSystem.IsWindows())
        {
            return new WindowsConsoleDriver();
        }

        throw new PlatformNotSupportedException(
            $"Platform {Environment.OSVersion} is not supported.");
    }

    private TerminalCapabilities CreateCapabilities(bool supportsKgp)
    {
        return new TerminalCapabilities
        {
            SupportsMouse = _enableMouse,
            SupportsTrueColor = true,
            Supports256Colors = true,
            SupportsAlternateScreen = true,
            HandlesAlternateScreenNatively = true,  // Real upstream terminal handles buffer switching
            SupportsBracketedPaste = true,  // Raw mode can handle this
            SupportsStyledUnderlines = true,
            SupportsUnderlineColor = true,
            SupportsKgp = supportsKgp
        };
    }

    /// <inheritdoc />
    public event Action<int, int>? Resized;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed) return ValueTask.CompletedTask;

        _driver.Write(data.Span);
        _driver.Flush();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        if (_disposed) return ReadOnlyMemory<byte>.Empty;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);
        
        var buffer = new byte[256];
        
        try
        {
            if (_prefetchedInput.Length > 0)
            {
                var prefetched = _prefetchedInput;
                _prefetchedInput = [];
                var result = NormalizeInputToUtf8(prefetched);
                if (!result.IsEmpty)
                {
                    return result;
                }
            }

            while (!linkedCts.Token.IsCancellationRequested)
            {
                var bytesRead = await _driver.ReadAsync(buffer, linkedCts.Token);

                if (bytesRead == 0)
                {
                    // EOF or cancelled
                    return ReadOnlyMemory<byte>.Empty;
                }

                var result = NormalizeInputToUtf8(buffer.AsSpan(0, bytesRead));
                if (!result.IsEmpty)
                {
                    return result;
                }
            }

            return ReadOnlyMemory<byte>.Empty;
        }
        catch (OperationCanceledException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    private ReadOnlyMemory<byte> NormalizeInputToUtf8(ReadOnlySpan<byte> input)
    {
        var encoding = _driver.InputEncoding;
        if (IsUtf8(encoding))
        {
            return input.ToArray();
        }

        if (_inputDecoder is null || !IsSameEncoding(_inputEncoding, encoding))
        {
            _inputEncoding = encoding;
            _inputDecoder = encoding.GetDecoder();
        }

        var chars = new char[encoding.GetMaxCharCount(input.Length)];
        var charCount = _inputDecoder.GetChars(input, chars, flush: false);
        if (charCount == 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        return Encoding.UTF8.GetBytes(new string(chars, 0, charCount));
    }

    private static bool IsUtf8(Encoding encoding)
        => encoding.CodePage == Encoding.UTF8.CodePage ||
           string.Equals(GetBodyNameOrNull(encoding), Encoding.UTF8.BodyName, StringComparison.OrdinalIgnoreCase);

    private static bool IsSameEncoding(Encoding? left, Encoding right)
        => left is not null &&
           left.CodePage == right.CodePage &&
           string.Equals(GetBodyNameOrNull(left), GetBodyNameOrNull(right), StringComparison.OrdinalIgnoreCase);

    private static string? GetBodyNameOrNull(Encoding encoding)
    {
        try
        {
            return encoding.BodyName;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken ct = default)
    {
        _driver.Flush();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask EnterRawModeAsync(CancellationToken ct = default)
    {
        if (_inRawMode) return ValueTask.CompletedTask;
        _inRawMode = true;

        // Enter raw mode for proper input capture
        // No escape sequences - screen mode is controlled by the workload
        _driver.EnterRawMode(_preserveOPost);

        return ProbeCapabilitiesAsync(ct);
    }

    /// <inheritdoc />
    public ValueTask ExitRawModeAsync(CancellationToken ct = default)
    {
        if (!_inRawMode) return ValueTask.CompletedTask;
        _inRawMode = false;

        // Drain any pending input before exiting raw mode
        for (int i = 0; i < 3; i++)
        {
            Thread.Sleep(20);
            _driver.DrainInput();
        }

        // Exit raw mode
        _driver.ExitRawMode();

        return ValueTask.CompletedTask;
    }

    private async ValueTask ProbeCapabilitiesAsync(CancellationToken ct)
    {
        if (_kgpProbeCompleted)
            return;

        _kgpProbeCompleted = true;

        // Windows uses console input records rather than a raw stdin byte stream, so
        // KGP APC replies are not currently probeable through the built-in console driver.
        if (_driver is WindowsConsoleDriver)
            return;

        _driver.Write(KgpProbeQuery);
        // Piggyback an OSC 11 background-colour query on the same probe pass.
        // Both responses arrive on stdin and are demuxed by signature.
        _driver.Write(BackgroundProbeQuery);
        _driver.Flush();

        var bufferedInput = new List<byte>();
        var readBuffer = new byte[256];

        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);
        probeCts.CancelAfter(_kgpProbeTimeout);

        try
        {
            while (!probeCts.IsCancellationRequested)
            {
                var bytesRead = await _driver.ReadAsync(readBuffer, probeCts.Token);
                if (bytesRead <= 0)
                    break;

                bufferedInput.AddRange(readBuffer.AsSpan(0, bytesRead).ToArray());

                if (TryConsumeKgpProbeResponse(bufferedInput, KgpProbeImageId))
                {
                    _capabilities = _capabilities with { SupportsKgp = true };
                }

                if (!_backgroundProbeCompleted &&
                    TryConsumeBackgroundProbeResponse(bufferedInput, out var bgRgb))
                {
                    _capabilities = _capabilities with { DefaultBackground = bgRgb };
                    _backgroundProbeCompleted = true;
                }

                if (_capabilities.SupportsKgp && _backgroundProbeCompleted)
                    break;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && !_disposeCts.IsCancellationRequested)
        {
            // Timed out waiting for a probe reply: treat unanswered probes as unsupported / use defaults.
        }
        finally
        {
            if (bufferedInput.Count > 0)
            {
                AppendPrefetchedInput(CollectionsMarshal.AsSpan(bufferedInput));
            }
        }
    }

    private void AppendPrefetchedInput(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;

        if (_prefetchedInput.Length == 0)
        {
            _prefetchedInput = data.ToArray();
            return;
        }

        var combined = new byte[_prefetchedInput.Length + data.Length];
        _prefetchedInput.CopyTo(combined, 0);
        data.CopyTo(combined.AsSpan(_prefetchedInput.Length));
        _prefetchedInput = combined;
    }

    private static bool TryConsumeKgpProbeResponse(List<byte> buffer, uint probeImageId)
    {
        var span = CollectionsMarshal.AsSpan(buffer);
        for (var start = 0; start <= span.Length - 4; start++)
        {
            if (span[start] != 0x1b || span[start + 1] != (byte)'_' || span[start + 2] != (byte)'G')
                continue;

            for (var end = start + 3; end < span.Length - 1; end++)
            {
                if (span[end] != 0x1b || span[end + 1] != (byte)'\\')
                    continue;

                var content = Encoding.ASCII.GetString(span[(start + 3)..end]);
                if (IsKgpProbeResponse(content, probeImageId))
                {
                    buffer.RemoveRange(start, end + 2 - start);
                    return true;
                }

                start = end + 1;
                break;
            }
        }

        return false;
    }

    private static bool IsKgpProbeResponse(string response, uint probeImageId)
    {
        var separator = response.IndexOf(';');
        if (separator < 0)
            return false;

        var controlData = response[..separator];
        var fields = controlData.Split(',');
        foreach (var field in fields)
        {
            if (!field.StartsWith("i=", StringComparison.Ordinal))
                continue;

            if (uint.TryParse(field.AsSpan(2), out var imageId) && imageId == probeImageId)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Scans the buffered input for an OSC 11 background-colour reply. The reply
    /// has the shape <c>ESC ] 11 ; rgb:RRRR/GGGG/BBBB ST</c> where ST is either
    /// <c>ESC \</c> (string terminator) or <c>BEL</c> (0x07). Component widths
    /// vary by terminal — common widths are 4 hex digits (xterm) but 2 and even
    /// 1 are also seen in the wild — so we parse leniently and scale up.
    /// </summary>
    private static bool TryConsumeBackgroundProbeResponse(List<byte> buffer, out int rgb)
    {
        rgb = 0;
        var span = CollectionsMarshal.AsSpan(buffer);
        for (var start = 0; start <= span.Length - 5; start++)
        {
            // Match ESC ] 1 1 ;
            if (span[start] != 0x1b || span[start + 1] != (byte)']' ||
                span[start + 2] != (byte)'1' || span[start + 3] != (byte)'1' ||
                span[start + 4] != (byte)';')
                continue;

            // Find the string terminator (ESC \ or BEL).
            var end = -1;
            var terminatorLength = 0;
            for (var i = start + 5; i < span.Length; i++)
            {
                if (span[i] == 0x07)
                {
                    end = i;
                    terminatorLength = 1;
                    break;
                }
                if (span[i] == 0x1b && i + 1 < span.Length && span[i + 1] == (byte)'\\')
                {
                    end = i;
                    terminatorLength = 2;
                    break;
                }
            }

            if (end < 0)
                return false; // payload incomplete; wait for more bytes

            var payload = Encoding.ASCII.GetString(span[(start + 5)..end]);
            if (TryParseRgbColor(payload, out rgb))
            {
                buffer.RemoveRange(start, end + terminatorLength - start);
                return true;
            }

            // Malformed payload; skip past this match and keep looking.
            start = end + terminatorLength - 1;
        }

        return false;
    }

    private static bool TryParseRgbColor(string payload, out int rgb)
    {
        rgb = 0;

        // Expected form: "rgb:RRRR/GGGG/BBBB" (xterm) or shorter widths.
        const string prefix = "rgb:";
        if (!payload.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var components = payload[prefix.Length..].Split('/');
        if (components.Length != 3)
            return false;

        Span<byte> rgbBytes = stackalloc byte[3];
        for (var i = 0; i < 3; i++)
        {
            var c = components[i];
            if (c.Length == 0 || c.Length > 4)
                return false;
            if (!ushort.TryParse(c, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
                return false;

            // Scale variable-width hex (1..4 digits) up to 8 bits. xterm uses 4
            // digits per channel (16-bit), so RRRR maps to (RRRR >> 8). Shorter
            // widths are bit-extended by repeating the highest nibble.
            int scaled = c.Length switch
            {
                1 => (value << 4) | value,                  // 0xR  -> 0xRR
                2 => value,                                  // already 8-bit
                3 => (value << 4) | (value & 0x000F),        // pad low nibble
                4 => value >> 8,                             // xterm 16-bit -> 8-bit
                _ => value
            };
            rgbBytes[i] = (byte)Math.Clamp(scaled, 0, 255);
        }

        rgb = (rgbBytes[0] << 16) | (rgbBytes[1] << 8) | rgbBytes[2];
        return true;
    }

    /// <inheritdoc />
    public (int Row, int Column) GetCursorPosition()
    {
        try
        {
            var (left, top) = Console.GetCursorPosition();
            return (top, left);
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        Disconnected?.Invoke();

        if (_inRawMode)
        {
            await ExitRawModeAsync();
        }

        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _driver.Dispose();
    }
}
