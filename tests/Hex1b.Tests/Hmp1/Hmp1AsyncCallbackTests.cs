using System.Diagnostics;
using System.IO.Pipelines;
using Hex1b;

namespace Hex1b.Tests.Hmp1;

/// <summary>
/// Tests for the Phase 16 async-callback API: multicast composition,
/// self-dispose from a callback (no deadlock), and DisconnectedTask
/// independence from slow user OnDisconnected handlers.
/// </summary>
public class Hmp1AsyncCallbackTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task OnPeerJoined_Multicast_AllHandlersRunInOrder_EvenWhenFirstThrows()
    {
        var server = new Hmp1PresentationAdapter(80, 24);
        await using var serverDispose = (IAsyncDisposable)server;

        var (s1, s2) = CreateFullDuplexPair();
        var primary = Hmp1TestHelpers.NewClient(s2, displayName: "primary");
        await using var primaryDispose = primary;

        var calls = new List<string>();
        primary.OnPeerJoined += (e, _) =>
        {
            lock (calls) calls.Add($"first:{e.DisplayName}");
            throw new InvalidOperationException("boom");
        };
        primary.OnPeerJoined += (e, _) =>
        {
            lock (calls) calls.Add($"second:{e.DisplayName}");
            return Task.CompletedTask;
        };

        var addPrimary = server.AddClient(s1, CancellationToken.None);
        await primary.ConnectAsync(CancellationToken.None);
        var primaryHandle = await addPrimary.WaitAsync(ShortTimeout);
        await using var primaryHandleDispose = primaryHandle;

        // Add a second peer to drive PeerJoined on the primary.
        var (other1, other2) = CreateFullDuplexPair();
        var other = Hmp1TestHelpers.NewClient(other2, displayName: "joiner");
        await using var otherDispose = other;
        var addOther = server.AddClient(other1, CancellationToken.None);
        await other.ConnectAsync(CancellationToken.None);
        var otherHandle = await addOther.WaitAsync(ShortTimeout);
        await using var otherHandleDispose = otherHandle;

        await WaitUntilAsync(() => { lock (calls) return calls.Count >= 2; }, ShortTimeout,
            () => $"only saw {calls.Count} of 2 expected callback invocations: [{string.Join(",", calls)}]");

        lock (calls)
        {
            Assert.Equal(2, calls.Count);
            Assert.Equal("first:joiner", calls[0]);
            Assert.Equal("second:joiner", calls[1]);
        }
    }

    [Fact]
    public async Task OnRoleChanged_SelfDisposeFromCallback_DoesNotDeadlock()
    {
        var server = new Hmp1PresentationAdapter(80, 24);
        await using var serverDispose = (IAsyncDisposable)server;

        var (s1, s2) = CreateFullDuplexPair();
        var adapter = Hmp1TestHelpers.NewClient(s2, displayName: "self-disposer", defaultRole: Hmp1Role.Secondary);

        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.OnRoleChanged = async (_, _) =>
        {
            // This is the deadlock-prone pattern: dispose the adapter from
            // inside its own read-pump callback. The Hmp1CallbackContext
            // AsyncLocal guard should let DisposeAsync skip the
            // await _readTask step that would otherwise wait on the
            // currently-running pump.
            await adapter.DisposeAsync();
            disposed.TrySetResult();
        };

        var addTask = server.AddClient(s1, CancellationToken.None);
        await adapter.ConnectAsync(CancellationToken.None);
        var handle = await addTask.WaitAsync(ShortTimeout);
        await using var handleDispose = handle;

        // Becoming primary triggers a RoleChange broadcast that arrives at
        // the read pump and invokes OnRoleChanged.
        await adapter.RequestPrimaryAsync(100, 30);

        var completed = await Task.WhenAny(disposed.Task, Task.Delay(ShortTimeout));
        Assert.Same(disposed.Task, completed);
    }

    [Fact]
    public async Task DisconnectedTask_CompletesBeforeSlowOnDisconnectedReturns()
    {
        var server = new Hmp1PresentationAdapter(80, 24);
        await using var serverDispose = (IAsyncDisposable)server;

        var (s1, s2) = CreateFullDuplexPair();

        var slowGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var adapter = new Hmp1WorkloadAdapter(new Hmp1ClientOptions
        {
            StreamFactory = _ => Task.FromResult<Stream>(s2),
            DisplayName = "slow-disconnect",
            OnDisconnected = async _ =>
            {
                handlerEntered.TrySetResult();
                await slowGate.Task;
            },
        });

        var addTask = server.AddClient(s1, CancellationToken.None);
        await adapter.ConnectAsync(CancellationToken.None);
        var handle = await addTask.WaitAsync(ShortTimeout);

        // Tear down the server side to provoke a disconnect.
        await handle.DisposeAsync();

        // The handler must enter (proves transport saw EOF) and the
        // DisconnectedTask must complete BEFORE we release the slow
        // handler — this is the contract the Hmp1BuilderExtensions
        // runCallback relies on.
        await handlerEntered.Task.WaitAsync(ShortTimeout);
        await adapter.DisconnectedTask.WaitAsync(ShortTimeout);
        Assert.False(slowGate.Task.IsCompleted, "Sanity: slow handler is still gated.");

        // Release the handler and finish disposal.
        slowGate.TrySetResult();
        await adapter.DisposeAsync();
    }

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
