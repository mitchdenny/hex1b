using System.IO.Pipelines;
using Hex1b;

namespace Hex1b.Tests.Hmp1;

/// <summary>
/// Tier 2 producer state-machine tests for the HMP1 multi-head primary/secondary
/// protocol. Wires raw HMP1 frames against a single in-process
/// <see cref="Hmp1PresentationAdapter"/> via real <see cref="Hmp1WorkloadAdapter"/>
/// clients; asserts state transitions are observed correctly across all peers.
/// </summary>
public class Hmp1MultiHeadTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task FirstPeer_AttachesAsSecondary_NoPrimary()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle, client) = await ConnectAsync(server, displayName: "first");

        Assert.False(client.IsPrimary);
        Assert.Null(client.PrimaryPeerId);
        Assert.Null(server.PrimaryPeerId);
        Assert.NotEmpty(client.PeerId);

        await handle.DisposeAsync();
        await client.DisposeAsync();
    }

    [Fact]
    public async Task SecondaryResize_IsDropped_PtyUnchanged()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle, client) = await ConnectAsync(server);

        var resized = false;
        server.Resized += (_, _) => resized = true;

        await client.ResizeAsync(120, 40);

        // Give the producer a moment to (not) act on it.
        await Task.Delay(150);

        Assert.False(resized, "Producer must not fire Resized for a non-primary peer's Resize.");
        Assert.Equal(80, server.Width);
        Assert.Equal(24, server.Height);

        await handle.DisposeAsync();
        await client.DisposeAsync();
    }

    [Fact]
    public async Task RequestPrimary_MakesCallerPrimary_ResizesPty()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle, client) = await ConnectAsync(server);

        var resizeTcs = new TaskCompletionSource<(int, int)>();
        server.Resized += (w, h) => resizeTcs.TrySetResult((w, h));

        await client.RequestPrimaryAsync(120, 40);
        var ok = await client.WaitForRoleAsync(primary: true, TestTimeout, CancellationToken.None);
        Assert.True(ok, "Expected to become primary within timeout.");

        var (w, h) = await resizeTcs.Task.WaitAsync(TestTimeout);
        Assert.Equal(120, w);
        Assert.Equal(40, h);
        Assert.Equal(client.PeerId, server.PrimaryPeerId);
        Assert.True(client.IsPrimary);

        await handle.DisposeAsync();
        await client.DisposeAsync();
    }

    [Fact]
    public async Task SecondPeer_Joins_FirstPrimarySeesPeerJoin()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle1, client1) = await ConnectAsync(server, displayName: "alpha");
        await client1.RequestPrimaryAsync(100, 30);
        await client1.WaitForRoleAsync(primary: true, TestTimeout, CancellationToken.None);

        var joinTcs = new TaskCompletionSource<PeerJoinEventArgs>();
        client1.PeerJoined += (_, e) => joinTcs.TrySetResult(e);

        var (handle2, client2) = await ConnectAsync(server, displayName: "beta");

        var join = await joinTcs.Task.WaitAsync(TestTimeout);
        Assert.Equal(client2.PeerId, join.PeerId);
        Assert.Equal("beta", join.DisplayName);

        // The newcomer sees the existing primary in its initial Hello roster + state.
        Assert.Equal(client1.PeerId, client2.PrimaryPeerId);
        Assert.False(client2.IsPrimary);
        Assert.Single(client2.Peers);
        Assert.Equal(client1.PeerId, client2.Peers[0].PeerId);

        await handle1.DisposeAsync();
        await handle2.DisposeAsync();
        await client1.DisposeAsync();
        await client2.DisposeAsync();
    }

    [Fact]
    public async Task SecondPeer_RequestsPrimary_RoleTransfersAcrossBothPeers()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle1, client1) = await ConnectAsync(server, displayName: "alpha");
        await client1.RequestPrimaryAsync(100, 30);
        await client1.WaitForRoleAsync(primary: true, TestTimeout, CancellationToken.None);

        var (handle2, client2) = await ConnectAsync(server, displayName: "beta");

        var role1Tcs = new TaskCompletionSource<RoleChangedEventArgs>();
        var role2Tcs = new TaskCompletionSource<RoleChangedEventArgs>();
        client1.RoleChanged += (_, e) => role1Tcs.TrySetResult(e);
        client2.RoleChanged += (_, e) => role2Tcs.TrySetResult(e);

        await client2.RequestPrimaryAsync(140, 50);

        var r1 = await role1Tcs.Task.WaitAsync(TestTimeout);
        var r2 = await role2Tcs.Task.WaitAsync(TestTimeout);

        Assert.Equal(client2.PeerId, r1.PrimaryPeerId);
        Assert.Equal(client2.PeerId, r2.PrimaryPeerId);
        Assert.Equal(140, r1.Width);
        Assert.Equal(50, r1.Height);
        Assert.True(r1.PreviouslyPrimary);
        Assert.False(r1.NowPrimary);
        Assert.False(r2.PreviouslyPrimary);
        Assert.True(r2.NowPrimary);

        Assert.False(client1.IsPrimary);
        Assert.True(client2.IsPrimary);
        Assert.Equal(client2.PeerId, server.PrimaryPeerId);
        Assert.Equal(140, server.Width);
        Assert.Equal(50, server.Height);

        await handle1.DisposeAsync();
        await handle2.DisposeAsync();
        await client1.DisposeAsync();
        await client2.DisposeAsync();
    }

    [Fact]
    public async Task PrimaryDisconnect_BroadcastsRoleChangeNullNoAutoPromotion()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle1, client1) = await ConnectAsync(server, displayName: "alpha");
        await client1.RequestPrimaryAsync(100, 30);
        await client1.WaitForRoleAsync(primary: true, TestTimeout, CancellationToken.None);

        var (handle2, client2) = await ConnectAsync(server, displayName: "beta");

        var roleChangedTcs = new TaskCompletionSource<RoleChangedEventArgs>();
        var leaveTcs = new TaskCompletionSource<PeerLeaveEventArgs>();
        client2.RoleChanged += (_, e) => roleChangedTcs.TrySetResult(e);
        client2.PeerLeft += (_, e) => leaveTcs.TrySetResult(e);

        await handle1.DisposeAsync();
        await client1.DisposeAsync();

        var roleChange = await roleChangedTcs.Task.WaitAsync(TestTimeout);
        var leave = await leaveTcs.Task.WaitAsync(TestTimeout);

        Assert.Null(roleChange.PrimaryPeerId);
        Assert.Equal("PrimaryDisconnected", roleChange.Reason);
        Assert.Equal(client1.PeerId, leave.PeerId);

        Assert.Null(server.PrimaryPeerId);
        Assert.False(client2.IsPrimary);
        // PTY size remains at last-known (no auto-promotion).
        Assert.Equal(100, server.Width);
        Assert.Equal(30, server.Height);

        await handle2.DisposeAsync();
        await client2.DisposeAsync();
    }

    [Fact]
    public async Task SecondaryDisconnect_NoRoleChange_OtherPeersUnaffected()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle1, client1) = await ConnectAsync(server, displayName: "primary");
        await client1.RequestPrimaryAsync(100, 30);
        await client1.WaitForRoleAsync(primary: true, TestTimeout, CancellationToken.None);

        var (handle2, client2) = await ConnectAsync(server, displayName: "secondary");

        var roleChanged = false;
        client1.RoleChanged += (_, _) => roleChanged = true;
        var leaveTcs = new TaskCompletionSource<PeerLeaveEventArgs>();
        client1.PeerLeft += (_, e) => leaveTcs.TrySetResult(e);

        await handle2.DisposeAsync();
        await client2.DisposeAsync();

        var leave = await leaveTcs.Task.WaitAsync(TestTimeout);
        Assert.Equal(client2.PeerId, leave.PeerId);

        // Give events a moment to settle then verify no role change.
        await Task.Delay(100);
        Assert.False(roleChanged, "Primary role change must not fire on secondary disconnect.");
        Assert.True(client1.IsPrimary);
        Assert.Equal(client1.PeerId, server.PrimaryPeerId);

        await handle1.DisposeAsync();
        await client1.DisposeAsync();
    }

    [Fact]
    public async Task RequestPrimary_WithZeroDims_FallsBackToLastKnownSize()
    {
        await using var server = new Hmp1PresentationAdapter(80, 24);
        var (handle, client) = await ConnectAsync(server);

        // Caller passes 0,0 — producer should use last-remembered dims (which are
        // the producer's defaults until a Resize comes in).
        await client.RequestPrimaryAsync(0, 0);
        await client.WaitForRoleAsync(primary: true, TestTimeout, CancellationToken.None);

        Assert.Equal(80, server.Width);
        Assert.Equal(24, server.Height);

        await handle.DisposeAsync();
        await client.DisposeAsync();
    }

    [Fact]
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
        Assert.Equal(160, w);
        Assert.Equal(50, h);
        Assert.Equal(160, server.Width);
        Assert.Equal(50, server.Height);

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
        string? defaultRole = null,
        CancellationToken ct = default)
    {
        var (s1, s2) = CreateFullDuplexPair();
        var client = new Hmp1WorkloadAdapter(s2, displayName, defaultRole);
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
