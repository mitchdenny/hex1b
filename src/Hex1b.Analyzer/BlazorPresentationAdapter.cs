using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Hex1b.Terminal;

namespace Hex1b.Analyzer;

/// <summary>
/// Message types for framed communication with the Blazor client.
/// </summary>
public enum TerminalMessageType
{
    /// <summary>Terminal output data.</summary>
    Output,
    /// <summary>Resize request from client.</summary>
    Resize,
    /// <summary>Input from client.</summary>
    Input,
    /// <summary>Initial state sync.</summary>
    StateSync,
    /// <summary>Terminal dimensions info.</summary>
    Dimensions
}

/// <summary>
/// A framed message for Blazor client communication.
/// </summary>
public sealed record TerminalMessage(TerminalMessageType Type, string Data);

/// <summary>
/// Presentation adapter that provides streaming output for Blazor clients.
/// </summary>
/// <remarks>
/// <para>
/// This adapter captures terminal output and provides it as an <see cref="IAsyncEnumerable{T}"/>
/// that Blazor components can subscribe to for streaming updates to xterm.js.
/// </para>
/// <para>
/// When a client reconnects, it can query the current terminal state and then resume
/// receiving live updates.
/// </para>
/// </remarks>
public sealed class BlazorPresentationAdapter : IHex1bTerminalPresentationAdapter
{
    /// <summary>Maximum buffer size before trimming (1MB).</summary>
    private const int MaxBufferSize = 1_000_000;
    
    /// <summary>Size to retain when trimming buffer.</summary>
    private const int TrimmedBufferSize = 500_000;

    private readonly Channel<TerminalMessage> _outputChannel;
    private readonly Channel<ReadOnlyMemory<byte>> _inputChannel;
    private readonly StringBuilder _outputBuffer = new();
    private readonly object _bufferLock = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private int _width;
    private int _height;
    private bool _disposed;

    /// <summary>
    /// Creates a new Blazor presentation adapter.
    /// </summary>
    /// <param name="width">Initial terminal width.</param>
    /// <param name="height">Initial terminal height.</param>
    public BlazorPresentationAdapter(int width, int height)
    {
        _width = width;
        _height = height;

        _outputChannel = Channel.CreateBounded<TerminalMessage>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = true
        });

        _inputChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <inheritdoc />
    public int Width => _width;

    /// <inheritdoc />
    public int Height => _height;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => new()
    {
        SupportsMouse = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
        SupportsAlternateScreen = true,
        SupportsBracketedPaste = true
    };

    /// <inheritdoc />
    public event Action<int, int>? Resized;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed) return ValueTask.CompletedTask;

        var text = Encoding.UTF8.GetString(data.Span);
        
        // Buffer output for state queries
        lock (_bufferLock)
        {
            _outputBuffer.Append(text);
            // Limit buffer size to prevent memory issues
            if (_outputBuffer.Length > MaxBufferSize)
            {
                _outputBuffer.Remove(0, _outputBuffer.Length - TrimmedBufferSize);
            }
        }

        // Send to output channel for streaming
        var message = new TerminalMessage(TerminalMessageType.Output, text);
        _outputChannel.Writer.TryWrite(message);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        if (_disposed) return ReadOnlyMemory<byte>.Empty;

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);
            return await _inputChannel.Reader.ReadAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
        catch (ChannelClosedException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    /// <summary>
    /// Gets an async enumerable of terminal messages for streaming to clients.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of terminal messages.</returns>
    public async IAsyncEnumerable<TerminalMessage> GetOutputStreamAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_disposed) yield break;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);

        // First send dimensions
        yield return new TerminalMessage(TerminalMessageType.Dimensions, 
            JsonSerializer.Serialize(new { cols = _width, rows = _height }));

        // Then stream updates
        await foreach (var message in _outputChannel.Reader.ReadAllAsync(linkedCts.Token))
        {
            yield return message;
        }
    }

    /// <summary>
    /// Gets the current buffered terminal output for state sync on reconnection.
    /// </summary>
    /// <returns>The buffered output as a string.</returns>
    public string GetBufferedOutput()
    {
        lock (_bufferLock)
        {
            return _outputBuffer.ToString();
        }
    }

    /// <summary>
    /// Gets the current terminal dimensions.
    /// </summary>
    /// <returns>A tuple of (cols, rows).</returns>
    public (int Cols, int Rows) GetDimensions() => (_width, _height);

    /// <summary>
    /// Sends input from the Blazor client to the terminal.
    /// </summary>
    /// <param name="data">The input data as a string.</param>
    public void SendInput(string data)
    {
        if (_disposed) return;

        var bytes = Encoding.UTF8.GetBytes(data);
        _inputChannel.Writer.TryWrite(bytes);
    }

    /// <summary>
    /// Handles a resize request from the Blazor client.
    /// </summary>
    /// <param name="cols">New width in columns.</param>
    /// <param name="rows">New height in rows.</param>
    public void HandleResize(int cols, int rows)
    {
        if (_disposed) return;

        if (_width != cols || _height != rows)
        {
            _width = cols;
            _height = rows;
            Resized?.Invoke(cols, rows);
        }
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken ct = default)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask EnterRawModeAsync(CancellationToken ct = default)
    {
        // Blazor clients are already "raw" - xterm.js handles the terminal emulation
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ExitRawModeAsync(CancellationToken ct = default)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        Disconnected?.Invoke();

        _disposeCts.Cancel();
        _outputChannel.Writer.Complete();
        _inputChannel.Writer.Complete();
        _disposeCts.Dispose();

        return ValueTask.CompletedTask;
    }
}
