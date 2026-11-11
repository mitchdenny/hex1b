namespace Hex1b.Tests;

using Hex1b.Animation;
using Hex1b.Nodes;
using Microsoft.Extensions.Time.Testing;

[DoNotParallelize]
[TestClass]
public class AnimationTimerTests
{
    [TestMethod]
    public void ScheduleRedraw_ReconciliationConsumesTime_UsesFrameStartDeadline()
    {
        var clock = new FakeTimeProvider();
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(16), clock);
        timer.BeginRedrawFrame();
        clock.Advance(TimeSpan.FromMilliseconds(10));
        timer.ScheduleRedraw(new TextBlockNode(), false, TimeSpan.FromMilliseconds(16), () => { });
        timer.EndRedrawFrame();
        Assert.AreEqual(TimeSpan.FromMilliseconds(6), timer.GetTimeUntilNextDue());
        clock.Advance(TimeSpan.FromMilliseconds(6));
        Assert.AreEqual(1, timer.FireDue());
    }

    [TestMethod]
    public void ScheduleRedraw_RepeatedReconciliation_KeepsOneOriginalDeadline()
    {
        var clock = new FakeTimeProvider();
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(16), clock);
        var node = new TextBlockNode();
        var fired = 0;
        for (var i = 0; i < 10; i++)
        {
            timer.BeginRedrawFrame();
            timer.ScheduleRedraw(node, false, TimeSpan.FromMilliseconds(100), () => fired++);
            timer.EndRedrawFrame();
            Assert.AreEqual(1, timer.PendingRedrawCount);
            clock.Advance(TimeSpan.FromMilliseconds(10));
        }
        Assert.AreEqual(TimeSpan.Zero, timer.GetTimeUntilNextDue());
        Assert.AreEqual(1, timer.FireDue());
        Assert.AreEqual(1, fired);
        Assert.AreEqual(0, timer.PendingRedrawCount);
        Assert.IsNull(timer.GetTimeUntilNextDue());
    }

    [TestMethod]
    public void ScheduleRedraw_EarlierRequest_BringsDeadlineForwardWithLatestCallback()
    {
        var clock = new FakeTimeProvider();
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(16), clock);
        var node = new TextBlockNode();
        var result = "";
        timer.ScheduleRedraw(node, false, TimeSpan.FromMilliseconds(100), () => result = "old");
        clock.Advance(TimeSpan.FromMilliseconds(10));
        timer.ScheduleRedraw(node, false, TimeSpan.FromMilliseconds(20), () => result = "new");
        clock.Advance(TimeSpan.FromMilliseconds(20));
        Assert.AreEqual(1, timer.FireDue());
        Assert.AreEqual("new", result);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ScheduleRedraw_RemovedOrDisabledOwner_DoesNotFire(bool disabled)
    {
        var clock = new FakeTimeProvider();
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(16), clock);
        var node = new TextBlockNode();
        var fired = false;
        timer.ScheduleRedraw(node, false, TimeSpan.FromMilliseconds(20), () => fired = true);
        timer.BeginRedrawFrame();
        if (disabled)
            timer.ScheduleRedraw(node, false, null, () => fired = true);
        timer.EndRedrawFrame();
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(0, timer.PendingRedrawCount);
        Assert.AreEqual(0, timer.FireDue());
        Assert.IsFalse(fired);
    }

    [TestMethod]
    public void ScheduleRedraw_TwoPurposesAndOrdinaryCallbacks_RemainIndependent()
    {
        var clock = new FakeTimeProvider();
        var timer = new AnimationTimer(TimeSpan.FromMilliseconds(16), clock);
        var node = new TextBlockNode();
        var redraws = 0;
        var callbacks = 0;
        timer.BeginRedrawFrame();
        timer.ScheduleRedraw(node, false, TimeSpan.FromMilliseconds(20), () => redraws++);
        timer.ScheduleRedraw(node, true, TimeSpan.FromMilliseconds(30), () => redraws++);
        timer.Schedule(TimeSpan.FromMilliseconds(20), () => callbacks++);
        timer.Schedule(TimeSpan.FromMilliseconds(20), () => callbacks++);
        timer.EndRedrawFrame();
        Assert.AreEqual(2, timer.PendingRedrawCount);
        clock.Advance(TimeSpan.FromMilliseconds(20));
        Assert.AreEqual(3, timer.FireDue());
        Assert.AreEqual(1, redraws);
        Assert.AreEqual(2, callbacks);
        clock.Advance(TimeSpan.FromMilliseconds(10));
        Assert.AreEqual(1, timer.FireDue());
        Assert.AreEqual(2, redraws);
    }

    [TestMethod]
    public void ScheduleRedraw_HighFrameInterval_DoesNotClampAnimationDeadline()
    {
        var clock = new FakeTimeProvider();
        var timer = new AnimationTimer(TimeSpan.FromSeconds(1), clock);
        timer.ScheduleRedraw(new TextBlockNode(), false, TimeSpan.FromMilliseconds(20), () => { });
        timer.Schedule(TimeSpan.FromMilliseconds(20), () => { });
        Assert.AreEqual(TimeSpan.FromMilliseconds(20), timer.GetTimeUntilNextDue());
        clock.Advance(TimeSpan.FromMilliseconds(20));
        Assert.AreEqual(1, timer.FireDue());
        Assert.AreEqual(TimeSpan.FromMilliseconds(980), timer.GetTimeUntilNextDue(),
            "The public one-shot timer still honors its configured minimum interval.");
    }

    [TestMethod]
    public void Schedule_AddsTimer()
    {
        var timer = new AnimationTimer();
        var fired = false;
        
        timer.Schedule(TimeSpan.FromMilliseconds(100), () => fired = true);
        
        Assert.IsTrue(timer.HasScheduledTimers);
        Assert.IsFalse(fired);
    }
    
    [TestMethod]
    public void GetTimeUntilNextDue_ReturnsNullWhenNoTimers()
    {
        var timer = new AnimationTimer();
        
        Assert.IsNull(timer.GetTimeUntilNextDue());
    }
    
    [TestMethod]
    public void GetTimeUntilNextDue_ReturnsTimeWhenTimerScheduled()
    {
        var timer = new AnimationTimer();
        timer.Schedule(TimeSpan.FromMilliseconds(100), () => { });
        
        var timeUntil = timer.GetTimeUntilNextDue();
        
        Assert.IsNotNull(timeUntil);
        Assert.IsTrue(timeUntil.Value.TotalMilliseconds > 0);
        Assert.IsTrue(timeUntil.Value.TotalMilliseconds <= 100);
    }
    
    [TestMethod]
    public void FireDue_FiresExpiredTimers()
    {
        var timer = new AnimationTimer();
        var fired = false;
        
        // Schedule with minimum delay (16ms)
        timer.Schedule(TimeSpan.FromMilliseconds(1), () => fired = true);
        
        // Wait for timer to expire
        Thread.Sleep(50);
        
        timer.FireDue();
        
        Assert.IsTrue(fired);
        Assert.IsFalse(timer.HasScheduledTimers);
    }
    
    [TestMethod]
    public void FireDue_DoesNotFireFutureTimers()
    {
        var timer = new AnimationTimer();
        var fired = false;
        
        timer.Schedule(TimeSpan.FromSeconds(10), () => fired = true);
        timer.FireDue();
        
        Assert.IsFalse(fired);
        Assert.IsTrue(timer.HasScheduledTimers);
    }
    
    [TestMethod]
    public void Schedule_ClampsToMinimum16ms()
    {
        var timer = new AnimationTimer();
        timer.Schedule(TimeSpan.FromMilliseconds(1), () => { });
        
        var timeUntil = timer.GetTimeUntilNextDue();
        
        // Should be close to 16ms minimum, not 1ms
        Assert.IsNotNull(timeUntil);
        Assert.IsTrue(timeUntil.Value.TotalMilliseconds >= 15); // Allow 1ms tolerance
    }
    
    [TestMethod]
    public void FireDue_FiresMultipleExpiredTimers()
    {
        var timer = new AnimationTimer();
        var count = 0;
        
        timer.Schedule(TimeSpan.FromMilliseconds(1), () => count++);
        timer.Schedule(TimeSpan.FromMilliseconds(1), () => count++);
        timer.Schedule(TimeSpan.FromMilliseconds(1), () => count++);
        
        Thread.Sleep(50);
        timer.FireDue();
        
        Assert.AreEqual(3, count);
        Assert.IsFalse(timer.HasScheduledTimers);
    }
    
    [TestMethod]
    public void GetTimeUntilNextDue_ReturnsZeroWhenTimerPastDue()
    {
        var timer = new AnimationTimer();
        timer.Schedule(TimeSpan.FromMilliseconds(1), () => { });
        
        // Wait for timer to be past due
        Thread.Sleep(50);
        
        var timeUntil = timer.GetTimeUntilNextDue();
        
        Assert.IsNotNull(timeUntil);
        Assert.AreEqual(TimeSpan.Zero, timeUntil.Value);
    }
    
    [TestMethod]
    public void MultipleTimers_GetTimeReturnsEarliest()
    {
        var timer = new AnimationTimer();
        
        timer.Schedule(TimeSpan.FromMilliseconds(500), () => { });
        timer.Schedule(TimeSpan.FromMilliseconds(100), () => { }); // Earlier
        timer.Schedule(TimeSpan.FromMilliseconds(300), () => { });
        
        var timeUntil = timer.GetTimeUntilNextDue();
        
        Assert.IsNotNull(timeUntil);
        // Should be ~100ms or less (whichever is minimum after clamping)
        Assert.IsTrue(timeUntil.Value.TotalMilliseconds <= 100);
    }
}
