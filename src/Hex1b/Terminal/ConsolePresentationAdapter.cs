using System.Text;
using Hex1b.Input;

namespace Hex1b.Terminal;

/// <summary>
/// Console presentation adapter using platform-specific raw mode for proper input handling.
/// </summary>
/// <remarks>
/// This adapter uses raw terminal mode (termios on Unix, SetConsoleMode on Windows)
/// to properly capture mouse events, escape sequences, and control characters.
/// </remarks>
public sealed class ConsolePresentationAdapter : IHex1bTerminalPresentationAdapter
{
    private readonly IConsoleDriver _driver;
    private readonly bool _enableMouse;
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _disposed;
    private bool _inTuiMode;

    private const string EnterAlternateBuffer = "\x1b[?1049h";
    private const string ExitAlternateBuffer = "\x1b[?1049l";
    private const string ClearScreen = "\x1b[2J";
    private const string MoveCursorHome = "\x1b[H";
    private const string HideCursor = "\x1b[?25l";
    private const string ShowCursor = "\x1b[?25h";

    /// <summary>
    /// Creates a new console presentation adapter with raw mode support.
    /// </summary>
    /// <param name="enableMouse">Whether to enable mouse tracking.</param>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown if raw mode is not supported on the current platform.
    /// </exception>
    public ConsolePresentationAdapter(bool enableMouse = false)
    {
        _enableMouse = enableMouse;
        
        // Create platform-specific driver
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            _driver = new UnixConsoleDriver();
        }
        else if (OperatingSystem.IsWindows())
        {
            _driver = new WindowsConsoleDriver();
        }
        else
        {
            throw new PlatformNotSupportedException(
                $"Platform {Environment.OSVersion} is not supported.");
        }
        
        // Wire up resize events
        _driver.Resized += (w, h) => Resized?.Invoke(w, h);
    }

    /// <inheritdoc />
    public int Width => _driver.Width;

    /// <inheritdoc />
    public int Height => _driver.Height;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => new()
    {
        SupportsMouse = _enableMouse,
        SupportsTrueColor = true,
        Supports256Colors = true,
        SupportsAlternateScreen = true,
        SupportsBracketedPaste = true  // Raw mode can handle this
    };

    /// <inheritdoc />
    public event Action<int, int>? Resized;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed) return ValueTask.CompletedTask;

        _driver.Write(data.Span);
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
            var bytesRead = await _driver.ReadAsync(buffer, linkedCts.Token);
            
            if (bytesRead == 0)
            {
                // EOF or cancelled
                return ReadOnlyMemory<byte>.Empty;
            }
            
            return buffer.AsMemory(0, bytesRead);
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
    public ValueTask EnterTuiModeAsync(CancellationToken ct = default)
    {
        if (_inTuiMode) return ValueTask.CompletedTask;
        _inTuiMode = true;

        // Enter raw mode first
        _driver.EnterRawMode();

        // Send escape sequences for TUI mode
        var escapes = new StringBuilder();
        escapes.Append(EnterAlternateBuffer);
        escapes.Append(HideCursor);
        if (_enableMouse)
        {
            escapes.Append(MouseParser.EnableMouseTracking);
        }
        escapes.Append(ClearScreen);
        escapes.Append(MoveCursorHome);

        _driver.Write(Encoding.UTF8.GetBytes(escapes.ToString()));
        _driver.Flush();

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ExitTuiModeAsync(CancellationToken ct = default)
    {
        if (!_inTuiMode) return ValueTask.CompletedTask;
        _inTuiMode = false;

        // First, disable mouse tracking to stop new events from being sent
        if (_enableMouse)
        {
            _driver.Write(Encoding.UTF8.GetBytes(MouseParser.DisableMouseTracking));
            _driver.Flush();
            
            // Drain input multiple times with delays to catch any in-flight mouse events
            // Mouse events can still be arriving from the terminal after we send disable
            for (int i = 0; i < 3; i++)
            {
                Thread.Sleep(20);
                _driver.DrainInput();
            }
        }

        // Now send the rest of the exit sequences
        var escapes = new StringBuilder();
        escapes.Append(ShowCursor);
        escapes.Append(ExitAlternateBuffer);

        _driver.Write(Encoding.UTF8.GetBytes(escapes.ToString()));
        _driver.Flush();

        // Exit raw mode last
        _driver.ExitRawMode();

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        Disconnected?.Invoke();

        if (_inTuiMode)
        {
            await ExitTuiModeAsync();
        }

        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _driver.Dispose();
    }
}
