using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests;

[TestClass]
public class FrameSchedulerTests
{
    [TestMethod]
    public async Task RequestFrame_IdleAtTimestampZero_StartsImmediately()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.MinValue);
        using var scheduler = new FrameScheduler(TimeSpan.FromMilliseconds(16), clock);
        Assert.AreEqual(0L, clock.GetTimestamp());
        var ready = scheduler.WaitForFrameAsync(TestContext.Current.CancellationToken);
        Assert.IsFalse(ready.IsCompleted);

        scheduler.RequestFrame();
        Assert.IsTrue(await ready.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(scheduler.TryBeginFrame());
        scheduler.RequestFrame();
        Assert.IsFalse(scheduler.TryBeginFrame(), "Timestamp zero must still count as a frame start.");
    }

    [TestMethod]
    public async Task RequestFrame_RepeatedWhilePacing_DoesNotPostponeDeadline()
    {
        var clock = new FakeTimeProvider();
        using var scheduler = new FrameScheduler(TimeSpan.FromMilliseconds(16), clock);
        scheduler.RequestFrame();
        Assert.IsTrue(scheduler.TryBeginFrame());
        scheduler.RequestFrame();
        var ready = scheduler.WaitForFrameAsync(TestContext.Current.CancellationToken);

        for (var i = 0; i < 15; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(1));
            scheduler.RequestFrame();
        }
        Assert.IsFalse(ready.IsCompleted);
        Assert.IsFalse(scheduler.TryBeginFrame());
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.IsTrue(await ready.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(scheduler.TryBeginFrame());
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.IsFalse(scheduler.TryBeginFrame(), "Coalesced requests must not turn into queued frames.");
    }

    [TestMethod]
    public async Task WaitForFrame_RenderConsumesBudget_WaitsOnlyRemainingInterval()
    {
        var clock = new FakeTimeProvider();
        using var scheduler = new FrameScheduler(TimeSpan.FromMilliseconds(16), clock);
        scheduler.RequestFrame();
        Assert.IsTrue(scheduler.TryBeginFrame());
        clock.Advance(TimeSpan.FromMilliseconds(10));
        scheduler.RequestFrame();
        var ready = scheduler.WaitForFrameAsync(TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromMilliseconds(5));
        Assert.IsFalse(ready.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.IsTrue(await ready.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(scheduler.TryBeginFrame());
    }

    [TestMethod]
    public void TryBeginFrame_Overrun_DoesNotCatchUp()
    {
        var clock = new FakeTimeProvider();
        using var scheduler = new FrameScheduler(TimeSpan.FromMilliseconds(16), clock);
        scheduler.RequestFrame();
        Assert.IsTrue(scheduler.TryBeginFrame());
        clock.Advance(TimeSpan.FromMilliseconds(100));
        scheduler.RequestFrame();
        Assert.IsTrue(scheduler.TryBeginFrame());
        scheduler.RequestFrame();
        Assert.IsFalse(scheduler.TryBeginFrame());
    }

    [TestMethod]
    public async Task WaitForFrame_Ready_DoesNotConsumeRequest()
    {
        using var scheduler = new FrameScheduler(TimeSpan.FromMilliseconds(16), new FakeTimeProvider());
        scheduler.RequestFrame();
        Assert.IsTrue(await scheduler.WaitForFrameAsync(TestContext.Current.CancellationToken));
        Assert.IsTrue(await scheduler.WaitForFrameAsync(TestContext.Current.CancellationToken));
        Assert.IsTrue(scheduler.TryBeginFrame());
    }

    [TestMethod]
    public async Task TryBeginFrame_InputBypassesPacing_UpdatesPendingWaitDeadline()
    {
        var clock = new FakeTimeProvider();
        using var scheduler = new FrameScheduler(TimeSpan.FromMilliseconds(16), clock);
        scheduler.RequestFrame();
        Assert.IsTrue(scheduler.TryBeginFrame());
        scheduler.RequestFrame();
        var ready = scheduler.WaitForFrameAsync(TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromMilliseconds(10));
        Assert.IsTrue(scheduler.TryBeginFrame(bypassPacing: true));
        scheduler.RequestFrame(); // Output during this input-driven frame.
        clock.Advance(TimeSpan.FromMilliseconds(6));
        Assert.IsFalse(scheduler.TryBeginFrame());
        clock.Advance(TimeSpan.FromMilliseconds(10));
        Assert.IsTrue(await ready.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(scheduler.TryBeginFrame());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WaitForFrame_Cancellation_InterruptsIdleAndPacing(bool pacing)
    {
        using var scheduler = new FrameScheduler(TimeSpan.FromSeconds(1), new FakeTimeProvider());
        if (pacing)
        {
            scheduler.RequestFrame();
            Assert.IsTrue(scheduler.TryBeginFrame());
            scheduler.RequestFrame();
        }
        using var cancellation = new CancellationTokenSource();
        var ready = scheduler.WaitForFrameAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await ready);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Dispose_PendingWait_CompletesWithoutAdvancingTime(bool pacing)
    {
        var scheduler = new FrameScheduler(TimeSpan.FromSeconds(1), new FakeTimeProvider());
        if (pacing)
        {
            scheduler.RequestFrame();
            Assert.IsTrue(scheduler.TryBeginFrame());
            scheduler.RequestFrame();
        }
        var ready = scheduler.WaitForFrameAsync(TestContext.Current.CancellationToken);
        scheduler.Dispose();
        Assert.IsFalse(await ready.WaitAsync(TimeSpan.FromSeconds(5)));
        scheduler.RequestFrame();
        Assert.IsFalse(scheduler.TryBeginFrame());
    }
}
