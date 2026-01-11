using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Hex1b.Terminal;

namespace Hex1b.Analyzer;

/// <summary>
/// Message types for framed communication with web clients.
/// </summary>
public enum TerminalMessageType
{
    /// <summary>Terminal output data.</summary>
    Output,
    /// <summary>Resize notification.</summary>
    Resize,
    /// <summary>Input from client.</summary>
    Input,
    /// <summary>Initial state sync.</summary>
    StateSync,
    /// <summary>Terminal dimensions info.</summary>
    Dimensions
}

/// <summary>
/// A framed message for web client communication.
/// </summary>
public sealed record TerminalMessage(TerminalMessageType Type, string Data);

/// <summary>
/// A presentation adapter that follows a primary adapter's dimensions and handles web client connections.
/// </summary>
/// <remarks>
/// <para>
/// This adapter "follows" the primary presentation adapter (typically ConsolePresentationAdapter),
/// mirroring its dimensions and forwarding resize events to connected web clients.
/// </para>
/// <para>
/// When a client connects, the adapter pauses I/O briefly to retransmit the current terminal state,
/// then resumes streaming updates.
/// </para>
/// </remarks>
public sealed class FollowingPresentationAdapter : IHex1bTerminalPresentationAdapter
{
    /// <summary>Maximum buffer size before trimming (1MB).</summary>
    private const int MaxBufferSize = 1_000_000;
    
    /// <summary>Size to retain when trimming buffer.</summary>
    private const int TrimmedBufferSize = 500_000;

    private readonly IHex1bTerminalPresentationAdapter _primaryAdapter;
    private readonly Channel<TerminalMessage> _outputChannel;
    private readonly Channel<ReadOnlyMemory<byte>> _inputChannel;
    private readonly StringBuilder _outputBuffer = new();
    private readonly object _bufferLock = new();
    private readonly object _pauseLock = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly List<ClientConnection> _clients = [];
    private readonly object _clientsLock = new();
    private int _width;
    private int _height;
    private bool _disposed;
    private bool _paused;

    /// <summary>
    /// Creates a new FollowingPresentationAdapter.
    /// </summary>
    /// <param name="primaryAdapter">The primary adapter to follow for resize events.</param>
    public FollowingPresentationAdapter(IHex1bTerminalPresentationAdapter primaryAdapter)
    {
        _primaryAdapter = primaryAdapter ?? throw new ArgumentNullException(nameof(primaryAdapter));
        _width = primaryAdapter.Width;
        _height = primaryAdapter.Height;

        // Subscribe to primary adapter's resize events
        _primaryAdapter.Resized += OnPrimaryResized;

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

    private void OnPrimaryResized(int width, int height)
    {
        if (_disposed) return;

        _width = width;
        _height = height;

        // Notify all connected clients of the resize
        lock (_clientsLock)
        {
            foreach (var client in _clients)
            {
                client.NotifyResize(width, height);
            }
        }

        // Also forward the resize event
        Resized?.Invoke(width, height);
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

        // Check if paused
        lock (_pauseLock)
        {
            if (_paused) return ValueTask.CompletedTask;
        }

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
    /// Registers a new client connection and returns a connection handle.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID.</param>
    /// <returns>A client connection for streaming data.</returns>
    public ClientConnection RegisterClient(string connectionId)
    {
        var client = new ClientConnection(connectionId, this);
        
        lock (_clientsLock)
        {
            _clients.Add(client);
        }

        return client;
    }

    /// <summary>
    /// Unregisters a client connection.
    /// </summary>
    /// <param name="client">The client to unregister.</param>
    public void UnregisterClient(ClientConnection client)
    {
        lock (_clientsLock)
        {
            _clients.Remove(client);
        }
    }

    /// <summary>
    /// Gets the current buffered terminal output for state sync on client connection.
    /// Briefly pauses I/O to ensure consistent state.
    /// </summary>
    /// <returns>The buffered output as a string.</returns>
    public string GetBufferedOutputWithPause()
    {
        // Briefly pause to get consistent state
        lock (_pauseLock)
        {
            _paused = true;
        }

        try
        {
            lock (_bufferLock)
            {
                return _outputBuffer.ToString();
            }
        }
        finally
        {
            lock (_pauseLock)
            {
                _paused = false;
            }
        }
    }

    /// <summary>
    /// Gets the current terminal dimensions.
    /// </summary>
    /// <returns>A tuple of (cols, rows).</returns>
    public (int Cols, int Rows) GetDimensions() => (_width, _height);

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

        // Stream updates
        await foreach (var message in _outputChannel.Reader.ReadAllAsync(linkedCts.Token))
        {
            yield return message;
        }
    }

    /// <summary>
    /// Sends input from a web client to the terminal.
    /// </summary>
    /// <param name="data">The input data as a string.</param>
    public void SendInput(string data)
    {
        if (_disposed) return;

        var bytes = Encoding.UTF8.GetBytes(data);
        _inputChannel.Writer.TryWrite(bytes);
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken ct = default)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask EnterRawModeAsync(CancellationToken ct = default)
    {
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

        _primaryAdapter.Resized -= OnPrimaryResized;
        Disconnected?.Invoke();

        _disposeCts.Cancel();
        _outputChannel.Writer.Complete();
        _inputChannel.Writer.Complete();
        _disposeCts.Dispose();

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Represents a connected web client.
/// </summary>
public class ClientConnection
{
    private readonly string _connectionId;
    private readonly FollowingPresentationAdapter _adapter;
    private readonly Channel<(int Width, int Height)> _resizeChannel;

    /// <summary>
    /// Creates a new client connection.
    /// </summary>
    public ClientConnection(string connectionId, FollowingPresentationAdapter adapter)
    {
        _connectionId = connectionId;
        _adapter = adapter;
        _resizeChannel = Channel.CreateBounded<(int, int)>(new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });
    }

    /// <summary>
    /// Gets the connection ID.
    /// </summary>
    public string ConnectionId => _connectionId;

    /// <summary>
    /// Notifies the client of a resize event.
    /// </summary>
    public void NotifyResize(int width, int height)
    {
        _resizeChannel.Writer.TryWrite((width, height));
    }

    /// <summary>
    /// Gets an async enumerable of resize events for this client.
    /// </summary>
    public async IAsyncEnumerable<(int Width, int Height)> GetResizeEventsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var resize in _resizeChannel.Reader.ReadAllAsync(ct))
        {
            yield return resize;
        }
    }
}
