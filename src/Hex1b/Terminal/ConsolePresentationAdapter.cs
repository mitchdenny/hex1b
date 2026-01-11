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
    private readonly bool _preserveOPost;
    private readonly CancellationTokenSource _disposeCts = new();
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
    {
        _enableMouse = enableMouse;
        _preserveOPost = preserveOPost;
        
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

        return ValueTask.CompletedTask;
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
