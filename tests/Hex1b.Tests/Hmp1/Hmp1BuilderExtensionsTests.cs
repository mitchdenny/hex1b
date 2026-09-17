using System.Threading.Channels;
using WebTerminalDemo;

namespace Hex1b.Tests.Hmp1;

[TestClass]
public class Hmp1BuilderExtensionsTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task WithHmp1Server_WithDimensions_UsesFinalSizeBeforeCreatingWorkload(bool dimensionsFirst)
    {
        var streams = Channel.CreateUnbounded<Stream>();
        using var workload = new Hex1bAppWorkloadAdapter();
        var builder = Hex1bTerminal.CreateBuilder();
        if (dimensionsFirst)
            builder.WithDimensions(160, 48);
        builder.WithHmp1Server(streams.Reader.ReadAllAsync);
        if (!dimensionsFirst)
            builder.WithDimensions(160, 48);

        var factoryCalled = false;
        builder.SetWorkloadFactory(presentation =>
        {
            var adapter = TestSeq.IsType<Hmp1PresentationAdapter>(presentation);
            Assert.AreEqual(160, adapter.Width);
            Assert.AreEqual(48, adapter.Height);
            factoryCalled = true;
            return new Hex1bTerminalBuildContext(workload, null);
        });

        await using var terminal = builder.Build();
        Assert.IsTrue(factoryCalled);
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(160, snapshot.Width);
        Assert.AreEqual(48, snapshot.Height);
    }

    [TestMethod]
    public async Task WithHmp1Server_WithoutDimensions_UsesDefaultSize()
    {
        var streams = Channel.CreateUnbounded<Stream>();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHmp1Server(streams.Reader.ReadAllAsync)
            .Build();

        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(80, snapshot.Width);
        Assert.AreEqual(24, snapshot.Height);
    }

    [TestMethod]
    public async Task WithHmp1Server_MultipleListeners_ShareFinalSizeAndFirstRegistrationHooks()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TestTimeout);
        var firstStreams = Channel.CreateUnbounded<Stream>();
        var secondStreams = Channel.CreateUnbounded<Stream>();
        var connected = Channel.CreateUnbounded<string>();
        var firstTransformed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondTransformed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resized = new TaskCompletionSource<(int Width, int Height)>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Hmp1ClientConnectedEventArgs, CancellationToken, Task> onConnected = (e, _) =>
        {
            connected.Writer.TryWrite(e.PeerId);
            return Task.CompletedTask;
        };
        Func<Hmp1ClientDisconnectedEventArgs, CancellationToken, Task> onDisconnected = (_, _) => Task.CompletedTask;
        Func<Hmp1ServerResizedEventArgs, CancellationToken, Task> onResized = (e, _) =>
        {
            resized.TrySetResult((e.Width, e.Height));
            return Task.CompletedTask;
        };
        Func<Hmp1ServerPrimaryChangedEventArgs, CancellationToken, Task> onPrimaryChanged = (_, _) => Task.CompletedTask;
        Hmp1ServerOptions? firstOptions = null;
        using var workload = new Hex1bAppWorkloadAdapter();
        var builder = Hex1bTerminal.CreateBuilder()
            .WithDimensions(40, 12)
            .WithHmp1Server(firstStreams.Reader.ReadAllAsync, options =>
            {
                firstOptions = options;
                options.OnClientConnected = onConnected;
                options.OnClientDisconnected = onDisconnected;
                options.OnResized = onResized;
                options.OnPrimaryChanged = onPrimaryChanged;
                options.StreamTransform = stream =>
                {
                    firstTransformed.TrySetResult();
                    return Task.FromResult(stream);
                };
            })
            .WithDimensions(120, 32)
            .WithHmp1Server(secondStreams.Reader.ReadAllAsync, options =>
            {
                options.OnClientConnected = (_, _) => Task.CompletedTask;
                options.OnClientDisconnected = (_, _) => Task.CompletedTask;
                options.OnResized = (_, _) => Task.CompletedTask;
                options.OnPrimaryChanged = (_, _) => Task.CompletedTask;
                options.StreamTransform = stream =>
                {
                    secondTransformed.TrySetResult();
                    return Task.FromResult(stream);
                };
            })
            .WithDimensions(160, 48);

        Assert.IsNotNull(firstOptions);
        firstOptions.OnClientConnected = null;
        firstOptions.OnClientDisconnected = null;
        firstOptions.OnResized = null;
        firstOptions.OnPrimaryChanged = null;
        firstOptions.StreamTransform = null;

        builder.SetWorkloadFactory(presentation =>
        {
            var adapter = TestSeq.IsType<Hmp1PresentationAdapter>(presentation);
            Assert.AreSame(onConnected, adapter.OnClientConnected);
            Assert.AreSame(onDisconnected, adapter.OnClientDisconnected);
            Assert.AreSame(onResized, adapter.OnResized);
            Assert.AreSame(onPrimaryChanged, adapter.OnPrimaryChanged);
            return new Hex1bTerminalBuildContext(workload, null);
        });
        await using var terminal = builder.Build();
        var (firstServer, firstClient) = DuplexPipeStream.CreatePair();
        await using var firstServerStream = firstServer;
        await using var firstClientStream = firstClient;
        var (secondServer, secondClient) = DuplexPipeStream.CreatePair();
        await using var secondServerStream = secondServer;
        await using var secondClientStream = secondClient;
        await using var client1 = Hmp1TestHelpers.NewClient(firstClient);
        await using var client2 = Hmp1TestHelpers.NewClient(secondClient);

        await firstStreams.Writer.WriteAsync(firstServer, cts.Token);
        await client1.ConnectAsync(cts.Token);
        Assert.AreEqual(client1.PeerId, await connected.Reader.ReadAsync(cts.Token));
        await firstTransformed.Task.WaitAsync(cts.Token);
        Assert.AreEqual(160, client1.CurrentWidth);
        Assert.AreEqual(48, client1.CurrentHeight);
        Assert.IsFalse(client1.IsPrimary);

        await secondStreams.Writer.WriteAsync(secondServer, cts.Token);
        await client2.ConnectAsync(cts.Token);
        Assert.AreEqual(client2.PeerId, await connected.Reader.ReadAsync(cts.Token));
        await secondTransformed.Task.WaitAsync(cts.Token);
        Assert.AreEqual(client1.PeerId, TestSeq.Single(client2.Peers).PeerId);
        Assert.AreEqual(160, client2.CurrentWidth);
        Assert.AreEqual(48, client2.CurrentHeight);
        Assert.IsFalse(client2.IsPrimary);
        Assert.AreEqual(160, terminal.CreateSnapshot().Width);
        Assert.AreEqual(48, terminal.CreateSnapshot().Height);
        Assert.IsFalse(resized.Task.IsCompleted, "Initial sizing and secondary attachment must not raise resize callbacks.");

        await client2.RequestPrimaryAsync(100, 30, cts.Token);
        Assert.IsTrue(await client2.WaitForRoleAsync(true, TestTimeout, cts.Token));
        Assert.AreEqual((100, 30), await resized.Task.WaitAsync(cts.Token));
        Assert.AreEqual(100, client2.CurrentWidth);
        Assert.AreEqual(30, client2.CurrentHeight);
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(100, snapshot.Width);
        Assert.AreEqual(30, snapshot.Height);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task WithHmp1UdsServer_WithDimensions_AdvertisesConfiguredSize(bool dimensionsFirst)
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("Unix domain socket regression runs on Unix.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TestTimeout);
        var socketPath = Path.Combine(OperatingSystem.IsMacOS() ? "/tmp" : Path.GetTempPath(),
            $"hex1b-{Guid.NewGuid():N}.sock");
        using var workload = new Hex1bAppWorkloadAdapter();
        var builder = Hex1bTerminal.CreateBuilder().WithWorkload(workload);
        if (dimensionsFirst)
            builder.WithDimensions(82, 28);
        builder.WithHmp1UdsServer(socketPath);
        if (!dimensionsFirst)
            builder.WithDimensions(82, 28);

        await using (var terminal = builder.Build())
        {
            await using var client = new Hmp1WorkloadAdapter(new Hmp1ClientOptions
            {
                StreamFactory = Hmp1Transports.RetryingUnixSocket(socketPath)
            });
            await client.ConnectAsync(cts.Token);
            Assert.AreEqual(82, client.CurrentWidth);
            Assert.AreEqual(28, client.CurrentHeight);
            Assert.AreEqual(82, terminal.CreateSnapshot().Width);
            Assert.AreEqual(28, terminal.CreateSnapshot().Height);
        }

        Assert.IsFalse(File.Exists(socketPath), "Disposal should release the listener socket.");
    }

    [TestMethod]
    [DataRow(120, 32, true)]
    [DataRow(120, 32, false)]
    [DataRow(82, 28, true)]
    [DataRow(82, 28, false)]
    [DataRow(160, 48, true)]
    [DataRow(160, 48, false)]
    [DataRow(40, 12, true)]
    [DataRow(40, 12, false)]
    [DataRow(80, 24, true)]
    [DataRow(80, 24, false)]
    public async Task WithHmp1Server_PtyWithoutViewers_RetainsDimensionsAfterInput(
        int width, int height, bool dimensionsFirst)
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("The PTY size probe requires /bin/sh and stty.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        var streams = Channel.CreateUnbounded<Stream>();
        var builder = Hex1bTerminal.CreateBuilder()
            .WithPtyProcess("/bin/sh", "-c",
                "set -- $(stty size); printf 'initial:%s,%s:end\\n' \"$1\" \"$2\"; read -r input; " +
                "set -- $(stty size); printf 'after:%s,%s:end\\n' \"$1\" \"$2\"; read -r input");
        if (dimensionsFirst)
            builder.WithDimensions(width, height);
        builder.WithHmp1Server(streams.Reader.ReadAllAsync);
        if (!dimensionsFirst)
            builder.WithDimensions(width, height);

        await using var terminal = builder.Build();
        var runTask = terminal.RunAsync(cts.Token);
        try
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText($"initial:{height},{width}:end"), TestTimeout,
                    "The child must observe the configured PTY size before any viewer attaches.")
                .Type("continue").Enter()
                .WaitUntil(s => s.ContainsText($"after:{height},{width}:end"), TestTimeout,
                    "Automation input must not change the child PTY size.")
                .Type("exit").Enter()
                .Build()
                .ApplyAsync(terminal, cts.Token);
            Assert.AreEqual(0, await runTask.WaitAsync(cts.Token));
        }
        finally
        {
            await cts.CancelAsync();
            try
            {
                await runTask.WaitAsync(TestTimeout);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
            }
        }
    }
}
