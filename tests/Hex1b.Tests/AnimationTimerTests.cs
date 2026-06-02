namespace Hex1b.Tests;

using Hex1b.Animation;

[TestClass]
public class AnimationTimerTests
{
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
