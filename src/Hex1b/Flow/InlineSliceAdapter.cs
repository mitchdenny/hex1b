using System.Text;
using System.Threading.Channels;
using Hex1b.Input;

namespace Hex1b.Flow;

/// <summary>
/// Workload adapter for inline slice rendering. Renders in the normal terminal buffer
/// without entering the alternate screen. All cursor positioning is offset by the
/// slice's row origin in the terminal.
/// </summary>
internal sealed class InlineSliceAdapter : IHex1bAppTerminalWorkloadAdapter, IDisposable
{
    private readonly Channel<byte[]> _outputChannel;
    private readonly Channel<Hex1bEvent> _inputChannel;
    private readonly TerminalCapabilities _capabilities;
    private int _width;
    private int _height;
    private int _rowOrigin;
    private bool _disposed;
    private int _outputQueueDepth;
    private bool _inTuiMode;

    public InlineSliceAdapter(int width, int height, int rowOrigin, TerminalCapabilities? capabilities = null)
    {
        _width = width;
        _height = height;
        _rowOrigin = rowOrigin;
        _capabilities = capabilities ?? new TerminalCapabilities
        {
            SupportsMouse = false,
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

    /// <summary>
    /// The row origin of this slice in the terminal buffer.
    /// </summary>
    public int RowOrigin
    {
        get => _rowOrigin;
        set => _rowOrigin = value;
    }

    // === App-side APIs ===

    public void Write(string text)
    {
        if (_disposed) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        if (_outputChannel.Writer.TryWrite(bytes))
        {
            Interlocked.Increment(ref _outputQueueDepth);
        }
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (_disposed) return;
        if (_outputChannel.Writer.TryWrite(data.ToArray()))
        {
            Interlocked.Increment(ref _outputQueueDepth);
        }
    }

    public void Flush() { }

    public ChannelReader<Hex1bEvent> InputEvents => _inputChannel.Reader;
    public int Width => _width;
    public int Height => _height;
    public TerminalCapabilities Capabilities => _capabilities;
    public int OutputQueueDepth => _outputQueueDepth;

    /// <summary>
    /// Enter TUI mode for inline slice — hides cursor but does NOT enter alternate screen.
    /// </summary>
    public void EnterTuiMode()
    {
        if (_inTuiMode) return;
        _inTuiMode = true;
        Write("\x1b[?25l"); // Hide cursor
    }

    /// <summary>
    /// Exit TUI mode for inline slice — shows cursor, no alternate screen restore.
    /// </summary>
    public void ExitTuiMode()
    {
        if (!_inTuiMode) return;
        _inTuiMode = false;

        var sb = new StringBuilder();
        sb.Append("\x1b[0m");   // Reset text attributes
        sb.Append("\x1b[?25h"); // Show cursor
        Write(sb.ToString());
    }

    /// <summary>
    /// Clear the slice region only.
    /// </summary>
    public void Clear()
    {
        var sb = new StringBuilder();
        for (int row = 0; row < _height; row++)
        {
            sb.Append($"\x1b[{_rowOrigin + row + 1};1H"); // Move to row
            sb.Append("\x1b[2K");                          // Clear entire line
        }
        Write(sb.ToString());
    }

    /// <summary>
    /// Set cursor position with row offset applied.
    /// </summary>
    public void SetCursorPosition(int left, int top)
    {
        Write($"\x1b[{_rowOrigin + top + 1};{left + 1}H");
    }

    // === Terminal-side APIs ===

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
        catch (OperationCanceledException) { }
        catch (ChannelClosedException) { }

        return ReadOnlyMemory<byte>.Empty;
    }

    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        // Raw byte input is not used for flow slices — events arrive via WriteInputEventAsync
        return ValueTask.CompletedTask;
    }

    public ValueTask WriteInputEventAsync(Hex1bEvent evt, CancellationToken ct = default)
    {
        if (_disposed) return ValueTask.CompletedTask;
        return _inputChannel.Writer.WriteAsync(evt, ct);
    }

    public bool TryWriteInputEvent(Hex1bEvent evt)
    {
        if (_disposed) return false;
        return _inputChannel.Writer.TryWrite(evt);
    }

    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        var changed = _width != width || _height != height;
        _width = width;
        _height = height;
        if (changed)
        {
            _inputChannel.Writer.TryWrite(new Hex1bResizeEvent(width, height));
        }
        return ValueTask.CompletedTask;
    }

    public event Action? Disconnected;

    internal void RaiseDisconnected() => Disconnected?.Invoke();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _outputChannel.Writer.TryComplete();
        _inputChannel.Writer.TryComplete();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
