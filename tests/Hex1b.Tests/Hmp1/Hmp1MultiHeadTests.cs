using System.IO.Pipelines;
using Hex1b;

namespace Hex1b.Tests.Hmp1;

/// <summary>
/// Tier 2 producer state-machine tests for the HMP1 multi-head primary/secondary
/// protocol. Wires raw HMP1 frames against a single in-process
/// <see cref="Hmp1PresentationAdapter"/> via real <see cref="Hmp1WorkloadAdapter"/>
/// clients; asserts state transitions are observed correctly across all peers.
/// </summary>
[TestClass]
public class Hmp1MultiHeadTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task FirstPeer_AttachesAsSecondary_NoPrimary()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle, client) = await ConnectAsync(server, displayName: "first");

        Assert.IsFalse(client.IsPrimary);
        Assert.IsNull(client.PrimaryPeerId);
        Assert.IsNull(server.PrimaryPeerId);
        Assert.IsNotEmpty(client.PeerId);

        await handle.DisposeAsync();
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task SecondaryResize_IsDropped_PtyUnchanged()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle, client) = await ConnectAsync(server);

        var resized = false;
        server.Resized += (_, _) => resized = true;

        await client.ResizeAsync(120, 40);

        // Give the producer a moment to (not) act on it.
        await Task.Delay(150);

        Assert.IsFalse(resized, "Producer must not fire Resized for a non-primary peer's Resize.");
        Assert.AreEqual(80, server.Width);
        Assert.AreEqual(24, server.Height);

        await handle.DisposeAsync();
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task RequestPrimary_MakesCallerPrimary_ResizesPty()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle, client) = await ConnectAsync(server);

        var resizeTcs = new TaskCompletionSource<(int, int)>();
        server.Resized += (w, h) => resizeTcs.TrySetResult((w, h));

        await client.RequestPrimaryAsync(120, 40);
        var ok = await client.WaitForRoleAsync(primary: true, TestTimeout, CancellationToken.None);
        Assert.IsTrue(ok, "Expected to become primary within timeout.");

        var (w, h) = await resizeTcs.Task.WaitAsync(TestTimeout);
        Assert.AreEqual(120, w);
        Assert.AreEqual(40, h);
        Assert.AreEqual(client.PeerId, server.PrimaryPeerId);
        Assert.IsTrue(client.IsPrimary);

        await handle.DisposeAsync();
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task SecondPeer_Joins_FirstPrimarySeesPeerJoin()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle1, client1) = await ConnectAsync(server, displayName: "alpha");
        await client1.RequestPrimaryAsync(100, 30);
        await client1.WaitForRoleAsync(primary: true, TestTimeout, CancellationToken.None);

        var joinTcs = new TaskCompletionSource<PeerJoinEventArgs>();
        client1.OnPeerJoined += (e, _) => { joinTcs.TrySetResult(e); return Task.CompletedTask; };

        var (handle2, client2) = await ConnectAsync(server, displayName: "beta");

        var join = await joinTcs.Task.WaitAsync(TestTimeout);
        Assert.AreEqual(client2.PeerId, join.PeerId);
        Assert.AreEqual("beta", join.DisplayName);

        // The newcomer sees the existing primary in its initial Hello roster + state.
        Assert.AreEqual(client1.PeerId, client2.PrimaryPeerId);
        Assert.IsFalse(client2.IsPrimary);
        TestSeq.Single(client2.Peers);
        Assert.AreEqual(client1.PeerId, client2.Peers[0].PeerId);

        await handle1.DisposeAsync();
        await handle2.DisposeAsync();
        await client1.DisposeAsync();
        await client2.DisposeAsync();
    }

    [TestMethod]
    public async Task SecondPeer_RequestsPrimary_RoleTransfersAcrossBothPeers()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle1, client1) = await ConnectAsync(server, displayName: "alpha");
        await client1.RequestPrimaryAsync(100, 30);
        await client1.WaitForRoleAsync(primary: true, TestTimeout, CancellationToken.None);

        var (handle2, client2) = await ConnectAsync(server, displayName: "beta");

        var role1Tcs = new TaskCompletionSource<RoleChangedEventArgs>();
        var role2Tcs = new TaskCompletionSource<RoleChangedEventArgs>();
        client1.OnRoleChanged += (e, _) => { role1Tcs.TrySetResult(e); return Task.CompletedTask; };
        client2.OnRoleChanged += (e, _) => { role2Tcs.TrySetResult(e); return Task.CompletedTask; };

        await client2.RequestPrimaryAsync(140, 50);

        var r1 = await role1Tcs.Task.WaitAsync(TestTimeout);
        var r2 = await role2Tcs.Task.WaitAsync(TestTimeout);

        Assert.AreEqual(client2.PeerId, r1.PrimaryPeerId);
        Assert.AreEqual(client2.PeerId, r2.PrimaryPeerId);
        Assert.AreEqual(140, r1.Width);
        Assert.AreEqual(50, r1.Height);
        Assert.IsTrue(r1.PreviouslyPrimary);
        Assert.IsFalse(r1.NowPrimary);
        Assert.IsFalse(r2.PreviouslyPrimary);
        Assert.IsTrue(r2.NowPrimary);

        Assert.IsFalse(client1.IsPrimary);
        Assert.IsTrue(client2.IsPrimary);
        Assert.AreEqual(client2.PeerId, server.PrimaryPeerId);
        Assert.AreEqual(140, server.Width);
        Assert.AreEqual(50, server.Height);

        await handle1.DisposeAsync();
        await handle2.DisposeAsync();
        await client1.DisposeAsync();
        await client2.DisposeAsync();
    }

    [TestMethod]
    public async Task PrimaryDisconnect_BroadcastsRoleChangeNullNoAutoPromotion()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle1, client1) = await ConnectAsync(server, displayName: "alpha");
        await client1.RequestPrimaryAsync(100, 30);
        await client1.WaitForRoleAsync(primary: true, TestTimeout, CancellationToken.None);

        var (handle2, client2) = await ConnectAsync(server, displayName: "beta");

        var roleChangedTcs = new TaskCompletionSource<RoleChangedEventArgs>();
        var leaveTcs = new TaskCompletionSource<PeerLeaveEventArgs>();
        client2.OnRoleChanged += (e, _) => { roleChangedTcs.TrySetResult(e); return Task.CompletedTask; };
        client2.OnPeerLeft += (e, _) => { leaveTcs.TrySetResult(e); return Task.CompletedTask; };

        await handle1.DisposeAsync();
        await client1.DisposeAsync();

        var roleChange = await roleChangedTcs.Task.WaitAsync(TestTimeout);
        var leave = await leaveTcs.Task.WaitAsync(TestTimeout);

        Assert.IsNull(roleChange.PrimaryPeerId);
        Assert.AreEqual("PrimaryDisconnected", roleChange.Reason);
        Assert.AreEqual(client1.PeerId, leave.PeerId);

        Assert.IsNull(server.PrimaryPeerId);
        Assert.IsFalse(client2.IsPrimary);
        // PTY size remains at last-known (no auto-promotion).
        Assert.AreEqual(100, server.Width);
        Assert.AreEqual(30, server.Height);

        await handle2.DisposeAsync();
        await client2.DisposeAsync();
    }

    [TestMethod]
    public async Task SecondaryDisconnect_NoRoleChange_OtherPeersUnaffected()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle1, client1) = await ConnectAsync(server, displayName: "primary");
        await client1.RequestPrimaryAsync(100, 30);
        await client1.WaitForRoleAsync(primary: true, TestTimeout, CancellationToken.None);

        var (handle2, client2) = await ConnectAsync(server, displayName: "secondary");

        var roleChanged = false;
        client1.OnRoleChanged += (_, _) => { roleChanged = true; return Task.CompletedTask; };
        var leaveTcs = new TaskCompletionSource<PeerLeaveEventArgs>();
        client1.OnPeerLeft += (e, _) => { leaveTcs.TrySetResult(e); return Task.CompletedTask; };

        await handle2.DisposeAsync();
        await client2.DisposeAsync();

        var leave = await leaveTcs.Task.WaitAsync(TestTimeout);
        Assert.AreEqual(client2.PeerId, leave.PeerId);

        // Give events a moment to settle then verify no role change.
        await Task.Delay(100);
        Assert.IsFalse(roleChanged, "Primary role change must not fire on secondary disconnect.");
        Assert.IsTrue(client1.IsPrimary);
        Assert.AreEqual(client1.PeerId, server.PrimaryPeerId);

        await handle1.DisposeAsync();
        await client1.DisposeAsync();
    }

    [TestMethod]
    public async Task RequestPrimary_WithZeroDims_FallsBackToLastKnownSize()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle, client) = await ConnectAsync(server);

        // Caller passes 0,0 — producer should use last-remembered dims (which are
        // the producer's defaults until a Resize comes in).
        await client.RequestPrimaryAsync(0, 0);
        await client.WaitForRoleAsync(primary: true, TestTimeout, CancellationToken.None);

        Assert.AreEqual(80, server.Width);
        Assert.AreEqual(24, server.Height);

        await handle.DisposeAsync();
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task PrimaryResize_BroadcastsAndUpdatesPty()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle1, client1) = await ConnectAsync(server);
        await client1.RequestPrimaryAsync(90, 25);
        await client1.WaitForRoleAsync(primary: true, TestTimeout, CancellationToken.None);

        var (handle2, _client2) = await ConnectAsync(server);

        var resizeTcs = new TaskCompletionSource<(int, int)>();
        server.Resized += (w, h) => resizeTcs.TrySetResult((w, h));

        await client1.ResizeAsync(160, 50);

        var (w, h) = await resizeTcs.Task.WaitAsync(TestTimeout);
        Assert.AreEqual(160, w);
        Assert.AreEqual(50, h);
        Assert.AreEqual(160, server.Width);
        Assert.AreEqual(50, server.Height);

        await handle1.DisposeAsync();
        await handle2.DisposeAsync();
        await client1.DisposeAsync();
        await _client2.DisposeAsync();
    }

    /// <summary>
    /// Drives the new ClientHello-first handshake. The producer's <c>AddClient</c>
    /// blocks reading <see cref="Hmp1FrameType.ClientHello"/> from the client
    /// stream, and the workload adapter's <c>ConnectAsync</c> writes ClientHello
    /// then reads Hello + StateSync. They must therefore run concurrently.
    /// </summary>
    private static async Task<(Hmp1ClientHandle Handle, Hmp1WorkloadAdapter Client)> ConnectAsync(
        Hmp1PresentationAdapter server,
        string? displayName = null,
        Hmp1Role? defaultRole = null,
        CancellationToken ct = default)
    {
        var (s1, s2) = CreateFullDuplexPair();
        var client = new Hmp1WorkloadAdapter(new Hmp1ClientOptions
        {
            StreamFactory = _ => Task.FromResult<Stream>(s2),
            DisplayName = displayName,
            DefaultRole = defaultRole,
        });
        var addTask = server.AddClient(s1, ct);
        var connectTask = client.ConnectAsync(ct);
        var handle = await addTask.ConfigureAwait(false);
        await connectTask.ConfigureAwait(false);
        return (handle, client);
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
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
            => readStream.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
            => readStream.ReadAsync(buffer, ct);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => readStream.ReadAsync(buffer, offset, count, ct);

        public override void Write(byte[] buffer, int offset, int count)
            => writeStream.Write(buffer, offset, count);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
            => writeStream.WriteAsync(buffer, ct);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => writeStream.WriteAsync(buffer, offset, count, ct);

        public override void Flush() => writeStream.Flush();

        public override Task FlushAsync(CancellationToken ct) => writeStream.FlushAsync(ct);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                readStream.Dispose();
                writeStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
