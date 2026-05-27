using System.Buffers;
using System.Diagnostics;
using Hex1b;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the bounded output channel + backpressure behaviour on
/// <see cref="Hex1bAppWorkloadAdapter"/>. The default is unbounded
/// (back-compat), so most tests opt in via the constructor parameter.
/// </summary>
public class Hex1bAppWorkloadAdapterBackpressureTests
{
    [Fact]
    public void Write_OnUnboundedAdapter_NeverBlocks()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        // Many writes without a reader should all enqueue immediately on
        // the default unbounded channel.
        for (var i = 0; i < 1000; i++)
            adapter.Write("x");
        Assert.Equal(1000, adapter.OutputQueueDepth);
    }

    [Fact]
    public async Task Write_OnBoundedAdapter_BlocksProducerWhenFull()
    {
        using var adapter = new Hex1bAppWorkloadAdapter(maxQueuedOutputItems: 2);

        // Fill the channel to capacity.
        adapter.Write("a");
        adapter.Write("b");
        Assert.Equal(2, adapter.OutputQueueDepth);

        // Producer thread - third write must block until a slot frees.
        var producerStarted = new TaskCompletionSource();
        var producerDone = new TaskCompletionSource();
        var producer = Task.Run(() =>
        {
            producerStarted.SetResult();
            adapter.Write("c"); // expected to block
            producerDone.SetResult();
        });

        await producerStarted.Task;
        // Give the producer time to attempt the write and block on the channel.
        await Task.Delay(150);
        Assert.False(producerDone.Task.IsCompleted, "Producer should be blocked while channel is full.");

        // Drain one item; producer should unblock and complete.
        var read = await adapter.TryReadOutputAsync(TimeSpan.FromSeconds(1));
        Assert.True(read.HasValue);

        await producerDone.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await producer;
        Assert.Equal(2, adapter.OutputQueueDepth);
    }

    [Fact]
    public async Task Write_OnBoundedAdapter_ChannelCompletionUnblocksProducer()
    {
        using var adapter = new Hex1bAppWorkloadAdapter(maxQueuedOutputItems: 1);

        adapter.Write("a"); // fill capacity

        var producer = Task.Run(() => adapter.Write("b")); // blocks

        await Task.Delay(100);
        Assert.False(producer.IsCompleted, "Producer should block until disposal unblocks it.");

        // Disposing completes the writer, which should unblock the blocked
        // write attempt without leaking resources or throwing out of the
        // adapter API.
        adapter.Dispose();
        await producer.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Constructor_NegativeMaxQueued_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Hex1bAppWorkloadAdapter(maxQueuedOutputItems: -1));
    }
}

internal static class BackpressureTestHelpers
{
    public static async Task<WorkloadOutputItem?> TryReadOutputAsync(this Hex1bAppWorkloadAdapter adapter, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            var item = await adapter.ReadOutputItemAsync(cts.Token);
            if (item.Bytes.Length > 0 || item.Tokens is not null)
                return item;
        }
        catch (OperationCanceledException)
        {
        }
        return null;
    }
}
