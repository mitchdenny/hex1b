using System.Text;
using System.Threading.Channels;
using Hex1b.Input;

namespace Hex1b.Terminal;

/// <summary>
/// Workload adapter for Hex1bApp TUI applications.
/// Bridges the raw byte interface of <see cref="IHex1bTerminalWorkloadAdapter"/>
/// with the higher-level APIs that Hex1bApp needs.
/// </summary>
/// <remarks>
/// <para>
/// This adapter has two faces:
/// <list type="bullet">
///   <item>Terminal side: Implements <see cref="IHex1bTerminalWorkloadAdapter"/> for Hex1bTerminal</item>
///   <item>App side: Provides <see cref="Write(string)"/>, <see cref="InputEvents"/>, etc. for Hex1bApp</item>
/// </list>
/// </para>
/// <para>
/// Data flow:
/// <list type="bullet">
///   <item>App calls Write() → bytes queued → Terminal calls ReadOutputAsync()</item>
///   <item>Terminal calls WriteInputAsync() → parsed to events → App reads InputEvents</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var workload = new Hex1bAppWorkloadAdapter();
/// var terminal = new Hex1bTerminal(workload, 80, 24);
/// var app = new Hex1bApp(workload, ctx => ctx.Text("Hello"));
/// await app.RunAsync();
/// </code>
/// </example>
public sealed class Hex1bAppWorkloadAdapter : IHex1bAppTerminalWorkloadAdapter, IDisposable
{
    private readonly Channel<byte[]> _outputChannel;
    private readonly Channel<Hex1bEvent> _inputChannel;
    private int _width;
    private int _height;
    private bool _disposed;
    private bool _inTuiMode;
    private bool _dimensionsInitialized;
    private int _outputQueueDepth; // Manual tracking since unbounded channels don't support Count

    /// <summary>
    /// Creates a new app workload adapter.
    /// </summary>
    /// <param name="capabilities">Terminal capabilities. If null, defaults with full support.</param>
    /// <remarks>
    /// Dimensions are set by the terminal via <see cref="ResizeAsync"/>.
    /// Initial dimensions default to 0x0 until the terminal notifies.
    /// </remarks>
    public Hex1bAppWorkloadAdapter(TerminalCapabilities? capabilities = null)
    {
        _width = 0;
        _height = 0;
        Capabilities = capabilities ?? new TerminalCapabilities
        {
            SupportsMouse = true,
            Supports256Colors = true,
            SupportsTrueColor = true,
        };

        _outputChannel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _inputChannel = Channel.CreateUnbounded<Hex1bEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
    }

    // ========================================
    // App-side APIs (used by Hex1bApp)
    // ========================================

    /// <summary>
    /// Write ANSI-encoded output to the terminal.
    /// </summary>
    public void Write(string text)
    {
        if (_disposed) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        if (_outputChannel.Writer.TryWrite(bytes))
        {
            Interlocked.Increment(ref _outputQueueDepth);
        }
    }

    /// <summary>
    /// Write raw bytes to the terminal.
    /// </summary>
    public void Write(ReadOnlySpan<byte> data)
    {
        if (_disposed) return;
        if (_outputChannel.Writer.TryWrite(data.ToArray()))
        {
            Interlocked.Increment(ref _outputQueueDepth);
        }
    }

    /// <summary>
    /// Flush any buffered output.
    /// </summary>
    public void Flush()
    {
        // Channel-based, no buffering needed
    }

    /// <summary>
    /// Channel of parsed input events from the terminal.
    /// </summary>
    public ChannelReader<Hex1bEvent> InputEvents => _inputChannel.Reader;

    /// <summary>
    /// Current terminal width.
    /// </summary>
    public int Width => _width;

    /// <summary>
    /// Current terminal height.
    /// </summary>
    public int Height => _height;

    /// <summary>
    /// Terminal capabilities.
    /// </summary>
    public TerminalCapabilities Capabilities { get; }
    
    /// <summary>
    /// Gets the number of output items waiting to be consumed by the terminal.
    /// Can be used to detect back pressure and adjust input processing accordingly.
    /// </summary>
    public int OutputQueueDepth => _outputQueueDepth;

    /// <summary>
    /// Enter TUI mode. Writes standard ANSI sequences for alternate screen, hide cursor, enable mouse.
    /// </summary>
    public void EnterTuiMode()
    {
        if (_inTuiMode) return;
        _inTuiMode = true;

        // Standard TUI mode sequences
        var sb = new StringBuilder();
        sb.Append("\x1b[?1049h");  // Enter alternate screen
        sb.Append("\x1b[?25l");    // Hide cursor
        if (Capabilities.SupportsMouse)
        {
            sb.Append("\x1b[?1003h");  // Enable mouse tracking
            sb.Append("\x1b[?1006h");  // SGR mouse mode
        }
        Write(sb.ToString());
    }

    /// <summary>
    /// Exit TUI mode. Restores terminal state.
    /// </summary>
    public void ExitTuiMode()
    {
        if (!_inTuiMode) return;
        _inTuiMode = false;

        var sb = new StringBuilder();
        if (Capabilities.SupportsMouse)
        {
            sb.Append("\x1b[?1006l");  // Disable SGR mouse mode
            sb.Append("\x1b[?1003l");  // Disable mouse tracking
        }
        sb.Append("\x1b[?25h");    // Show cursor
        sb.Append("\x1b[?1049l");  // Exit alternate screen
        Write(sb.ToString());
    }

    /// <summary>
    /// Clear the screen.
    /// </summary>
    public void Clear()
    {
        Write("\x1b[2J\x1b[H");
    }

    /// <summary>
    /// Set the cursor position.
    /// </summary>
    public void SetCursorPosition(int left, int top)
    {
        Write($"\x1b[{top + 1};{left + 1}H");
    }

    // ========================================
    // Terminal-side APIs (IHex1bTerminalWorkloadAdapter)
    // ========================================

    /// <summary>
    /// Tries to read output without blocking. Returns true if data was available.
    /// </summary>
    /// <param name="data">The output data if available, or empty if not.</param>
    /// <returns>True if data was available, false otherwise.</returns>
    public bool TryReadOutput(out ReadOnlyMemory<byte> data)
    {
        data = ReadOnlyMemory<byte>.Empty;
        if (_disposed) return false;
        
        if (_outputChannel.Reader.TryRead(out var bytes))
        {
            Interlocked.Decrement(ref _outputQueueDepth);
            data = bytes;
            return true;
        }
        return false;
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        if (_disposed) return ReadOnlyMemory<byte>.Empty;

        try
        {
            if (await _outputChannel.Reader.WaitToReadAsync(ct))
            {
                if (_outputChannel.Reader.TryRead(out var bytes))
                {
                    Interlocked.Decrement(ref _outputQueueDepth);
                    return bytes;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (ChannelClosedException)
        {
            // Channel completed
        }

        return ReadOnlyMemory<byte>.Empty;
    }

    /// <inheritdoc />
    public async ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed) return;

        // Parse raw bytes into events and write to input channel
        // For now, we assume the terminal has already parsed bytes into events
        // and calls WriteInputEventAsync instead
        
        // If we receive raw bytes, we need to parse them
        // This is a simplified version - full parsing is in Hex1bTerminal
        var text = Encoding.UTF8.GetString(data.Span);
        foreach (var c in text)
        {
            var evt = ParseKeyInput(c);
            if (evt != null)
            {
                await _inputChannel.Writer.WriteAsync(evt, ct);
            }
        }
    }

    /// <summary>
    /// Write a parsed input event directly (used by Hex1bTerminal after parsing).
    /// </summary>
    public ValueTask WriteInputEventAsync(Hex1bEvent evt, CancellationToken ct = default)
    {
        if (_disposed) return ValueTask.CompletedTask;
        return _inputChannel.Writer.WriteAsync(evt, ct);
    }

    /// <summary>
    /// Write a parsed input event directly (synchronous).
    /// </summary>
    public bool TryWriteInputEvent(Hex1bEvent evt)
    {
        if (_disposed) return false;
        return _inputChannel.Writer.TryWrite(evt);
    }

    /// <inheritdoc />
    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        var wasInitialized = _dimensionsInitialized;
        _dimensionsInitialized = true;
        
        var changed = _width != width || _height != height;
        _width = width;
        _height = height;
        
        // Only fire resize event if dimensions changed AND we were already initialized
        // (skip the initial dimension setup from terminal constructor)
        if (changed && wasInitialized)
        {
            _inputChannel.Writer.TryWrite(new Hex1bResizeEvent(width, height));
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public event Action? Disconnected;

    // ========================================
    // Testing APIs
    // ========================================

    /// <summary>
    /// Injects a key input event (for testing).
    /// </summary>
    public void SendKey(ConsoleKey key, char keyChar = '\0', bool shift = false, bool alt = false, bool control = false)
    {
        var evt = KeyMapper.ToHex1bKeyEvent(key, keyChar, shift, alt, control);
        _inputChannel.Writer.TryWrite(evt);
    }

    /// <summary>
    /// Injects a key input event using Hex1bKey (for testing).
    /// </summary>
    public void SendKey(Hex1bKey key, char keyChar = '\0', Hex1bModifiers modifiers = Hex1bModifiers.None)
    {
        var evt = new Hex1bKeyEvent(key, keyChar, modifiers);
        _inputChannel.Writer.TryWrite(evt);
    }

    /// <summary>
    /// Injects a mouse input event (for testing).
    /// </summary>
    public void SendMouse(MouseButton button, MouseAction action, int x, int y, Hex1bModifiers modifiers = Hex1bModifiers.None, int clickCount = 1)
    {
        var evt = new Hex1bMouseEvent(button, action, x, y, modifiers, clickCount);
        _inputChannel.Writer.TryWrite(evt);
    }

    /// <summary>
    /// Types a string of characters (for testing).
    /// </summary>
    public void TypeText(string text)
    {
        foreach (var c in text)
        {
            var key = CharToConsoleKey(c);
            var shift = char.IsUpper(c);
            SendKey(key, c, shift: shift);
        }
    }

    // ========================================
    // Private helpers
    // ========================================

    private static Hex1bKeyEvent? ParseKeyInput(char c)
    {
        return c switch
        {
            '\r' or '\n' => new Hex1bKeyEvent(Hex1bKey.Enter, c, Hex1bModifiers.None),
            '\t' => new Hex1bKeyEvent(Hex1bKey.Tab, c, Hex1bModifiers.None),
            '\x1b' => new Hex1bKeyEvent(Hex1bKey.Escape, c, Hex1bModifiers.None),
            '\x7f' or '\b' => new Hex1bKeyEvent(Hex1bKey.Backspace, c, Hex1bModifiers.None),
            ' ' => new Hex1bKeyEvent(Hex1bKey.Spacebar, c, Hex1bModifiers.None),
            >= 'a' and <= 'z' => new Hex1bKeyEvent(
                KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (c - 'a'))), c, Hex1bModifiers.None),
            >= 'A' and <= 'Z' => new Hex1bKeyEvent(
                KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (c - 'A'))), c, Hex1bModifiers.Shift),
            >= '0' and <= '9' => new Hex1bKeyEvent(
                KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.D0 + (c - '0'))), c, Hex1bModifiers.None),
            _ when !char.IsControl(c) => new Hex1bKeyEvent(Hex1bKey.None, c, Hex1bModifiers.None),
            _ => null
        };
    }

    private static ConsoleKey CharToConsoleKey(char c)
    {
        return c switch
        {
            >= 'a' and <= 'z' => (ConsoleKey)((int)ConsoleKey.A + (c - 'a')),
            >= 'A' and <= 'Z' => (ConsoleKey)((int)ConsoleKey.A + (c - 'A')),
            >= '0' and <= '9' => (ConsoleKey)((int)ConsoleKey.D0 + (c - '0')),
            ' ' => ConsoleKey.Spacebar,
            '\r' or '\n' => ConsoleKey.Enter,
            '\t' => ConsoleKey.Tab,
            '\b' => ConsoleKey.Backspace,
            _ => ConsoleKey.NoName
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Note: ExitTuiMode is NOT called here because Hex1bTerminal handles
        // writing mouse-disable and screen-restore sequences directly to the
        // presentation layer during its disposal to avoid race conditions.

        _outputChannel.Writer.TryComplete();
        _inputChannel.Writer.TryComplete();
        Disconnected?.Invoke();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
