using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Hex1b.Reflow;
using Microsoft.Extensions.Logging.Abstractions;
using WebTerminalDemo;

namespace Hex1b.Tests;

[TestClass]
public class WebTerminalDemoLifecycleTests
{
    [TestMethod]
    [DataRow(DemoReflowStrategy.None, null)]
    [DataRow(DemoReflowStrategy.Auto, typeof(AutoReflowStrategy))]
    [DataRow(DemoReflowStrategy.Alacritty, typeof(AlacrittyReflowStrategy))]
    [DataRow(DemoReflowStrategy.Foot, typeof(FootReflowStrategy))]
    [DataRow(DemoReflowStrategy.Ghostty, typeof(GhosttyReflowStrategy))]
    [DataRow(DemoReflowStrategy.ITerm2, typeof(ITerm2ReflowStrategy))]
    [DataRow(DemoReflowStrategy.Kitty, typeof(KittyReflowStrategy))]
    [DataRow(DemoReflowStrategy.Vte, typeof(VteReflowStrategy))]
    [DataRow(DemoReflowStrategy.WezTerm, typeof(WezTermReflowStrategy))]
    [DataRow(DemoReflowStrategy.WindowsTerminal, typeof(WindowsTerminalReflowStrategy))]
    [DataRow(DemoReflowStrategy.Xterm, typeof(XtermReflowStrategy))]
    public void CreateRequest_ExplicitReflow_SelectsNamedProvider(object value, Type? providerType)
    {
        var strategy = (DemoReflowStrategy)value;
        var request = new CreateTerminalRequest(ReflowStrategy: strategy);
        Assert.AreEqual(providerType, request.GetReflowProvider()?.GetType());
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var restored = JsonSerializer.Deserialize<CreateTerminalRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.AreEqual(strategy, restored.ReflowStrategy);
    }

    [TestMethod]
    [DataRow("shell", DemoReflowStrategy.Default, DemoReflowStrategy.Ghostty, true)]
    [DataRow("activity", DemoReflowStrategy.Default, DemoReflowStrategy.None, false)]
    [DataRow("shell", DemoReflowStrategy.None, DemoReflowStrategy.None, false)]
    [DataRow("activity", DemoReflowStrategy.Ghostty, DemoReflowStrategy.Ghostty, true)]
    [DataRow("activity", DemoReflowStrategy.Vte, DemoReflowStrategy.Vte, true)]
    public async Task Create_ReflowPolicy_IsAppliedToProducerAndReported(
        string scene, object selected, object resolved, bool enabled)
    {
        await using var registry = CreateRegistry();
        var info = registry.Create(new(scene, 40, 12, ReflowStrategy: (DemoReflowStrategy)selected))!;
        using var first = registry.TryOpenView(info.Id).View!;
        using var second = registry.TryOpenView(info.Id).View!;
        Assert.AreEqual((DemoReflowStrategy)resolved, info.ReflowStrategy);
        Assert.AreEqual((DemoReflowStrategy)resolved, TestSeq.Single(registry.List()).ReflowStrategy);
        Assert.AreSame(first.Instance.Presentation, second.Instance.Presentation);
        Assert.AreEqual(enabled, first.Instance.Presentation.ReflowEnabled);
    }

    [TestMethod]
    public void CreateRequest_OmittedReflow_KeepsSceneDefault()
    {
        var request = JsonSerializer.Deserialize<CreateTerminalRequest>("""{"scene":"shell"}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.AreEqual(DemoReflowStrategy.Default, request.ReflowStrategy);
        Assert.AreSame(GhosttyReflowStrategy.Instance, request.GetReflowProvider());
    }

    [TestMethod]
    public void CreateRequest_UnknownReflow_IsRejected()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateTerminalRequest>(
            """{"reflowStrategy":"unknown"}""", new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CreateTerminalRequest(ReflowStrategy: (DemoReflowStrategy)999).GetReflowProvider());
    }

    [TestMethod]
    [DataRow("close", 1000, "Demo: graceful view closure", false)]
    [DataRow("close", 1000, "Demo: graceful view closure", true)]
    [DataRow("policy", 1008, "Demo: policy violation", false)]
    [DataRow("policy", 1008, "Demo: policy violation", true)]
    [DataRow("server-error", 1011, "Demo: server failure", false)]
    [DataRow("server-error", 1011, "Demo: server failure", true)]
    [DataRow("abort", 1006, "", false)]
    [DataRow("abort", 1006, "", true)]
    public async Task RequestFailure_ConnectedView_ClosesOnlyThatView(string mode, int code, string reason, bool relay)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = timeout.Token;
        await using var registry = CreateRegistry();
        var instance = registry.Create(new("activity", 40, 12))!;
        await using var observer = await Connection.CreateAsync(registry, instance.Id, false, ct);
        await ReadFrameAsync(observer.Client, ct);
        await using var target = await Connection.CreateAsync(registry, instance.Id, relay, ct);
        await ReadFrameAsync(target.Client, ct);
        var failure = BrowserCloseRequest.ForFailure(mode)!;

        Assert.AreEqual(202, registry.RequestViewFailure(instance.Id, target.View.Id, failure));
        Assert.AreEqual(409, target.View.RequestFailure(failure));
        if (mode == "abort")
            await Assert.ThrowsAsync<WebSocketException>(() => ReadCloseAsync(target.Client, ct));
        else
        {
            var closed = await ReadCloseAsync(target.Client, ct);
            Assert.AreEqual(code, (int)closed.CloseStatus!);
            Assert.AreEqual(reason, closed.CloseStatusDescription);
        }
        await target.Completion.WaitAsync(ct);

        Assert.AreEqual(1, TestSeq.Single(registry.List()).PeerCount);
        Assert.IsFalse(observer.View.Instance.IsStopping);
        Assert.AreEqual(404, registry.RequestViewFailure(instance.Id, target.View.Id, failure));
        await observer.Client.SendAsync("""{"type":"resync"}"""u8.ToArray(), WebSocketMessageType.Text, true, ct);
        await ReadFrameAsync(observer.Client, ct);
        Assert.AreEqual(WebSocketState.Open, observer.Client.State);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task InitialFailure_BeforeFirstFrame_NeverCreatesPresentationPeer(bool abort, bool relay)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = timeout.Token;
        await using var registry = CreateRegistry();
        var instance = registry.Create(new("activity", 40, 12))!;
        await using var target = await Connection.CreateAsync(registry, instance.Id, relay, ct,
            abort ? InitialViewFailure.BeforeFrameAbort : InitialViewFailure.BeforeFrameClose);

        if (abort)
            await Assert.ThrowsAsync<WebSocketException>(() =>
                target.Client.ReceiveAsync(new byte[4096], ct));
        else
        {
            var closed = await target.Client.ReceiveAsync(new byte[4096], ct);
            Assert.AreEqual(WebSocketMessageType.Close, closed.MessageType, "No HWT1 frame should precede closure.");
            Assert.AreEqual(WebSocketCloseStatus.NormalClosure, closed.CloseStatus);
            Assert.AreEqual("Demo: closed before first frame", closed.CloseStatusDescription);
            await target.Client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", ct);
        }
        await target.Completion.WaitAsync(ct);
        Assert.AreEqual(0, TestSeq.Single(registry.List()).PeerCount);
        Assert.IsFalse(target.View.Instance.IsStopping);
        using var reopened = registry.TryOpenView(instance.Id, target.View.Id).View;
        Assert.IsNotNull(reopened, "The view reservation must be released even without a presentation.");
    }

    [TestMethod]
    public async Task DeleteAsync_WithTwoViews_PublishesOwnerReasonBeforeStoppingAndClosesBoth()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = timeout.Token;
        await using var registry = CreateRegistry();
        var instance = registry.Create(new("activity", 40, 12))!;
        await using var direct = await Connection.CreateAsync(registry, instance.Id, false, ct);
        await using var relay = await Connection.CreateAsync(registry, instance.Id, true, ct);
        await ReadFrameAsync(direct.Client, ct);
        await ReadFrameAsync(relay.Client, ct);
        BrowserCloseRequest? observed = null;
        using var registration = direct.View.Instance.Stopping.Register(() =>
            observed = direct.View.Instance.CloseReason);

        var stop = registry.DeleteAsync(instance.Id);
        var closed = await Task.WhenAll(ReadCloseAsync(direct.Client, ct), ReadCloseAsync(relay.Client, ct));
        foreach (var result in closed)
        {
            Assert.AreEqual(4000, (int)result.CloseStatus!);
            Assert.AreEqual("Terminal stopped by owner", result.CloseStatusDescription);
        }
        Assert.AreEqual(BrowserCloseRequest.OwnerStopped, observed);
        Assert.IsTrue(await stop.WaitAsync(ct));
        await Task.WhenAll(direct.Completion, relay.Completion).WaitAsync(ct);
        Assert.IsEmpty(registry.List());
    }

    [TestMethod]
    public async Task WorkloadExit_ShellExitsNaturally_PublishesExitCodeToBothViews()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = timeout.Token;
        await using var registry = CreateRegistry();
        var instance = registry.Create(new("shell", 40, 12))!;
        await using var direct = await Connection.CreateAsync(registry, instance.Id, false, ct);
        await using var relay = await Connection.CreateAsync(registry, instance.Id, true, ct);
        await ReadFrameAsync(direct.Client, ct);
        await ReadFrameAsync(relay.Client, ct);
        BrowserCloseRequest? observed = null;
        using var registration = direct.View.Instance.Stopping.Register(() =>
            observed = direct.View.Instance.CloseReason);

        await direct.Client.SendAsync("""{"type":"input","text":"exit 7\r"}"""u8.ToArray(),
            WebSocketMessageType.Text, true, ct);
        var closed = await Task.WhenAll(ReadCloseAsync(direct.Client, ct), ReadCloseAsync(relay.Client, ct));
        foreach (var result in closed)
        {
            Assert.AreEqual(4000, (int)result.CloseStatus!);
            Assert.AreEqual("Workload exited with code 7", result.CloseStatusDescription);
        }
        Assert.AreEqual("Workload exited with code 7", observed?.Reason);
        await direct.View.Instance.StopAsync().WaitAsync(ct);
        Assert.IsEmpty(registry.List());
    }

    [TestMethod]
    public async Task ApplicationStopping_WithConnectedView_SendsGoingAwayInsteadOfOwnerStop()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var stopping = new CancellationTokenSource();
        var ct = timeout.Token;
        await using var registry = CreateRegistry(stopping.Token);
        var instance = registry.Create(new("activity", 40, 12))!;
        await using var target = await Connection.CreateAsync(registry, instance.Id, true, ct);
        await ReadFrameAsync(target.Client, ct);

        await stopping.CancelAsync();
        var closed = await ReadCloseAsync(target.Client, ct);
        Assert.AreEqual(WebSocketCloseStatus.EndpointUnavailable, closed.CloseStatus);
        Assert.AreEqual("Application shutting down", closed.CloseStatusDescription);
        await target.View.Instance.StopAsync().WaitAsync(ct);
        Assert.IsEmpty(registry.List());
    }

    [TestMethod]
    public async Task TryOpenView_DuplicateAndCapacityFailures_DoNotStealOrLeakReservations()
    {
        await using var registry = CreateRegistry();
        var instance = registry.Create(new("activity", 40, 12))!;
        var id = Guid.NewGuid().ToString("D");
        using var first = registry.TryOpenView(instance.Id, id).View!;
        var duplicate = registry.TryOpenView(instance.Id, id.ToUpperInvariant());
        Assert.IsNull(duplicate.View);
        Assert.AreEqual(409, duplicate.Status);
        var views = new List<TerminalView>();
        try
        {
            for (var index = 0; index < 7; index++)
            {
                var next = registry.TryOpenView(instance.Id);
                Assert.AreEqual(200, next.Status);
                views.Add(next.View!);
                Assert.IsTrue(Guid.TryParseExact(next.View!.Id, "D", out _));
            }
            Assert.AreEqual(429, registry.TryOpenView(instance.Id).Status);
            Assert.AreEqual(404, registry.TryOpenView("missing").Status);
            Assert.AreEqual(404, registry.RequestViewFailure(instance.Id, Guid.NewGuid().ToString("D"),
                BrowserCloseRequest.ForFailure("close")!));
            Assert.IsFalse(first.Failure.IsCompleted);
            first.Dispose();
            using var replacement = registry.TryOpenView(instance.Id, id).View;
            Assert.IsNotNull(replacement);
        }
        finally
        {
            foreach (var view in views)
                view.Dispose();
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("normal")]
    [DataRow("CLOSE")]
    public void ForFailure_InvalidMode_IsRejected(string? mode)
    {
        Assert.IsNull(BrowserCloseRequest.ForFailure(mode));
    }

    [TestMethod]
    public async Task RunAsync_InvalidInput_StillSendsPolicyCloseWithARealReceiver()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = timeout.Token;
        await using var registry = CreateRegistry();
        var instance = registry.Create(new("activity", 40, 12))!;
        await using var target = await Connection.CreateAsync(registry, instance.Id, false, ct);
        await ReadFrameAsync(target.Client, ct);
        await target.Client.SendAsync("{"u8.ToArray(), WebSocketMessageType.Text, true, ct);

        var closed = await ReadCloseAsync(target.Client, ct);
        Assert.AreEqual(WebSocketCloseStatus.PolicyViolation, closed.CloseStatus);
        Assert.Contains("Invalid input", closed.CloseStatusDescription!);
        await target.Completion.WaitAsync(ct);
        Assert.AreEqual(0, TestSeq.Single(registry.List()).PeerCount);
    }

    [TestMethod]
    public async Task RunAsync_PeerDoesNotAnswerClose_BoundsCleanupAndReleasesView()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = timeout.Token;
        await using var registry = CreateRegistry();
        var instance = registry.Create(new("activity", 40, 12))!;
        await using var target = await Connection.CreateAsync(registry, instance.Id, true, ct);
        await ReadFrameAsync(target.Client, ct);

        Assert.AreEqual(202, registry.RequestViewFailure(instance.Id, target.View.Id,
            BrowserCloseRequest.ForFailure("close")!));
        // Deliberately neither receive nor acknowledge the server's close frame.
        await target.Completion.WaitAsync(ct);
        Assert.AreEqual(WebSocketState.Aborted, target.Server.State);
        Assert.AreEqual(0, TestSeq.Single(registry.List()).PeerCount);
    }

    [TestMethod]
    public async Task RunAsync_RequestAborted_JoinsTransportAndReleasesPeer()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var request = new CancellationTokenSource();
        var ct = timeout.Token;
        await using var registry = CreateRegistry();
        var instance = registry.Create(new("activity", 40, 12))!;
        await using var target = await Connection.CreateAsync(registry, instance.Id, true, request.Token);
        await ReadFrameAsync(target.Client, ct);

        await request.CancelAsync();
        await target.Completion.WaitAsync(ct);
        Assert.AreEqual(0, TestSeq.Single(registry.List()).PeerCount);
        Assert.IsFalse(target.View.Instance.IsStopping);
    }

    [TestMethod]
    public async Task RunAsync_BlockedSocketWrite_BoundsCleanupWithoutConcurrentCloseWrite()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = timeout.Token;
        await using var registry = CreateRegistry();
        var instance = registry.Create(new("activity", 40, 12))!;
        await using var target = await Connection.CreateAsync(registry, instance.Id, false, ct, blockSends: true);
        var gated = (GatedWebSocket)target.Server;
        await gated.SendStarted.Task.WaitAsync(ct);

        Assert.AreEqual(202, registry.RequestViewFailure(instance.Id, target.View.Id,
            BrowserCloseRequest.ForFailure("close")!));
        await target.Completion.WaitAsync(ct);
        Assert.IsFalse(gated.CloseOverlappedSend);
        Assert.AreEqual(WebSocketState.Aborted, gated.State);
        Assert.IsTrue(gated.SendFinished);
        Assert.AreEqual(0, TestSeq.Single(registry.List()).PeerCount);
    }

    [TestMethod]
    public async Task RunAsync_InFlightSocketWrite_CompletesBeforeGracefulClose()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = timeout.Token;
        await using var registry = CreateRegistry();
        var instance = registry.Create(new("activity", 40, 12))!;
        await using var target = await Connection.CreateAsync(registry, instance.Id, false, ct, blockSends: true);
        var gated = (GatedWebSocket)target.Server;
        await gated.SendStarted.Task.WaitAsync(ct);

        Assert.AreEqual(202, registry.RequestViewFailure(instance.Id, target.View.Id,
            BrowserCloseRequest.ForFailure("close")!));
        gated.ReleaseSend.TrySetResult();
        var result = await ReadCloseAsync(target.Client, ct);
        Assert.AreEqual(WebSocketCloseStatus.NormalClosure, result.CloseStatus);
        Assert.AreEqual("Demo: graceful view closure", result.CloseStatusDescription);
        await target.Completion.WaitAsync(ct);
        Assert.IsFalse(gated.CloseOverlappedSend);
        Assert.IsTrue(gated.SendFinished);
    }

    private static TerminalRegistry CreateRegistry(CancellationToken applicationStopping = default)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var contentRoot = Path.Combine(directory.FullName, "samples", "WebTerminalDemo");
            if (Directory.Exists(Path.Combine(contentRoot, "Tapes")))
                return new(new DemoTapeCatalog(contentRoot), NullLogger.Instance, applicationStopping);
        }
        throw new DirectoryNotFoundException("Could not find the WebTerminalDemo tapes.");
    }

    private static async Task ReadFrameAsync(WebSocket socket, CancellationToken ct)
    {
        using var message = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, ct);
            Assert.AreEqual(WebSocketMessageType.Binary, result.MessageType);
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        var frame = message.ToArray();
        var length = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(4));
        using var document = JsonDocument.Parse(frame.AsMemory(8, length));
        var ack = Encoding.UTF8.GetBytes(
            $$"""{"type":"ack","revision":{{document.RootElement.GetProperty("revision").GetUInt32()}}}""");
        await socket.SendAsync(ack, WebSocketMessageType.Text, true, ct);
    }

    private static async Task<WebSocketReceiveResult> ReadCloseAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[4096];
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType != WebSocketMessageType.Close)
                continue;
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", ct);
            return result;
        }
    }

    private sealed class Connection(TerminalView view, WebSocket client, WebSocket server,
        CancellationToken ct, bool relay, InitialViewFailure initialFailure) : IAsyncDisposable
    {
        public TerminalView View { get; } = view;
        public WebSocket Client { get; } = client;
        public WebSocket Server { get; } = server;
        public Task Completion { get; } = RunAsync(view, server, ct, relay, initialFailure);

        public static async Task<Connection> CreateAsync(TerminalRegistry registry, string instanceId,
            bool relay, CancellationToken ct, InitialViewFailure failure = InitialViewFailure.None, bool blockSends = false)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var connecting = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket? accepted = null;
            WebSocket? client = null, server = null;
            try
            {
                await connecting.ConnectAsync(IPAddress.Loopback, port, ct);
                accepted = await listener.AcceptSocketAsync(ct);
                client = WebSocket.CreateFromStream(new NetworkStream(connecting, ownsSocket: true), false, null, Timeout.InfiniteTimeSpan);
                server = WebSocket.CreateFromStream(new NetworkStream(accepted, ownsSocket: true), true, null, Timeout.InfiniteTimeSpan);
                if (blockSends)
                    server = new GatedWebSocket(server);
                var view = registry.TryOpenView(instanceId).View!;
                return new(view, client, server, ct, relay, failure);
            }
            catch
            {
                client?.Dispose();
                server?.Dispose();
                connecting.Dispose();
                accepted?.Dispose();
                throw;
            }
        }

        private static async Task RunAsync(TerminalView view, WebSocket server, CancellationToken ct,
            bool relay, InitialViewFailure initialFailure)
        {
            using (view)
                await new BrowserSession(server, view, "test", relay, NullLogger.Instance, initialFailure).RunAsync(ct);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Abort();
            Server.Abort();
            await Completion.WaitAsync(TimeSpan.FromSeconds(10));
            Client.Dispose();
            Server.Dispose();
        }
    }

    private sealed class GatedWebSocket(WebSocket inner) : WebSocket
    {
        private bool _sending;
        public TaskCompletionSource SendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSend { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CloseOverlappedSend { get; private set; }
        public bool SendFinished { get; private set; }
        public override WebSocketCloseStatus? CloseStatus => inner.CloseStatus;
        public override string? CloseStatusDescription => inner.CloseStatusDescription;
        public override WebSocketState State => inner.State;
        public override string? SubProtocol => inner.SubProtocol;
        public override void Abort() => inner.Abort();
        public override void Dispose() => inner.Dispose();
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
            inner.CloseAsync(closeStatus, statusDescription, cancellationToken);
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            CloseOverlappedSend |= _sending;
            return inner.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
        }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
            inner.ReceiveAsync(buffer, cancellationToken);
        public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage,
            CancellationToken cancellationToken)
        {
            _sending = true;
            SendStarted.TrySetResult();
            try
            {
                await ReleaseSend.Task.WaitAsync(cancellationToken);
                await inner.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
            }
            finally
            {
                _sending = false;
                SendFinished = true;
            }
        }
    }
}
