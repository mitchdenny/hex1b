using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Hex1b;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Hex1b.Tests;

public class RemoteTerminalWorkloadAdapterTests : IAsyncLifetime
{
    private WebApplication? _server;
    private int _port;
    private readonly List<WebSocket> _connectedClients = new();
    private readonly SemaphoreSlim _clientConnected = new(0);
    // Signal so the server handler stays alive until test cleanup
    private readonly TaskCompletionSource _serverDone = new();

    public async ValueTask InitializeAsync()
    {
        _port = Random.Shared.Next(19000, 19999);
        _server = await StartMockServerAsync(_port);
    }

    public async ValueTask DisposeAsync()
    {
        // Signal server handlers to exit
        _serverDone.TrySetResult();

        foreach (var ws in _connectedClients)
        {
            try { ws.Dispose(); } catch { }
        }

        if (_server != null)
        {
            await _server.StopAsync();
            await _server.DisposeAsync();
        }
    }

    private Uri WsUri => new($"ws://localhost:{_port}/ws/attach");

    [Fact]
    public async Task Constructor_WithValidUri_CreatesAdapter()
    {
        await using var adapter = new RemoteTerminalWorkloadAdapter(new Uri("ws://localhost:9999/ws/attach"));
        Assert.NotNull(adapter);
    }

    [Fact]
    public void Constructor_WithNullUri_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new RemoteTerminalWorkloadAdapter(null!));
    }

    [Fact]
    public async Task ConnectAsync_ReceivesHandshake_SetsRemoteDimensions()
    {
        await using var adapter = new RemoteTerminalWorkloadAdapter(WsUri);
        await adapter.ConnectAsync(CancellationToken.None);

        Assert.Equal(120, adapter.RemoteWidth);
        Assert.Equal(30, adapter.RemoteHeight);
    }

    [Fact]
    public async Task ConnectAsync_ClaimsLeadership()
    {
        await using var adapter = new RemoteTerminalWorkloadAdapter(WsUri);
        await adapter.ConnectAsync(CancellationToken.None);

        // Wait for client to connect on server side
        await _clientConnected.WaitAsync(TimeSpan.FromSeconds(5));
        var serverWs = _connectedClients[0];

        // The adapter sends {"type":"lead"} after handshake
        var msg = await ReceiveTextAsync(serverWs);
        using var doc = JsonDocument.Parse(msg);
        Assert.Equal("lead", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task ReadOutputAsync_ReceivesBinaryFrames_ReturnsData()
    {
        await using var adapter = new RemoteTerminalWorkloadAdapter(WsUri);
        await adapter.ConnectAsync(CancellationToken.None);

        await _clientConnected.WaitAsync(TimeSpan.FromSeconds(5));
        var serverWs = _connectedClients[0];

        // Drain the lead claim
        await ReceiveTextAsync(serverWs);

        // Drain the initial screen data ("Welcome" from handshake)
        using var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await adapter.ReadOutputAsync(drainCts.Token);

        // Send binary output from server
        var outputData = Encoding.UTF8.GetBytes("Hello from remote!");
        await serverWs.SendAsync(outputData, WebSocketMessageType.Binary, true, CancellationToken.None);

        // Read from adapter
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = await adapter.ReadOutputAsync(cts.Token);
        Assert.Equal("Hello from remote!", Encoding.UTF8.GetString(received.Span));
    }

    [Fact]
    public async Task WriteInputAsync_SendsBinaryFrameToServer()
    {
        await using var adapter = new RemoteTerminalWorkloadAdapter(WsUri);
        await adapter.ConnectAsync(CancellationToken.None);

        await _clientConnected.WaitAsync(TimeSpan.FromSeconds(5));
        var serverWs = _connectedClients[0];

        // Drain the lead claim
        await ReceiveTextAsync(serverWs);

        // Send input from adapter
        var inputData = Encoding.UTF8.GetBytes("ls -la\n");
        await adapter.WriteInputAsync(inputData);

        // Verify server received it as binary
        var buffer = new byte[4096];
        var result = await serverWs.ReceiveAsync(buffer, CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Binary, result.MessageType);
        Assert.Equal("ls -la\n", Encoding.UTF8.GetString(buffer, 0, result.Count));
    }

    [Fact]
    public async Task ResizeAsync_SendsJsonResizeFrame()
    {
        await using var adapter = new RemoteTerminalWorkloadAdapter(WsUri);
        await adapter.ConnectAsync(CancellationToken.None);

        await _clientConnected.WaitAsync(TimeSpan.FromSeconds(5));
        var serverWs = _connectedClients[0];

        // Drain the lead claim
        await ReceiveTextAsync(serverWs);

        // Send resize
        await adapter.ResizeAsync(200, 50);

        // Verify server received JSON resize
        var msg = await ReceiveTextAsync(serverWs);
        using var doc = JsonDocument.Parse(msg);
        Assert.Equal("resize", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(200, doc.RootElement.GetProperty("cols").GetInt32());
        Assert.Equal(50, doc.RootElement.GetProperty("rows").GetInt32());
    }

    [Fact]
    public async Task Disconnected_FiredWhenServerCloses()
    {
        await using var adapter = new RemoteTerminalWorkloadAdapter(WsUri);
        await adapter.ConnectAsync(CancellationToken.None);

        await _clientConnected.WaitAsync(TimeSpan.FromSeconds(5));
        var serverWs = _connectedClients[0];

        var disconnectedFired = new TaskCompletionSource();
        adapter.Disconnected += () => disconnectedFired.TrySetResult();

        // Close from server side
        await serverWs.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);

        // Wait for Disconnected event
        await Task.WhenAny(disconnectedFired.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(disconnectedFired.Task.IsCompletedSuccessfully, "Disconnected event should fire when server closes");
    }

    [Fact]
    public async Task ReadOutputAsync_ReturnsInitialScreenData()
    {
        await using var adapter = new RemoteTerminalWorkloadAdapter(WsUri);
        await adapter.ConnectAsync(CancellationToken.None);

        // The mock server sends initial data "Welcome" in the handshake
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var first = await adapter.ReadOutputAsync(cts.Token);
        Assert.Equal("Welcome", Encoding.UTF8.GetString(first.Span));
    }

    [Fact]
    public async Task ExitFrame_TriggersDisconnect()
    {
        await using var adapter = new RemoteTerminalWorkloadAdapter(WsUri);
        await adapter.ConnectAsync(CancellationToken.None);

        await _clientConnected.WaitAsync(TimeSpan.FromSeconds(5));
        var serverWs = _connectedClients[0];

        // Drain the lead claim
        await ReceiveTextAsync(serverWs);

        var disconnectedFired = new TaskCompletionSource();
        adapter.Disconnected += () => disconnectedFired.TrySetResult();

        // Send exit control frame
        var exitJson = """{"type":"exit"}""";
        await serverWs.SendAsync(
            Encoding.UTF8.GetBytes(exitJson), WebSocketMessageType.Text, true, CancellationToken.None);

        await Task.WhenAny(disconnectedFired.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(disconnectedFired.Task.IsCompletedSuccessfully, "Exit frame should trigger Disconnected");
    }

    // --- Mock WebSocket Server ---

    private async Task<WebApplication> StartMockServerAsync(int port)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, port);
        });
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.UseWebSockets();

        app.Map("/ws/attach", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            // Do NOT use 'using' — we hold the WebSocket open for the test.
            // Disposal happens in DisposeAsync.
            var ws = await context.WebSockets.AcceptWebSocketAsync();
            _connectedClients.Add(ws);

            // Send handshake
            var handshake = JsonSerializer.Serialize(new
            {
                type = "attached",
                width = 120,
                height = 30,
                leader = true,
                data = "Welcome"
            });
            await ws.SendAsync(
                Encoding.UTF8.GetBytes(handshake), WebSocketMessageType.Text, true, context.RequestAborted);

            _clientConnected.Release();

            // Keep the handler alive — do NOT call ReceiveAsync here,
            // because the test code reads from this same WebSocket.
            try
            {
                await _serverDone.Task.WaitAsync(context.RequestAborted);
            }
            catch (OperationCanceledException) { }
        });

        await app.StartAsync();
        return app;
    }

    private static async Task<string> ReceiveTextAsync(WebSocket ws, CancellationToken ct = default)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(buffer, ct);
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
