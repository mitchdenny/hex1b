using System.Diagnostics;
using System.IO.Pipelines;
using Hex1b;

namespace Hex1b.Tests.Hmp1;

/// <summary>
/// Tests for the <see cref="Hmp1ClientOptions"/> callback surface plumbed
/// through <see cref="Hmp1BuilderExtensions.WithHmp1Stream(Hex1bTerminalBuilder, System.IO.Stream, System.Action{Hmp1ClientOptions}?)"/>,
/// plus the underlying <see cref="Hmp1WorkloadAdapter.OnConnected"/> and
/// <see cref="Hmp1WorkloadAdapter.OnRemoteResized"/> async callbacks.
/// </summary>
public class Hmp1ClientOptionsTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Adapter_Connected_FiresOnceWithHandshakeState()
    {
        var server = new Hmp1PresentationAdapter(120, 36);
        await using var serverDispose = (IAsyncDisposable)server;

        var (s1, s2) = CreateFullDuplexPair();

        var adapter = Hmp1TestHelpers.NewClient(s2, displayName: "viewer");
        await using var adapterDispose = adapter;

        var connectedCount = 0;
        Hmp1ConnectedEventArgs? captured = null;
        adapter.OnConnected += (e, _) =>
        {
            Interlocked.Increment(ref connectedCount);
            captured = e;
            return ValueTask.CompletedTask;
        };

        var addTask = server.AddClient(s1, CancellationToken.None);
        await adapter.ConnectAsync(CancellationToken.None);
        var handle = await addTask.WaitAsync(ShortTimeout);
        await using var handleDispose = handle;

        Assert.Equal(1, connectedCount);
        Assert.NotNull(captured);
        Assert.False(string.IsNullOrEmpty(captured!.PeerId));
        Assert.Null(captured.PrimaryPeerId);
        Assert.Equal(120, captured.Width);
        Assert.Equal(36, captured.Height);
        Assert.Empty(captured.Peers);
    }

    [Fact]
    public async Task Adapter_RemoteResized_FiresWhenLocalPrimaryRequestsResize()
    {
        var server = new Hmp1PresentationAdapter(80, 24);
        await using var serverDispose = (IAsyncDisposable)server;

        var (s1, s2) = CreateFullDuplexPair();

        var adapter = Hmp1TestHelpers.NewClient(s2, displayName: "primary");
        await using var adapterDispose = adapter;

        var resizes = new List<RemoteResizedEventArgs>();
        adapter.OnRemoteResized += (e, _) =>
        {
            lock (resizes) { resizes.Add(e); }
            return ValueTask.CompletedTask;
        };

        var addTask = server.AddClient(s1, CancellationToken.None);
        await adapter.ConnectAsync(CancellationToken.None);
        var handle = await addTask.WaitAsync(ShortTimeout);
        await using var handleDispose = handle;

        // Initial Hello dims must NOT have fired RemoteResized.
        lock (resizes) { Assert.Empty(resizes); }

        // Take primary at a new dim — RoleChange-driven resize from 80x24 to 110x35.
        await adapter.RequestPrimaryAsync(110, 35);
        await adapter.WaitForRoleAsync(primary: true, ShortTimeout, CancellationToken.None);

        await WaitUntilAsync(() => { lock (resizes) { return resizes.Count >= 1; } }, ShortTimeout, () => "RemoteResized did not fire after take-primary");

        RemoteResizedEventArgs first;
        lock (resizes) { first = resizes[^1]; }
        Assert.Equal(110, first.Width);
        Assert.Equal(35, first.Height);
        Assert.True(first.CausedByLocalPrimary, "Local peer was the new primary; CausedByLocalPrimary should be true.");
    }

    [Fact]
    public async Task Adapter_RemoteResized_FiresOnPureResizeFrameWhilePrimary()
    {
        var server = new Hmp1PresentationAdapter(80, 24);
        await using var serverDispose = (IAsyncDisposable)server;

        var (s1, s2) = CreateFullDuplexPair();

        var adapter = Hmp1TestHelpers.NewClient(s2, displayName: "primary");
        await using var adapterDispose = adapter;

        var resizes = new List<RemoteResizedEventArgs>();
        adapter.OnRemoteResized += (e, _) =>
        {
            lock (resizes) { resizes.Add(e); }
            return ValueTask.CompletedTask;
        };

        var addTask = server.AddClient(s1, CancellationToken.None);
        await adapter.ConnectAsync(CancellationToken.None);
        var handle = await addTask.WaitAsync(ShortTimeout);
        await using var handleDispose = handle;

        // Become primary first.
        await adapter.RequestPrimaryAsync(100, 30);
        await adapter.WaitForRoleAsync(primary: true, ShortTimeout, CancellationToken.None);

        // Wait for the take-over Resize to land before snapshotting count, so
        // the subsequent ResizeAsync's broadcast is the only new event.
        await WaitUntilAsync(() => { lock (resizes) { return resizes.Count >= 1; } }, ShortTimeout, () => "Initial take-over RemoteResized did not arrive");
        int baselineCount;
        lock (resizes) { baselineCount = resizes.Count; }

        // Now issue a pure ResizeAsync from primary; producer broadcasts a Resize frame back.
        await adapter.ResizeAsync(140, 45);

        await WaitUntilAsync(() => { lock (resizes) { return resizes.Count > baselineCount; } }, ShortTimeout, () => "RemoteResized did not fire after primary ResizeAsync");

        RemoteResizedEventArgs latest;
        lock (resizes) { latest = resizes[^1]; }
        Assert.Equal(140, latest.Width);
        Assert.Equal(45, latest.Height);
        Assert.True(latest.CausedByLocalPrimary, "Local peer is the primary; CausedByLocalPrimary should be true.");
    }

    [Fact]
    public async Task Adapter_RemoteResized_DoesNotFireWhenDimsUnchanged()
    {
        var server = new Hmp1PresentationAdapter(100, 30);
        await using var serverDispose = (IAsyncDisposable)server;

        var (s1, s2) = CreateFullDuplexPair();

        var adapter = Hmp1TestHelpers.NewClient(s2, displayName: "primary");
        await using var adapterDispose = adapter;

        var resizes = new List<RemoteResizedEventArgs>();
        adapter.OnRemoteResized += (e, _) =>
        {
            lock (resizes) { resizes.Add(e); }
            return ValueTask.CompletedTask;
        };

        var addTask = server.AddClient(s1, CancellationToken.None);
        await adapter.ConnectAsync(CancellationToken.None);
        var handle = await addTask.WaitAsync(ShortTimeout);
        await using var handleDispose = handle;

        // Take primary with the same dims as the producer already has.
        // The producer still sends a RoleChange (with same dims), but the
        // adapter must not raise RemoteResized because dims didn't change.
        await adapter.RequestPrimaryAsync(100, 30);
        await adapter.WaitForRoleAsync(primary: true, ShortTimeout, CancellationToken.None);

        // Give the read pump a beat to process any in-flight frames.
        await Task.Delay(100);

        lock (resizes) { Assert.Empty(resizes); }
    }

    [Fact]
    public async Task Builder_OptionsCallback_AllHooksFireForCorrespondingEvents()
    {
        var server = new Hmp1PresentationAdapter(80, 24);
        await using var serverDispose = (IAsyncDisposable)server;

        var (s1, s2) = CreateFullDuplexPair();

        var connectedCount = 0;
        var roleChangedCount = 0;
        var peerJoinedCount = 0;
        var peerLeftCount = 0;
        var remoteResizedCount = 0;
        var disconnectedCount = 0;

        Hmp1ConnectedEventArgs? capturedConnected = null;
        RemoteResizedEventArgs? lastRemoteResized = null;
        PeerJoinEventArgs? capturedPeerJoined = null;
        PeerLeaveEventArgs? capturedPeerLeft = null;

        var terminal = Hex1bTerminal.CreateBuilder()
            .WithHeadless()
            .WithHmp1Stream(s2, opt =>
            {
                opt.DisplayName = "viewer-via-options";
                opt.DefaultRole = Hmp1Role.Secondary;
                opt.OnConnected = (e, _) =>
                {
                    Interlocked.Increment(ref connectedCount);
                    capturedConnected = e;
                    return ValueTask.CompletedTask;
                };
                opt.OnRoleChanged = (_, _) => { Interlocked.Increment(ref roleChangedCount); return ValueTask.CompletedTask; };
                opt.OnPeerJoined = (e, _) =>
                {
                    Interlocked.Increment(ref peerJoinedCount);
                    capturedPeerJoined = e;
                    return ValueTask.CompletedTask;
                };
                opt.OnPeerLeft = (e, _) =>
                {
                    Interlocked.Increment(ref peerLeftCount);
                    capturedPeerLeft = e;
                    return ValueTask.CompletedTask;
                };
                opt.OnRemoteResized = (e, _) =>
                {
                    Interlocked.Increment(ref remoteResizedCount);
                    lastRemoteResized = e;
                    return ValueTask.CompletedTask;
                };
                opt.OnDisconnected = _ => { Interlocked.Increment(ref disconnectedCount); return ValueTask.CompletedTask; };
            })
            .Build();
        await using var terminalDispose = terminal;

        // First peer (target of the options callback).
        using var ctsRun = new CancellationTokenSource();
        var addTask = server.AddClient(s1, ctsRun.Token);
        var runTask = Task.Run(() => terminal.RunAsync(ctsRun.Token));
        var handle = await addTask.WaitAsync(ShortTimeout);
        await using var handleDispose = handle;

        await WaitUntilAsync(() => connectedCount == 1, ShortTimeout, () => "OnConnected never fired");
        Assert.NotNull(capturedConnected);
        Assert.False(string.IsNullOrEmpty(capturedConnected!.PeerId));
        Assert.Equal(80, capturedConnected.Width);
        Assert.Equal(24, capturedConnected.Height);

        // Add a second peer to drive PeerJoined.
        var (other1, other2) = CreateFullDuplexPair();
        var otherAdapter = Hmp1TestHelpers.NewClient(other2, displayName: "other-peer");
        await using var otherAdapterDispose = otherAdapter;
        var otherAddTask = server.AddClient(other1, CancellationToken.None);
        await otherAdapter.ConnectAsync(CancellationToken.None);
        var otherHandle = await otherAddTask.WaitAsync(ShortTimeout);

        await WaitUntilAsync(() => peerJoinedCount >= 1, ShortTimeout, () => "OnPeerJoined never fired");
        Assert.NotNull(capturedPeerJoined);
        Assert.Equal("other-peer", capturedPeerJoined!.DisplayName);

        // Have the other peer take primary at a new dim — drives OnRoleChanged
        // (for our viewer) and OnRemoteResized.
        await otherAdapter.RequestPrimaryAsync(120, 40);
        await otherAdapter.WaitForRoleAsync(primary: true, ShortTimeout, CancellationToken.None);

        await WaitUntilAsync(() => roleChangedCount >= 1, ShortTimeout, () => "OnRoleChanged never fired");
        await WaitUntilAsync(() => remoteResizedCount >= 1, ShortTimeout, () => "OnRemoteResized never fired");
        Assert.NotNull(lastRemoteResized);
        Assert.Equal(120, lastRemoteResized!.Width);
        Assert.Equal(40, lastRemoteResized.Height);
        Assert.False(lastRemoteResized.CausedByLocalPrimary, "Other peer caused the resize, not us.");

        // Disconnect the other peer to drive OnPeerLeft.
        await otherHandle.DisposeAsync();
        await otherAdapter.DisposeAsync();

        await WaitUntilAsync(() => peerLeftCount >= 1, ShortTimeout, () => "OnPeerLeft never fired");
        Assert.NotNull(capturedPeerLeft);

        // Tear down our terminal — drives OnDisconnected.
        await ctsRun.CancelAsync();
        try { await runTask.WaitAsync(ShortTimeout); } catch (OperationCanceledException) { }

        await WaitUntilAsync(() => disconnectedCount >= 1, ShortTimeout, () => "OnDisconnected never fired");
    }

    [Fact]
    public async Task Builder_OptionsCallback_NullHooksAreTolerated()
    {
        var server = new Hmp1PresentationAdapter(80, 24);
        await using var serverDispose = (IAsyncDisposable)server;

        var (s1, s2) = CreateFullDuplexPair();

        // Configure options but leave every hook null. Should not throw.
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithHeadless()
            .WithHmp1Stream(s2, opt =>
            {
                opt.DisplayName = "no-hooks";
            })
            .Build();
        await using var terminalDispose = terminal;

        using var ctsRun = new CancellationTokenSource();
        var addTask = server.AddClient(s1, ctsRun.Token);
        var runTask = Task.Run(() => terminal.RunAsync(ctsRun.Token));
        var handle = await addTask.WaitAsync(ShortTimeout);
        await using var handleDispose = handle;

        // No assertions on events — the goal is just to prove that calling
        // WithHmp1Stream(stream, opt => { ... }) with every event hook left
        // null doesn't throw during build, connect, or read-pump shutdown.
        Assert.NotNull(handle);

        await ctsRun.CancelAsync();
        try { await runTask.WaitAsync(ShortTimeout); } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task Builder_OptionsCallback_NullConfigureSucceeds()
    {
        var server = new Hmp1PresentationAdapter(80, 24);
        await using var serverDispose = (IAsyncDisposable)server;

        var (s1, s2) = CreateFullDuplexPair();

        // Pass a null configure delegate — same as the no-arg overload.
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithHeadless()
            .WithHmp1Stream(s2, configure: null)
            .Build();
        await using var terminalDispose = terminal;

        using var ctsRun = new CancellationTokenSource();
        var addTask = server.AddClient(s1, ctsRun.Token);
        var runTask = Task.Run(() => terminal.RunAsync(ctsRun.Token));
        var handle = await addTask.WaitAsync(ShortTimeout);
        await using var handleDispose = handle;

        Assert.NotNull(handle);

        await ctsRun.CancelAsync();
        try { await runTask.WaitAsync(ShortTimeout); } catch (OperationCanceledException) { }
    }

    // ---- Test helpers ------------------------------------------------------

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout, Func<string> describeFailure)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (predicate()) return;
            await Task.Delay(10);
        }
        throw new Xunit.Sdk.XunitException($"Timed out waiting: {describeFailure()}");
    }

    private static (Stream S1, Stream S2) CreateFullDuplexPair()
    {
        var p12 = new Pipe();
        var p21 = new Pipe();
        return (
            new DuplexPipeStream(p21.Reader.AsStream(), p12.Writer.AsStream()),
            new DuplexPipeStream(p12.Reader.AsStream(), p21.Writer.AsStream()));
    }

    private sealed class DuplexPipeStream(Stream readStream, Stream writeStream) : Stream
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => writeStream.Flush();
        public override Task FlushAsync(CancellationToken ct) => writeStream.FlushAsync(ct);
        public override int Read(byte[] buffer, int offset, int count) => readStream.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => readStream.ReadAsync(buffer, ct);
        public override void Write(byte[] buffer, int offset, int count) => writeStream.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => writeStream.WriteAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { readStream.Dispose(); } catch { }
                try { writeStream.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
