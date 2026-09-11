using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Hex1b.Automation;
using Hex1b.Tokens;

namespace Hex1b.Tests.Hmp1;

[TestClass]
public class Hmp1ActivityStateTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    [DataRow("", 0, null, 0, null)]
    [DataRow("\x1b]9;4;0\a", 0, null, 0, null)]
    [DataRow("\x1b]9;4;1;0\a\x1b]133;A\a", 1, 0, 1, null)]
    [DataRow("\x1b]9;4;2;100\a\x1b]133;B\a", 2, 100, 2, null)]
    [DataRow("\x1b]9;4;3\a\x1b]133;C\a", 3, null, 3, null)]
    [DataRow("\x1b]9;4;4;37\a\x1b]133;D;0\a", 4, 37, 4, 0)]
    [DataRow("\x1b]133;D;7\a", 0, null, 4, 7)]
    [DataRow("\x1b]133;D;-2147483648\a", 0, null, 4, int.MinValue)]
    [DataRow("\x1b]133;D;2147483647\a", 0, null, 4, int.MaxValue)]
    [DataRow("\x1b]133;D\a", 0, null, 4, null)]
    [DataRow("\x1b]133;D;\a", 0, null, 4, null)]
    [DataRow("\x1b]133;D;-1\a\x1b]133;A\a", 0, null, 1, -1)]
    [DataRow("\x1b]133;D;-1\a\x1b]133;B\a", 0, null, 2, -1)]
    [DataRow("\x1b]133;D;-1\a\x1b]133;C\a", 0, null, 3, -1)]
    public async Task StateSync_LateAttachAndReconnect_RestoresAuthoritativeActivity(
        string sequence, int state, int? percentage, int phase, int? exitCode)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 5).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(sequence + "ready"));

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var connection = await ConnectAsync(server);
            await using var handle = connection.Handle;
            await using var client = connection.Client;
            await using var mirror = Hex1bTerminal.CreateBuilder()
                .WithWorkload(client).WithHeadless().Build();
            await WaitAsync(mirror, snapshot => snapshot.ContainsText("ready") &&
                (int)snapshot.Progress.State == state && snapshot.Progress.Percentage == percentage &&
                (int)snapshot.ShellIntegration.Phase == phase && snapshot.ShellIntegration.LastExitCode == exitCode);
            using var snapshot = mirror.CreateSnapshot();
            Assert.AreEqual(producer.Progress, snapshot.Progress);
            Assert.AreEqual(producer.ShellIntegration, snapshot.ShellIntegration);
        }
    }

    [TestMethod]
    public async Task StateSync_TwoHops_LiveChangesAndClearPreserveActivity()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 5).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;D;-1\a\x1b]133;C\a\x1b]9;4;3\ainitial"));
        var upstream = await ConnectAsync(server);
        await using var upstreamHandle = upstream.Handle;
        await using var upstreamClient = upstream.Client;
        await using var relay = new Hmp1PresentationAdapter(20, 5);
        await using var replica = Hex1bTerminal.CreateBuilder()
            .WithWorkload(upstreamClient).WithPresentation(relay).Build();
        var downstream = await ConnectAsync(relay);
        await using var downstreamHandle = downstream.Handle;
        await using var downstreamClient = downstream.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(downstreamClient).WithHeadless().Build();
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("initial") &&
            snapshot.ShellIntegration.Phase == TerminalShellIntegrationPhase.Executing &&
            snapshot.ShellIntegration.LastExitCode == -1 &&
            snapshot.Progress.State == TerminalProgressState.Indeterminate);

        foreach (var sequence in new[]
        {
            "\x1b]9;4;1;15\a", "\x1b]9;4;2;80\a", "\x1b]9;4;4;100\a",
            "\x1b]133;D;0\a", "\x1b]133;A\a", "\x1b]133;B\a", "\x1b]9;4;0\a"
        })
        {
            var previousProgress = producer.Progress;
            var previousShell = producer.ShellIntegration;
            workload.Write(sequence);
            await WaitAsync(producer, snapshot =>
                snapshot.Progress != previousProgress || snapshot.ShellIntegration != previousShell);
            await WaitAsync(mirror, snapshot =>
                snapshot.Progress == producer.Progress && snapshot.ShellIntegration == producer.ShellIntegration);
            Assert.AreEqual(producer.Progress, replica.Progress);
            Assert.AreEqual(producer.ShellIntegration, replica.ShellIntegration);
        }
    }

    [TestMethod]
    [DataRow("\x1b]9;4;2;63\x1b\\", 1)]
    [DataRow("\x1b]9;4;2;63\x1b\\", 8)]
    [DataRow("\x1b]9;4;2;63\x1b\\", 11)]
    [DataRow("\x1b]133;D;-7\x1b\\", 1)]
    [DataRow("\x1b]133;D;-7\x1b\\", 8)]
    [DataRow("\x1b]133;D;-7\x1b\\", 11)]
    public async Task StateSync_FragmentedOscAcrossTwoHops_ContinuesOnlyRealOutput(string sequence, int split)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 5).Build();
        var upstream = await ConnectAsync(server);
        await using var upstreamHandle = upstream.Handle;
        await using var upstreamClient = upstream.Client;
        await using var relay = new Hmp1PresentationAdapter(20, 5);
        await using var replica = Hex1bTerminal.CreateBuilder()
            .WithWorkload(upstreamClient).WithPresentation(relay).Build();
        workload.Write("\x1b]133;C\a\x1b]9;4;1;20\aready" + sequence[..split]);
        await WaitAsync(replica, snapshot => snapshot.ContainsText("ready"));
        var late = await ConnectAsync(relay);
        await using var lateHandle = late.Handle;
        await using var lateClient = late.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(lateClient).WithHeadless().Build();
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("ready") &&
            snapshot.Progress.Percentage == 20 &&
            snapshot.ShellIntegration.Phase == TerminalShellIntegrationPhase.Executing);

        var progressChanges = new ConcurrentQueue<TerminalProgress>();
        var shellChanges = new ConcurrentQueue<TerminalShellIntegration>();
        mirror.ProgressChanged += progressChanges.Enqueue;
        mirror.ShellIntegrationChanged += shellChanges.Enqueue;
        workload.Write(sequence[split..] + "done");
        await WaitAsync(producer, snapshot => snapshot.ContainsText("done"));
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("done") &&
            snapshot.Progress == producer.Progress && snapshot.ShellIntegration == producer.ShellIntegration);
        Assert.AreEqual(sequence.Contains("133") ? 1 : 0, shellChanges.Count);
        Assert.AreEqual(sequence.Contains("9;4") ? 1 : 0, progressChanges.Count);
        using var expected = producer.CreateSnapshot();
        using var actual = mirror.CreateSnapshot();
        for (var row = 0; row < expected.Height; row++)
            Assert.AreEqual(expected.GetLine(row), actual.GetLine(row));
    }

    [TestMethod]
    public async Task StateSync_RepeatedReplayAtRelay_DeduplicatesAndNeverSynthesizesShellMarkers()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var upstream = Hmp1TestHelpers.NewClient(streams.Client);
        var baseline = Activity(2, 42, 4, -7);
        await HandshakeAsync(wire, upstream, "\x1b]9;4;2;42\ainitial", baseline);
        await using var relay = new Hmp1PresentationAdapter(20, 5);
        await using var replica = Hex1bTerminal.CreateBuilder()
            .WithWorkload(upstream).WithPresentation(relay).Build();
        var connection = await ConnectAsync(relay);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(client).WithHeadless().Build();
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("initial") &&
            snapshot.ShellIntegration.LastExitCode == -7);
        var progress = new ConcurrentQueue<TerminalProgress>();
        var shell = new ConcurrentQueue<TerminalShellIntegration>();
        mirror.ProgressChanged += progress.Enqueue;
        mirror.ShellIntegrationChanged += shell.Enqueue;

        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.Output,
            "partial\x1b]133;D;99\x1b"u8.ToArray(), TestContext.Current.CancellationToken);
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("partial"));
        // RIS resets activity during replay; only the final restored value is observable.
        await WriteSyncAsync(wire, "\x1b" + "c\x1b]9;4;2;42\arepeat", baseline);
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("repeat") &&
            snapshot.ShellIntegration.LastExitCode == -7);
        Assert.IsTrue(progress.IsEmpty);
        Assert.IsTrue(shell.IsEmpty);

        await WriteSyncAsync(wire, "\x1b[2J\x1b[Hchanged", Activity(4, 81, 1, -7));
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("changed") &&
            snapshot.Progress.Percentage == 81 && snapshot.ShellIntegration.Phase == TerminalShellIntegrationPhase.Prompt);
        Assert.AreEqual(TerminalProgressState.Warning, TestSeq.Single(progress).State);
        Assert.AreEqual(TerminalShellIntegrationPhase.Prompt, TestSeq.Single(shell).Phase);

        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.Output,
            "\x1b]133;C\a\x1b]9;4;0\alive"u8.ToArray(), TestContext.Current.CancellationToken);
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("live") &&
            snapshot.Progress.State == TerminalProgressState.None &&
            snapshot.ShellIntegration.Phase == TerminalShellIntegrationPhase.Executing);
        TestSeq.AreEqual(new[] { TerminalShellIntegrationPhase.Prompt, TerminalShellIntegrationPhase.Executing },
            shell.Select(value => value.Phase));
    }

    [TestMethod]
    public async Task ReadPumpAsync_CheckpointPaused_HoldsSubsequentScreenReplayAndFollowingOutput()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await HandshakeAsync(wire, client, "initial", Activity(1, 25, 3, -7));
        await using var mirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(client).WithHeadless().Build();
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("initial") && snapshot.Progress.Percentage == 25);
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.StateSync,
            "\x1b[2J\x1b[Hreplacement"u8.ToArray(), TestContext.Current.CancellationToken);
        using (var snapshot = mirror.CreateSnapshot())
        {
            Assert.IsTrue(snapshot.ContainsText("initial"));
            Assert.AreEqual(25, snapshot.Progress.Percentage);
        }
        await Hmp1Protocol.WriteActivityStateAsync(wire, Activity(0, null, 4, null), TestContext.Current.CancellationToken);
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.Output,
            "\r\nlive"u8.ToArray(), TestContext.Current.CancellationToken);
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("replacement") && snapshot.ContainsText("live") &&
            snapshot.Progress.State == TerminalProgressState.None &&
            snapshot.ShellIntegration.Phase == TerminalShellIntegrationPhase.Finished &&
            snapshot.ShellIntegration.LastExitCode == null);
    }

    [TestMethod]
    public async Task ConnectAsync_CheckpointPaused_DoesNotPublishPartialBrowserBaseline()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        using var timeout = new CancellationTokenSource(Timeout);
        var connecting = client.ConnectAsync(timeout.Token);
        await Hmp1Protocol.ReadFrameAsync(wire, timeout.Token);
        await Hmp1Protocol.WriteHelloAsync(wire, 20, 5, "peer", null, [], timeout.Token);
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.StateSync, "screen"u8.ToArray(), timeout.Token);
        Assert.IsFalse(connecting.IsCompleted);
        Assert.IsFalse(client.IsConnected);
        await using var view = new Hwt1PresentationAdapter();
        await using var mirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(client).WithPresentation(view).Build();
        var pending = view.ReadFrameAsync(timeout.Token).AsTask();
        Assert.IsFalse(pending.IsCompleted);
        await Hmp1Protocol.WriteActivityStateAsync(wire, Activity(2, 67, 4, -1), timeout.Token);
        await connecting.WaitAsync(timeout.Token);
        var bytes = await pending.WaitAsync(timeout.Token);
        var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[4..]);
        using var metadata = JsonDocument.Parse(bytes.Slice(8, length));
        Assert.AreEqual("error", metadata.RootElement.GetProperty("progress").GetProperty("state").GetString());
        Assert.AreEqual(67, metadata.RootElement.GetProperty("progress").GetProperty("percentage").GetInt32());
        Assert.AreEqual("finished", metadata.RootElement.GetProperty("shellIntegration").GetProperty("phase").GetString());
        Assert.AreEqual(-1, mirror.ShellIntegration.LastExitCode);
    }

    [TestMethod]
    public async Task StateSync_BlockedImpactPresentation_PublicSnapshotHasCompleteActivity()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await HandshakeAsync(wire, client, "\x1b" + "c\x1b]9;4;1;9\ascreen", Activity(2, 67, 4, -7));
        await using var presentation = new ReplayImpactGate();
        await using var mirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(client).WithPresentation(presentation).Build();
        using var timeout = new CancellationTokenSource(Timeout);
        try
        {
            await presentation.Entered.Task.WaitAsync(timeout.Token);
            using var snapshot = mirror.CreateSnapshot();
            Assert.IsTrue(snapshot.ContainsText("screen"));
            Assert.AreEqual(TerminalProgressState.Error, snapshot.Progress.State);
            Assert.AreEqual(67, snapshot.Progress.Percentage);
            Assert.AreEqual(TerminalShellIntegrationPhase.Finished, snapshot.ShellIntegration.Phase);
            Assert.AreEqual(-7, snapshot.ShellIntegration.LastExitCode);
            var ready = mirror.WaitForHmp1InitialReplayAsync(timeout.Token);
            Assert.IsFalse(ready.IsCompleted);
            presentation.Release.TrySetResult();
            await ready.WaitAsync(timeout.Token);
        }
        finally
        {
            presentation.Release.TrySetResult();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task StateSync_PresentationFailure_FailsReadinessWithoutPublishingUnappliedCheckpoint(bool afterApplication)
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await HandshakeAsync(wire, client, "\x1b]9;4;1;9\ascreen", Activity(2, 67, 4, -7));
        await using ReplayPresentationGate presentation = afterApplication
            ? new ReplayImpactGate() : new ReplayPresentationGate();
        presentation.Fail = true;
        await using var mirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(client).WithPresentation(presentation).Build();
        using var timeout = new CancellationTokenSource(Timeout);
        try
        {
            await presentation.Entered.Task.WaitAsync(timeout.Token);
            var progressChanges = new ConcurrentQueue<TerminalProgress>();
            mirror.ProgressChanged += progressChanges.Enqueue;
            var ready = mirror.WaitForHmp1InitialReplayAsync(timeout.Token);
            presentation.Release.TrySetResult();
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ready.WaitAsync(timeout.Token));
            Assert.IsTrue(progressChanges.IsEmpty,
                "A failed presentation must not cause a new checkpoint assignment in cleanup.");
            using var snapshot = mirror.CreateSnapshot();
            Assert.AreEqual(afterApplication, snapshot.ContainsText("screen"));
            Assert.AreEqual(afterApplication ? TerminalProgressState.Error : TerminalProgressState.None, snapshot.Progress.State);
            Assert.AreEqual(afterApplication ? 67 : (int?)null, snapshot.Progress.Percentage);
            Assert.AreEqual(afterApplication ? TerminalShellIntegrationPhase.Finished : TerminalShellIntegrationPhase.Unknown,
                snapshot.ShellIntegration.Phase);
        }
        finally
        {
            presentation.Release.TrySetResult();
        }
    }

    [TestMethod]
    public async Task StateSync_WorkloadFilterFailure_DoesNotRestoreCheckpointAndFailsReadiness()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await HandshakeAsync(wire, client, "\x1b]9;4;1;9\ascreen", Activity(2, 67, 4, -7));
        await using var mirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(client).WithHeadless().AddWorkloadFilter(new FailingReplayFilter()).Build();
        using var timeout = new CancellationTokenSource(Timeout);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => mirror.WaitForHmp1InitialReplayAsync(timeout.Token));
        using var snapshot = mirror.CreateSnapshot();
        Assert.IsFalse(snapshot.ContainsText("screen"));
        Assert.AreEqual(TerminalProgressState.None, snapshot.Progress.State);
        Assert.AreEqual(TerminalShellIntegrationPhase.Unknown, snapshot.ShellIntegration.Phase);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task StateSync_GeneratedAlternateScreen_ReentrantObserversSeeCommittedSnapshot(bool unchangedTitles)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 5).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]2;saved title\a\x1b]1;saved icon\a\x1b]22;0\a" +
            "\x1b]2;current title\a\x1b]1;current icon\a" +
            "\x1b]133;D;-7\a\x1b]133;C\a\x1b]9;4;2;63\a\x1b[?1049hcomplete screen"));
        using var expected = producer.CreateSnapshot(includeAllKgpImages: false, includeSavedTitles: true);
        Assert.AreEqual(1, expected.SavedTitles.Count);
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var presentation = new ReplayPresentationGate();
        await using var mirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(client).WithPresentation(presentation).Build();
        using var timeout = new CancellationTokenSource(Timeout);
        var observations = new ConcurrentQueue<string>();
        Action<string> titleChanged = _ => Observe("title");
        Action<string> iconChanged = _ => Observe("icon");
        try
        {
            await presentation.Entered.Task.WaitAsync(timeout.Token);
            mirror.Resize(10, 3);
            mirror.ApplyTokens(AnsiTokenizer.Tokenize(
                (unchangedTitles
                    ? "\x1b]2;current title\a\x1b]1;current icon\a"
                    : "\x1b]2;old title\a\x1b]1;old icon\a") +
                "\x1b]133;A\a\x1b]9;4;1;9\aold"));
            mirror.WindowTitleChanged += titleChanged;
            mirror.IconNameChanged += iconChanged;
            presentation.Invalidated = () => Observe("resize");
            presentation.Release.TrySetResult();
            await mirror.WaitForHmp1InitialReplayAsync(timeout.Token);
            Assert.AreEqual(unchangedTitles ? 0 : 1, observations.Count(value => value == "title"));
            Assert.AreEqual(unchangedTitles ? 0 : 1, observations.Count(value => value == "icon"));
            Assert.AreEqual(1, observations.Count(value => value == "resize"));
        }
        finally
        {
            presentation.Release.TrySetResult();
            presentation.Invalidated = null;
            mirror.WindowTitleChanged -= titleChanged;
            mirror.IconNameChanged -= iconChanged;
        }

        void Observe(string observer)
        {
            using var snapshot = mirror.CreateSnapshot(includeAllKgpImages: false, includeSavedTitles: true);
            Assert.AreEqual(expected.Width, snapshot.Width, observer);
            Assert.AreEqual(expected.Height, snapshot.Height, observer);
            Assert.IsTrue(snapshot.InAlternateScreen, observer);
            for (var row = 0; row < expected.Height; row++)
                Assert.AreEqual(expected.GetLine(row), snapshot.GetLine(row), observer);
            Assert.AreEqual(expected.WindowTitle, snapshot.WindowTitle, observer);
            Assert.AreEqual(expected.IconName, snapshot.IconName, observer);
            TestSeq.AreEqual(expected.SavedTitles, snapshot.SavedTitles);
            Assert.AreEqual(expected.Progress, snapshot.Progress, observer);
            Assert.AreEqual(expected.ShellIntegration, snapshot.ShellIntegration, observer);
            observations.Enqueue(observer);
        }
    }

    [TestMethod]
    public async Task StateSync_TokenApplicationFailure_DoesNotInstallCheckpointOrEmitIntermediateActivity()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await HandshakeAsync(wire, client,
            "\x1b]9;4;1;9\a\x1b]2;new title\a\x1b]1;new icon\a1\r\n2\r\n3\r\n4\r\n5\r\n6",
            Activity(2, 67, 4, -7));
        await using var presentation = new ReplayPresentationGate();
        await using var mirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(client).WithPresentation(presentation)
            .WithScrollback(10, _ => throw new InvalidOperationException("Token application failed.")).Build();
        using var timeout = new CancellationTokenSource(Timeout);
        try
        {
            await presentation.Entered.Task.WaitAsync(timeout.Token);
            mirror.Resize(10, 3);
            var changes = new ConcurrentQueue<TerminalProgress>();
            var titles = new ConcurrentQueue<string>();
            var icons = new ConcurrentQueue<string>();
            var invalidations = 0;
            mirror.ProgressChanged += changes.Enqueue;
            mirror.WindowTitleChanged += titles.Enqueue;
            mirror.IconNameChanged += icons.Enqueue;
            presentation.Invalidated = () => Interlocked.Increment(ref invalidations);
            var ready = mirror.WaitForHmp1InitialReplayAsync(timeout.Token);
            presentation.Release.TrySetResult();
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ready.WaitAsync(timeout.Token));
            Assert.IsTrue(changes.IsEmpty);
            Assert.IsTrue(titles.IsEmpty);
            Assert.IsTrue(icons.IsEmpty);
            Assert.AreEqual(0, invalidations);
            Assert.AreEqual(TerminalProgressState.None, mirror.Progress.State);
            Assert.AreEqual(TerminalShellIntegrationPhase.Unknown, mirror.ShellIntegration.Phase);
        }
        finally
        {
            presentation.Release.TrySetResult();
            presentation.Invalidated = null;
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ConnectAsync_FailedOrCancelledHandshake_UnblocksInitialReplayWait(bool cancelled)
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await using var mirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(client).WithHeadless().Build();
        using var timeout = new CancellationTokenSource(Timeout);
        await mirror.WaitForHmp1InitialReplayAsync(timeout.Token);
        using var handshakeCancellation = new CancellationTokenSource();
        var connecting = client.ConnectAsync(handshakeCancellation.Token);
        await Hmp1Protocol.ReadFrameAsync(wire, timeout.Token);
        var ready = mirror.WaitForHmp1InitialReplayAsync(timeout.Token);
        Assert.IsFalse(ready.IsCompleted);
        if (cancelled)
        {
            handshakeCancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => connecting.WaitAsync(timeout.Token));
            await Assert.ThrowsAsync<OperationCanceledException>(() => ready.WaitAsync(timeout.Token));
        }
        else
        {
            await Hmp1Protocol.WriteHelloAsync(wire, 20, 5, timeout.Token);
            await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.StateSync, "screen"u8.ToArray(), timeout.Token);
            await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.ActivityState, "{}"u8.ToArray(), timeout.Token);
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => connecting.WaitAsync(timeout.Token));
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => ready.WaitAsync(timeout.Token));
        }
        Assert.IsFalse(client.IsConnected);
        using var snapshot = mirror.CreateSnapshot();
        Assert.IsFalse(snapshot.ContainsText("screen"));
    }

    [TestMethod]
    public async Task ConnectAsync_ConnectedCallbackAwaitsInitialReplay_DoesNotDeadlockHandshake()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless().Build();
        client.OnConnected = async (_, ct) => await mirror.WaitForHmp1InitialReplayAsync(ct);
        await HandshakeAsync(wire, client, "screen", Activity(2, 67, 4, -7));
        Assert.AreEqual(67, mirror.Progress.Percentage);
        Assert.AreEqual(-7, mirror.ShellIntegration.LastExitCode);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WithPlaceholderWorkload_LateBaselineAndReconnect_PreservesActivityAndRelay(bool throughRelay)
    {
#pragma warning disable HEX1B002
        using var firstWorkload = new Hex1bAppWorkloadAdapter();
        using var secondWorkload = new Hex1bAppWorkloadAdapter();
        using var placeholder = new Hex1bAppWorkloadAdapter();
        await using var firstServer = new Hmp1PresentationAdapter(20, 10);
        await using var secondServer = new Hmp1PresentationAdapter(20, 10);
        await using var firstProducer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(firstWorkload).WithPresentation(firstServer).WithDimensions(20, 10).Build();
        await using var secondProducer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(secondWorkload).WithPresentation(secondServer).WithDimensions(20, 10).Build();
        firstProducer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;D;-7\a\x1b]133;C\a\x1b]9;4;2;63\afirst"));
        secondProducer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;D;3\a\x1b]9;4;4;90\asecond"));
        var streams = Channel.CreateUnbounded<Stream>();
        await using var relay = new Hmp1PresentationAdapter(20, 10);
        await using var view = new Hwt1PresentationAdapter(20, 10);
        await using var mirror = Hex1bTerminal.CreateBuilder()
            .WithHmp1Client(async ct => await streams.Reader.ReadAsync(ct))
            .WithPlaceholderWorkload(builder => builder.WithWorkload(placeholder))
            .WithPresentation(throughRelay ? relay : view).WithDimensions(20, 10).Build();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var running = mirror.RunAsync(cancellation.Token);
        Hmp1ClientHandle? firstHandle = null;
        Hmp1ClientHandle? secondHandle = null;
        Hmp1ClientHandle? downstreamHandle = null;
        Hmp1WorkloadAdapter? downstreamClient = null;
        Hex1bTerminal? downstream = null;
        Hwt1PresentationAdapter? localBrowser = null;
        string? localPeerId = null;
        try
        {
            placeholder.Write("\x1b]133;A\a\x1b]9;4;1;12\aWaiting");
            await WaitAsync(mirror, snapshot => snapshot.ContainsText("Waiting") &&
                snapshot.ShellIntegration.Phase == TerminalShellIntegrationPhase.Prompt &&
                snapshot.Progress.Percentage == 12);
            var phases = new ConcurrentQueue<TerminalShellIntegrationPhase>();
            var progress = new ConcurrentQueue<TerminalProgressState>();
            mirror.ShellIntegrationChanged += value => phases.Enqueue(value.Phase);
            mirror.ProgressChanged += value => progress.Enqueue(value.State);
            if (!throughRelay)
            {
                var waiting = await ReadActivityFrameAsync(view, metadata =>
                    metadata.GetProperty("shellIntegration").GetProperty("phase").GetString() == "prompt");
                Assert.AreEqual(12, waiting.GetProperty("progress").GetProperty("percentage").GetInt32());
            }

            var firstStreams = CreateStreams();
            var firstAccept = firstServer.AddClient(firstStreams.Server, cancellation.Token);
            streams.Writer.TryWrite(firstStreams.Client);
            firstHandle = await firstAccept.WaitAsync(Timeout, cancellation.Token);
            await WaitAsync(mirror, snapshot => snapshot.ContainsText("first") &&
                snapshot.ShellIntegration.Phase == TerminalShellIntegrationPhase.Executing &&
                snapshot.ShellIntegration.LastExitCode == -7 && snapshot.Progress.Percentage == 63);
            TestSeq.AreEqual(new[] { TerminalShellIntegrationPhase.Executing }, phases);
            TestSeq.AreEqual(new[] { TerminalProgressState.Error }, progress);
            if (throughRelay)
            {
                (downstreamHandle, downstreamClient) = await ConnectAsync(relay);
                downstream = Hex1bTerminal.CreateBuilder().WithWorkload(downstreamClient).WithPresentation(view).Build();
            }
            var first = await ReadActivityFrameAsync(view, metadata =>
                metadata.GetProperty("shellIntegration").GetProperty("phase").GetString() == "executing");
            Assert.AreEqual(-7, first.GetProperty("shellIntegration").GetProperty("lastExitCode").GetInt32());
            Assert.AreEqual(63, first.GetProperty("progress").GetProperty("percentage").GetInt32());
            Assert.AreEqual(JsonValueKind.String, first.GetProperty("peer").GetProperty("id").ValueKind);
            if (throughRelay)
            {
                localBrowser = await relay.CreateBrowserViewAsync("local browser", cancellation.Token);
                var local = await ReadActivityFrameAsync(localBrowser, metadata =>
                    metadata.GetProperty("shellIntegration").GetProperty("phase").GetString() == "executing");
                localPeerId = local.GetProperty("peer").GetProperty("id").GetString();
                Assert.IsNotNull(localPeerId);
                Assert.AreNotEqual(firstHandle.PeerId, localPeerId);
                Assert.AreNotEqual(downstreamHandle!.PeerId, localPeerId);
                Assert.IsFalse(local.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
                await localBrowser.HandleMessageAsync(
                    """{"type":"requestPrimary","columns":20,"rows":10}"""u8.ToArray(), cancellation.Token);
                var primary = await ReadActivityFrameAsync(localBrowser, metadata =>
                    metadata.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
                Assert.AreEqual(localPeerId, primary.GetProperty("peer").GetProperty("id").GetString());
                Assert.AreEqual(localPeerId, primary.GetProperty("peer").GetProperty("primaryId").GetString());
                Assert.AreEqual(localPeerId, relay.PrimaryPeerId);
            }

            await firstHandle.DisposeAsync();
            placeholder.Write("Waiting again");
            await WaitAsync(mirror, snapshot => snapshot.ContainsText("Waiting again") &&
                snapshot.ShellIntegration.Phase == TerminalShellIntegrationPhase.Unknown);
            if (localBrowser is not null)
            {
                var fallback = await ReadActivityFrameAsync(localBrowser, metadata =>
                    metadata.GetProperty("shellIntegration").GetProperty("phase").GetString() == "unknown");
                Assert.AreEqual(localPeerId, fallback.GetProperty("peer").GetProperty("id").GetString());
                Assert.IsTrue(fallback.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
            }

            var secondStreams = CreateStreams();
            var secondAccept = secondServer.AddClient(secondStreams.Server, cancellation.Token);
            streams.Writer.TryWrite(secondStreams.Client);
            secondHandle = await secondAccept.WaitAsync(Timeout, cancellation.Token);
            await WaitAsync(mirror, snapshot => snapshot.ContainsText("second") &&
                snapshot.ShellIntegration.Phase == TerminalShellIntegrationPhase.Finished &&
                snapshot.ShellIntegration.LastExitCode == 3 && snapshot.Progress.Percentage == 90);
            var second = await ReadActivityFrameAsync(view, metadata =>
                metadata.GetProperty("shellIntegration").GetProperty("phase").GetString() == "finished");
            Assert.AreEqual(3, second.GetProperty("shellIntegration").GetProperty("lastExitCode").GetInt32());
            Assert.AreEqual("warning", second.GetProperty("progress").GetProperty("state").GetString());
            Assert.AreEqual(90, second.GetProperty("progress").GetProperty("percentage").GetInt32());
            if (localBrowser is not null)
            {
                var reconnected = await ReadActivityFrameAsync(localBrowser, metadata =>
                    metadata.GetProperty("shellIntegration").GetProperty("phase").GetString() == "finished");
                Assert.AreEqual(localPeerId, reconnected.GetProperty("peer").GetProperty("id").GetString());
                Assert.IsTrue(reconnected.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
                Assert.AreEqual(3, reconnected.GetProperty("shellIntegration").GetProperty("lastExitCode").GetInt32());
            }
            if (downstream is not null)
            {
                Assert.AreEqual(mirror.Progress, downstream.Progress);
                Assert.AreEqual(mirror.ShellIntegration, downstream.ShellIntegration);
            }
            TestSeq.AreEqual(new[]
            {
                TerminalShellIntegrationPhase.Executing,
                TerminalShellIntegrationPhase.Unknown,
                TerminalShellIntegrationPhase.Finished
            }, phases);
        }
        finally
        {
            cancellation.Cancel();
            try { await running.WaitAsync(Timeout, TestContext.Current.CancellationToken); }
            catch (OperationCanceledException) { }
            if (localBrowser is not null) await localBrowser.DisposeAsync();
            if (downstream is not null) await downstream.DisposeAsync();
            if (downstreamClient is not null) await downstreamClient.DisposeAsync();
            if (downstreamHandle is not null) await downstreamHandle.DisposeAsync();
            if (firstHandle is not null) await firstHandle.DisposeAsync();
            if (secondHandle is not null) await secondHandle.DisposeAsync();
        }
#pragma warning restore HEX1B002
    }

    [TestMethod]
    [DataRow(0, null)]
    [DataRow(1, 0)]
    [DataRow(2, 100)]
    [DataRow(3, null)]
    [DataRow(4, 42)]
    public async Task ReadOutputAsync_NativeAnsiReplay_RestoresProgressWithoutShellSequences(int state, int? percentage)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 5).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Activity(state, percentage, 0, null).BuildProgressReplay() +
            "\x1b]133;D;-7\a\x1b]133;A\a"));
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        using var timeout = new CancellationTokenSource(Timeout);
        var bytes = await client.ReadOutputAsync(timeout.Token);
        var ansi = Encoding.UTF8.GetString(bytes.Span);
        StringAssert.Contains(ansi, Activity(state, percentage, 0, null).BuildProgressReplay());
        Assert.IsFalse(ansi.Contains("]133;"));
        using var nativeWorkload = new Hex1bAppWorkloadAdapter();
        await using var native = Hex1bTerminal.CreateBuilder()
            .WithWorkload(nativeWorkload).WithHeadless().Build();
        native.ApplyTokens(AnsiTokenizer.Tokenize(ansi));
        Assert.AreEqual(producer.Progress, native.Progress);
        Assert.AreEqual(TerminalShellIntegrationPhase.Unknown, native.ShellIntegration.Phase);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("{}")]
    [DataRow("null")]
    [DataRow("{")]
    [DataRow("""{"progress":null,"shellIntegration":{"phase":0,"lastExitCode":null}}""")]
    [DataRow("""{"progress":{"state":0},"shellIntegration":{"phase":0,"lastExitCode":null}}""")]
    [DataRow("""{"progress":{"state":0,"percentage":0},"shellIntegration":{"phase":0,"lastExitCode":null}}""")]
    [DataRow("""{"progress":{"state":1,"percentage":null},"shellIntegration":{"phase":0,"lastExitCode":null}}""")]
    [DataRow("""{"progress":{"state":2,"percentage":101},"shellIntegration":{"phase":0,"lastExitCode":null}}""")]
    [DataRow("""{"progress":{"state":3,"percentage":7},"shellIntegration":{"phase":0,"lastExitCode":null}}""")]
    [DataRow("""{"progress":{"state":4,"percentage":-1},"shellIntegration":{"phase":0,"lastExitCode":null}}""")]
    [DataRow("""{"progress":{"state":5,"percentage":null},"shellIntegration":{"phase":0,"lastExitCode":null}}""")]
    [DataRow("""{"progress":{"state":0,"percentage":null},"shellIntegration":{"phase":5,"lastExitCode":null}}""")]
    [DataRow("""{"progress":{"state":0,"percentage":null},"shellIntegration":{"phase":0,"lastExitCode":0}}""")]
    [DataRow("""{"progress":{"state":0,"percentage":null},"shellIntegration":{"phase":4,"lastExitCode":2147483648}}""")]
    [DataRow("""{"progress":{"state":0,"percentage":null},"shellIntegration":{"phase":4,"lastExitCode":"0"}}""")]
    [DataRow("""{"progress":{"state":0,"percentage":null,"state":1},"shellIntegration":{"phase":0,"lastExitCode":null}}""")]
    [DataRow("""{"progress":{"state":0,"percentage":null},"shellIntegration":{"phase":0,"lastExitCode":null},"extra":true}""")]
    public async Task ConnectAsync_MalformedActivityCheckpoint_FailsWithoutPublishingReplay(string payload)
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        using var timeout = new CancellationTokenSource(Timeout);
        var connecting = client.ConnectAsync(timeout.Token);
        await Hmp1Protocol.ReadFrameAsync(wire, timeout.Token);
        await Hmp1Protocol.WriteHelloAsync(wire, 20, 5, timeout.Token);
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.StateSync, "screen"u8.ToArray(), timeout.Token);
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.ActivityState, Encoding.UTF8.GetBytes(payload), timeout.Token);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => connecting.WaitAsync(timeout.Token));
        Assert.IsFalse(client.IsConnected);
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("incomplete")]
    [DataRow("oversize")]
    [DataRow("wrong-frame")]
    public async Task ConnectAsync_InvalidCheckpointFraming_FailsConnection(string failure)
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        using var timeout = new CancellationTokenSource(Timeout);
        var connecting = client.ConnectAsync(timeout.Token);
        await Hmp1Protocol.ReadFrameAsync(wire, timeout.Token);
        await Hmp1Protocol.WriteHelloAsync(wire, 20, 5, timeout.Token);
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.StateSync, "screen"u8.ToArray(), timeout.Token);
        if (failure is "incomplete" or "oversize")
        {
            var header = new byte[5];
            header[0] = (byte)Hmp1FrameType.ActivityState;
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(1),
                failure == "oversize" ? Hmp1ActivityState.MaxPayloadSize + 1 : 10);
            await wire.WriteAsync(header, timeout.Token);
            await wire.FlushAsync(timeout.Token);
        }
        if (failure == "wrong-frame")
            await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.Output, "live"u8.ToArray(), timeout.Token);
        if (failure is "incomplete" or "missing")
            await wire.DisposeAsync();
        if (failure is "incomplete" or "oversize")
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connecting.WaitAsync(timeout.Token));
        else
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => connecting.WaitAsync(timeout.Token));
        Assert.IsFalse(client.IsConnected);
    }

    [TestMethod]
    public async Task ReadPumpAsync_InvalidSubsequentCheckpoint_DisconnectsWithoutApplyingPartialReplay()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await HandshakeAsync(wire, client, "initial", Activity(1, 25, 3, -7));
        await using var mirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(client).WithHeadless().Build();
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("initial") && snapshot.Progress.Percentage == 25);
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.StateSync,
            "\x1b"u8.ToArray(), TestContext.Current.CancellationToken);
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.ActivityState,
            "{}"u8.ToArray(), TestContext.Current.CancellationToken);
        await client.DisconnectedTask.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.AreEqual(25, mirror.Progress.Percentage);
        Assert.AreEqual(TerminalShellIntegrationPhase.Executing, mirror.ShellIntegration.Phase);
        Assert.AreEqual(-7, mirror.ShellIntegration.LastExitCode);
    }

    private static Hmp1ActivityState Activity(int state, int? percentage, int phase, int? exitCode) => new()
    {
        Progress = new() { State = state, Percentage = percentage },
        ShellIntegration = new() { Phase = phase, LastExitCode = exitCode }
    };

    private static async Task<JsonElement> ReadActivityFrameAsync(
        Hwt1PresentationAdapter view, Func<JsonElement, bool> predicate)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (true)
        {
            var frame = await view.ReadFrameAsync(timeout.Token);
            var length = BinaryPrimitives.ReadInt32LittleEndian(frame.Span[4..]);
            using var metadata = JsonDocument.Parse(frame.Slice(8, length));
            var root = metadata.RootElement;
            await view.HandleMessageAsync(Encoding.UTF8.GetBytes(
                $$"""{"type":"ack","revision":{{root.GetProperty("revision").GetUInt32()}}}"""), timeout.Token);
            if (predicate(root))
                return root.Clone();
        }
    }

    private static async Task WaitAsync(Hex1bTerminal terminal, Func<Hex1bTerminalSnapshot, bool> predicate)
    {
        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(predicate, Timeout, "authoritative HMP activity applied")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
    }

    private static async Task WriteSyncAsync(Stream stream, string ansi, Hmp1ActivityState activity)
    {
        await Hmp1Protocol.WriteFrameAsync(stream, Hmp1FrameType.StateSync,
            Encoding.UTF8.GetBytes(ansi), TestContext.Current.CancellationToken);
        await Hmp1Protocol.WriteActivityStateAsync(stream, activity, TestContext.Current.CancellationToken);
    }

    private static async Task HandshakeAsync(Stream stream, Hmp1WorkloadAdapter client, string ansi, Hmp1ActivityState activity)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        var connecting = client.ConnectAsync(timeout.Token);
        await Hmp1Protocol.ReadFrameAsync(stream, timeout.Token);
        await Hmp1Protocol.WriteHelloAsync(stream, 20, 5, "upstream", null, [], timeout.Token);
        await WriteSyncAsync(stream, ansi, activity);
        await connecting.WaitAsync(timeout.Token);
    }

    private static async Task<(Hmp1ClientHandle Handle, Hmp1WorkloadAdapter Client)> ConnectAsync(Hmp1PresentationAdapter server)
    {
        var streams = CreateStreams();
        var client = Hmp1TestHelpers.NewClient(streams.Client);
        using var timeout = new CancellationTokenSource(Timeout);
        var accepting = server.AddClient(streams.Server, TestContext.Current.CancellationToken);
        try
        {
            await client.ConnectAsync(timeout.Token);
            return (await accepting.WaitAsync(timeout.Token), client);
        }
        catch
        {
            await client.DisposeAsync();
            await streams.Server.DisposeAsync();
            throw;
        }
    }

    private static (Stream Server, Stream Client) CreateStreams()
    {
        var toClient = new Pipe();
        var toServer = new Pipe();
        return (new DuplexStream(toServer.Reader.AsStream(), toClient.Writer.AsStream()),
            new DuplexStream(toClient.Reader.AsStream(), toServer.Writer.AsStream()));
    }

    private class ReplayPresentationGate : IHex1bTerminalPresentationAdapter
    {
        private readonly HeadlessPresentationAdapter _headless = new(20, 5);
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Fail { get; set; }
        internal Action? Invalidated { get; set; }
        public int Width => _headless.Width;
        public int Height => _headless.Height;
        public TerminalCapabilities Capabilities => _headless.Capabilities;
        public event Action<int, int>? Resized { add => _headless.Resized += value; remove => _headless.Resized -= value; }
        public event Action? Disconnected { add => _headless.Disconnected += value; remove => _headless.Disconnected -= value; }
        public async ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(ct);
            if (Fail)
            {
                Fail = false;
                throw new InvalidOperationException("Replay presentation failed.");
            }
        }
        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default) => _headless.ReadInputAsync(ct);
        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public void InvalidatePresentation() => Invalidated?.Invoke();
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync() => _headless.DisposeAsync();
    }

    private sealed class ReplayImpactGate : ReplayPresentationGate, ICellImpactAwarePresentationAdapter
    {
        public ValueTask WriteOutputWithImpactsAsync(IReadOnlyList<AppliedToken> appliedTokens, CancellationToken ct = default)
            => WriteOutputAsync(ReadOnlyMemory<byte>.Empty, ct);
    }

    private sealed class FailingReplayFilter : IHex1bTerminalWorkloadFilter
    {
        public ValueTask OnOutputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => throw new InvalidOperationException("Replay workload filter failed.");
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnFrameCompleteAsync(TimeSpan elapsed, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default) => ValueTask.CompletedTask;
    }

    private sealed class DuplexStream(Stream input, Stream output) : Stream
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => input.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => input.ReadAsync(buffer, cancellationToken);
        public override void Write(byte[] buffer, int offset, int count) => output.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => output.WriteAsync(buffer, cancellationToken);
        public override void Flush() => output.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => output.FlushAsync(cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                input.Dispose();
                output.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
