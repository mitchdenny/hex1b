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
    private static readonly TimeSpan DefaultKgpProbeTimeout = TimeSpan.FromMilliseconds(150);

    private readonly IConsoleDriver _driver;
    private readonly bool _enableMouse;
    private readonly bool _preserveOPost;
    private readonly TimeSpan _kgpProbeTimeout;
    private readonly CancellationTokenSource _disposeCts = new();
    private ITerminalReflowProvider _reflowStrategy;
    private TerminalCapabilities _capabilities;
    private byte[] _prefetchedInput = [];
    private bool _kgpProbeCompleted;
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

        if (_prefetchedInput.Length > 0)
        {
            var prefetched = _prefetchedInput;
            _prefetchedInput = [];
            return prefetched;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);
        
        var buffer = new byte[256];
        
        try
        {
            var bytesRead = await _driver.ReadAsync(buffer, linkedCts.Token);
            
            if (bytesRead == 0)
            {
                // EOF or cancelled
                return ReadOnlyMemory<byte>.Empty;
            }
            
            var result = buffer.AsMemory(0, bytesRead);
            return result;
        }
        catch (OperationCanceledException)
        {
            return ReadOnlyMemory<byte>.Empty;
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
        // However, if a parent Hex1b terminal has advertised KGP support via environment
        // variable, trust that and enable KGP without probing.
        if (_driver is WindowsConsoleDriver)
        {
            if (Environment.GetEnvironmentVariable("HEX1B_TERMINAL_KGP") == "1")
            {
                _capabilities = _capabilities with { SupportsKgp = true };
            }
            return;
        }

        _driver.Write(KgpProbeQuery);
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
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && !_disposeCts.IsCancellationRequested)
        {
            // Timed out waiting for a KGP reply: treat as unsupported.
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
