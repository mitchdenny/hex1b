using System.Text;
using System.Threading.Channels;
using Hex1b;
using Hex1b.Testing;

namespace Hex1b.Tests;

[TestClass]
public class PlaceholderWorkloadAdapterTests
{
    [TestMethod]
    public async Task ReadOutputAsync_StartsFromPlaceholder_BeforePrimaryConnects()
    {
        var primary = new FakeConnectableAdapter();
        var placeholder = new FakeAdapter();
        placeholder.EnqueueOutput("PLACEHOLDER"u8.ToArray());

        await using var adapter = new PlaceholderWorkloadAdapter(
            primary, placeholder, PlaceholderResumePolicy.OnDisconnect);

        var data = await adapter.ReadOutputAsync(TestCancel());
        Assert.AreEqual("PLACEHOLDER", Encoding.UTF8.GetString(data.Span));
        Assert.AreSame(placeholder, adapter.ActiveChild);
    }

    [TestMethod]
    public async Task ReadOutputAsync_OnPrimaryConnected_PrependsResetThenStreamsPrimary()
    {
        var primary = new FakeConnectableAdapter();
        var placeholder = new FakeAdapter();
        primary.EnqueueOutput("PRIMARY"u8.ToArray());

        await using var adapter = new PlaceholderWorkloadAdapter(
            primary, placeholder, PlaceholderResumePolicy.OnDisconnect);

        primary.SignalConnected();

        // First read after swap should be the reset sequence: ESC c + ESC[?1049l + ESC[2J + ESC[H.
        var first = await adapter.ReadOutputAsync(TestCancel());
        AssertContainsSequence(first, new byte[] { 0x1b, (byte)'c' });
        AssertContainsSequence(first, "\x1b[?1049l"u8);

        var second = await adapter.ReadOutputAsync(TestCancel());
        TestSeq.AreEqual("PRIMARY"u8.ToArray(), second.ToArray());
        Assert.AreSame(primary, adapter.ActiveChild);
    }

    [TestMethod]
    public async Task ReadOutputAsync_OnPrimaryDisconnect_ReturnsToPlaceholder_WhenPolicyOnDisconnect()
    {
        var primary = new FakeConnectableAdapter();
        var placeholder = new FakeAdapter();
        primary.EnqueueOutput("HI"u8.ToArray());

        await using var adapter = new PlaceholderWorkloadAdapter(
            primary, placeholder, PlaceholderResumePolicy.OnDisconnect);

        primary.SignalConnected();
        _ = await adapter.ReadOutputAsync(TestCancel()); // reset
        _ = await adapter.ReadOutputAsync(TestCancel()); // "HI"

        primary.SignalDisconnected();
        // Reset for swap-back to placeholder.
        var resetBack = await adapter.ReadOutputAsync(TestCancel());
        AssertContainsSequence(resetBack, new byte[] { 0x1b, (byte)'c' });

        // Enqueue placeholder content only after the swap-back so the read
        // above can't race-consume it from the placeholder channel before
        // the swap completed.
        placeholder.EnqueueOutput("BACK"u8.ToArray());
        var placeholderAgain = await adapter.ReadOutputAsync(TestCancel());
        TestSeq.AreEqual("BACK"u8.ToArray(), placeholderAgain.ToArray());
        Assert.AreSame(placeholder, adapter.ActiveChild);
    }

    [TestMethod]
    public async Task ReadOutputAsync_OneShotPolicy_StopsOnPrimaryDisconnect()
    {
        var primary = new FakeConnectableAdapter();
        var placeholder = new FakeAdapter();
        primary.EnqueueOutput("HI"u8.ToArray());

        await using var adapter = new PlaceholderWorkloadAdapter(
            primary, placeholder, PlaceholderResumePolicy.OneShot);

        var disconnected = false;
        adapter.Disconnected += () => disconnected = true;

        primary.SignalConnected();
        _ = await adapter.ReadOutputAsync(TestCancel()); // reset
        _ = await adapter.ReadOutputAsync(TestCancel()); // "HI"
        primary.CompleteOutput();
        primary.SignalDisconnected();

        var trailing = await adapter.ReadOutputAsync(TestCancel());
        Assert.IsTrue(trailing.IsEmpty);
        Assert.IsTrue(disconnected);
    }

    [TestMethod]
    public async Task WriteInputAsync_OnlyDeliveredToActiveChild()
    {
        var primary = new FakeConnectableAdapter();
        var placeholder = new FakeAdapter();

        await using var adapter = new PlaceholderWorkloadAdapter(
            primary, placeholder, PlaceholderResumePolicy.OnDisconnect);

        await adapter.WriteInputAsync(new byte[] { 1 });
        Assert.AreEqual(1, placeholder.WrittenInputCount);
        Assert.AreEqual(0, primary.WrittenInputCount);

        primary.EnqueueOutput("X"u8.ToArray());
        primary.SignalConnected();
        _ = await adapter.ReadOutputAsync(TestCancel()); // reset
        _ = await adapter.ReadOutputAsync(TestCancel()); // X — confirms swap

        await adapter.WriteInputAsync(new byte[] { 2 });
        Assert.AreEqual(1, placeholder.WrittenInputCount);
        Assert.AreEqual(1, primary.WrittenInputCount);
    }

    [TestMethod]
    public async Task ResizeAsync_FansOutToBothChildren()
    {
        var primary = new FakeConnectableAdapter();
        var placeholder = new FakeAdapter();
        await using var adapter = new PlaceholderWorkloadAdapter(
            primary, placeholder, PlaceholderResumePolicy.OnDisconnect);

        await adapter.ResizeAsync(100, 40);
        Assert.AreEqual((100, 40), placeholder.LastResize);
        Assert.AreEqual((100, 40), primary.LastResize);
    }

    [TestMethod]
    public async Task SwapToRepaintable_InvokesRequestFullRepaint()
    {
        var primary = new RepaintablePrimary();
        var placeholder = new FakeAdapter();
        await using var adapter = new PlaceholderWorkloadAdapter(
            primary, placeholder, PlaceholderResumePolicy.OnDisconnect);

        primary.EnqueueOutput("X"u8.ToArray());
        primary.SignalConnected();
        _ = await adapter.ReadOutputAsync(TestCancel()); // reset (and triggers repaint)

        Assert.AreEqual(1, primary.RepaintRequests);
    }

    [TestMethod]
    public async Task ResizeAsync_RequestsRepaintOnRepaintableChildren()
    {
        var primary = new RepaintablePrimary();
        var placeholder = new RepaintablePlaceholder();
        await using var adapter = new PlaceholderWorkloadAdapter(
            primary, placeholder, PlaceholderResumePolicy.OnDisconnect);

        await adapter.ResizeAsync(120, 30);

        Assert.AreEqual(1, placeholder.RepaintRequests);
        Assert.AreEqual(1, primary.RepaintRequests);
    }

    [TestMethod]
    public async Task PlaceholderRunCallback_IsInvoked_WhenSupplied()
    {
        var primary = new FakeConnectableAdapter();
        var placeholder = new FakeAdapter();
        var ranTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Func<CancellationToken, Task<int>> placeholderRun = async ct =>
        {
            ranTcs.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException) { }
            return 0;
        };

        await using (var adapter = new PlaceholderWorkloadAdapter(
            primary, placeholder, placeholderRun, PlaceholderResumePolicy.OnDisconnect))
        {
            // Run callback should fire shortly after construction.
            await ranTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static CancellationToken TestCancel() =>
        new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;

    private static void AssertContainsSequence(ReadOnlyMemory<byte> haystack, ReadOnlySpan<byte> needle)
    {
        var hs = haystack.ToArray();
        for (var i = 0; i + needle.Length <= hs.Length; i++)
        {
            if (hs.AsSpan(i, needle.Length).SequenceEqual(needle)) return;
        }
        Assert.Fail($"Byte sequence not found. Haystack ({hs.Length} bytes): " +
            string.Join(' ', hs.Select(b => b.ToString("x2"))) +
            $"; Needle ({needle.Length} bytes): " +
            string.Join(' ', needle.ToArray().Select(b => b.ToString("x2"))));
    }

    // ---- fakes ----

    private class FakeAdapter : IHex1bTerminalWorkloadAdapter
    {
        private readonly Channel<ReadOnlyMemory<byte>> _out =
            Channel.CreateUnbounded<ReadOnlyMemory<byte>>();

        public int WrittenInputCount;
        public (int, int)? LastResize;

        public event Action? Disconnected;

        public void EnqueueOutput(byte[] bytes) => _out.Writer.TryWrite(bytes);
        public void CompleteOutput() => _out.Writer.TryComplete();

        public virtual async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        {
            try
            {
                return await _out.Reader.ReadAsync(ct).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                return ReadOnlyMemory<byte>.Empty;
            }
        }

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            Interlocked.Increment(ref WrittenInputCount);
            return ValueTask.CompletedTask;
        }

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
        {
            LastResize = (width, height);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _out.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        protected void RaiseDisconnected() => Disconnected?.Invoke();
    }

    private class FakeConnectableAdapter : FakeAdapter, IConnectableWorkloadAdapter
    {
        private readonly TaskCompletionSource _connected =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _disconnected =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ConnectedTask => _connected.Task;
        public Task DisconnectedTask => _disconnected.Task;
        public bool IsConnected =>
            _connected.Task.IsCompletedSuccessfully && !_disconnected.Task.IsCompleted;

        public void SignalConnected() => _connected.TrySetResult();

        public void SignalDisconnected()
        {
            _disconnected.TrySetResult();
            RaiseDisconnected();
        }
    }

    private sealed class RepaintablePrimary : FakeConnectableAdapter, IRepaintableWorkloadAdapter
    {
        public int RepaintRequests;
        public void RequestFullRepaint() => Interlocked.Increment(ref RepaintRequests);
    }

    private sealed class RepaintablePlaceholder : FakeAdapter, IRepaintableWorkloadAdapter
    {
        public int RepaintRequests;
        public void RequestFullRepaint() => Interlocked.Increment(ref RepaintRequests);
    }
}
