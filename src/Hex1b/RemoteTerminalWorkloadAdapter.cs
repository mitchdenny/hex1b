using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Hex1b;

/// <summary>
/// A workload adapter that connects to a remote terminal host over WebSocket.
/// The remote host exposes itself via <c>WebSocketDiagnosticsListener</c> at a
/// <c>/ws/attach</c> endpoint. This adapter bridges the WebSocket I/O to the
/// <see cref="IHex1bTerminalWorkloadAdapter"/> contract so a local
/// <see cref="Hex1bTerminal"/> can display and interact with the remote terminal.
/// </summary>
/// <remarks>
/// <para>
/// Use this adapter via the builder API:
/// <code>
/// await using var terminal = Hex1bTerminal.CreateBuilder()
///     .WithRemoteTerminal(new Uri("ws://localhost:8080/ws/attach"))
///     .Build();
///
/// await terminal.RunAsync();
/// </code>
/// </para>
/// <para>
/// Protocol: binary WebSocket frames carry raw terminal I/O bytes.
/// JSON text frames carry control messages (resize, leader, exit).
/// </para>
/// </remarks>
public sealed class RemoteTerminalWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    private readonly Uri _uri;
    private readonly ClientWebSocket _ws = new();
    private readonly Channel<ReadOnlyMemory<byte>> _outputChannel;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private bool _disposed;

    /// <summary>
    /// Creates a new remote terminal workload adapter.
    /// </summary>
    /// <param name="uri">WebSocket URI to connect to (e.g. <c>ws://localhost:8080/ws/attach</c>).</param>
    public RemoteTerminalWorkloadAdapter(Uri uri)
    {
        _uri = uri ?? throw new ArgumentNullException(nameof(uri));
        _outputChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true
            });
    }

    /// <summary>
    /// Gets the remote terminal width reported during the handshake.
    /// </summary>
    public int RemoteWidth { get; private set; }

    /// <summary>
    /// Gets the remote terminal height reported during the handshake.
    /// </summary>
    public int RemoteHeight { get; private set; }

    /// <summary>
    /// Gets whether this client is the resize leader.
    /// </summary>
    public bool IsLeader { get; private set; }

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <summary>
    /// Connects to the remote terminal host, performs the handshake,
    /// and starts the background receive pump.
    /// </summary>
    internal async Task ConnectAsync(CancellationToken ct)
    {
        await _ws.ConnectAsync(_uri, ct);

        // Read the initial handshake frame
        var handshake = await ReceiveFullMessageAsync(ct);
        if (handshake is not { Type: WebSocketMessageType.Text } msg)
            throw new InvalidOperationException("Expected text frame for WebSocket attach handshake");

        var json = Encoding.UTF8.GetString(msg.Data);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var type = root.GetProperty("type").GetString();
        if (type != "attached")
            throw new InvalidOperationException($"Unexpected handshake type: {type}");

        RemoteWidth = root.GetProperty("width").GetInt32();
        RemoteHeight = root.GetProperty("height").GetInt32();
        IsLeader = root.GetProperty("leader").GetBoolean();

        // Queue the initial screen snapshot so the terminal displays it immediately
        if (root.TryGetProperty("data", out var dataProp) && dataProp.GetString() is { Length: > 0 } initialData)
        {
            var bytes = Encoding.UTF8.GetBytes(initialData);
            _outputChannel.Writer.TryWrite(bytes);
        }

        // Claim leadership so our resize events control the remote terminal
        await ClaimLeadAsync(ct);

        // Start the background receive pump
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _receiveTask = Task.Run(() => ReceivePumpAsync(_receiveCts.Token), _receiveCts.Token);
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        if (_disposed)
            return new ValueTask<ReadOnlyMemory<byte>>(ReadOnlyMemory<byte>.Empty);

        return ReadOutputCoreAsync(ct);
    }

    private async ValueTask<ReadOnlyMemory<byte>> ReadOutputCoreAsync(CancellationToken ct)
    {
        try
        {
            if (await _outputChannel.Reader.WaitToReadAsync(ct))
            {
                if (_outputChannel.Reader.TryRead(out var data))
                    return data;
            }
        }
        catch (OperationCanceledException) { }
        catch (ChannelClosedException) { }

        return ReadOnlyMemory<byte>.Empty;
    }

    /// <inheritdoc />
    public async ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed || _ws.State != WebSocketState.Open) return;

        try
        {
            await _ws.SendAsync(data, WebSocketMessageType.Binary, true, ct);
        }
        catch (WebSocketException) { }
        catch (ObjectDisposedException) { }
    }

    /// <inheritdoc />
    public async ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        if (_disposed || _ws.State != WebSocketState.Open) return;

        try
        {
            var json = JsonSerializer.Serialize(new { type = "resize", cols = width, rows = height });
            await _ws.SendAsync(
                Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
        }
        catch (WebSocketException) { }
        catch (ObjectDisposedException) { }
    }

    /// <summary>
    /// Sends a lead claim to the remote host so this adapter controls resize.
    /// </summary>
    private async Task ClaimLeadAsync(CancellationToken ct)
    {
        await _ws.SendAsync(
            """{"type":"lead"}"""u8.ToArray(), WebSocketMessageType.Text, true, ct);
    }

    /// <summary>
    /// Requests the remote host to shut down.
    /// </summary>
    internal async Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_ws.State != WebSocketState.Open) return;

        try
        {
            await _ws.SendAsync(
                """{"type":"shutdown"}"""u8.ToArray(), WebSocketMessageType.Text, true, ct);
        }
        catch { }
    }

    /// <summary>
    /// Background pump that receives WebSocket frames and routes them.
    /// Binary frames → output channel. JSON text frames → control handling.
    /// </summary>
    private async Task ReceivePumpAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                var message = await ReceiveFullMessageAsync(ct);
                if (message is null)
                {
                    // Server sent a close frame — ack it
                    if (_ws.State == WebSocketState.CloseReceived)
                    {
                        try
                        {
                            await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                        }
                        catch { }
                    }
                    break;
                }

                if (message.Value.Type == WebSocketMessageType.Binary)
                {
                    // Raw terminal output → feed to output channel
                    _outputChannel.Writer.TryWrite(message.Value.Data);
                }
                else if (message.Value.Type == WebSocketMessageType.Text)
                {
                    if (HandleControlFrame(message.Value.Data))
                        break; // exit frame — stop the pump
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            _outputChannel.Writer.TryComplete();
            Disconnected?.Invoke();
        }
    }

    /// <summary>
    /// Handles a JSON control frame from the server.
    /// Returns <c>true</c> if the pump should stop (exit frame).
    /// </summary>
    private bool HandleControlFrame(byte[] data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            var type = doc.RootElement.GetProperty("type").GetString();

            switch (type)
            {
                case "leader":
                    IsLeader = doc.RootElement.GetProperty("isLeader").GetBoolean();
                    return false;

                case "resize":
                    var cols = doc.RootElement.GetProperty("cols").GetInt32();
                    var rows = doc.RootElement.GetProperty("rows").GetInt32();
                    RemoteWidth = cols;
                    RemoteHeight = rows;
                    return false;

                case "exit":
                    return true;
            }
        }
        catch (JsonException) { }

        return false;
    }

    /// <summary>
    /// Receives a complete WebSocket message, assembling multi-fragment messages.
    /// Returns null on close.
    /// </summary>
    private async Task<(WebSocketMessageType Type, byte[] Data)?> ReceiveFullMessageAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();

        WebSocketReceiveResult result;
        do
        {
            result = await _ws.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return (result.MessageType, ms.ToArray());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Stop the receive pump
        if (_receiveCts != null)
        {
            await _receiveCts.CancelAsync();
            try
            {
                if (_receiveTask != null)
                    await _receiveTask;
            }
            catch (OperationCanceledException) { }
        }
        _receiveCts?.Dispose();

        // Close the WebSocket gracefully (one-way close, don't wait for server ack)
        if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived)
        {
            try
            {
                await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
            catch { }
        }

        _ws.Dispose();
        _outputChannel.Writer.TryComplete();
    }
}
