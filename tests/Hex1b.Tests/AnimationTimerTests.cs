namespace Hex1b.Tests;

using Hex1b.Animation;

public class AnimationTimerTests
{
    [Fact]
    public void Schedule_AddsTimer()
    {
        var timer = new AnimationTimer();
        var fired = false;
        
        timer.Schedule(TimeSpan.FromMilliseconds(100), () => fired = true);
        
        Assert.True(timer.HasScheduledTimers);
        Assert.False(fired);
    }
    
    [Fact]
    public void GetTimeUntilNextDue_ReturnsNullWhenNoTimers()
    {
        var timer = new AnimationTimer();
        
        Assert.Null(timer.GetTimeUntilNextDue());
    }
    
    [Fact]
    public void GetTimeUntilNextDue_ReturnsTimeWhenTimerScheduled()
    {
        var timer = new AnimationTimer();
        timer.Schedule(TimeSpan.FromMilliseconds(100), () => { });
        
        var timeUntil = timer.GetTimeUntilNextDue();
        
        Assert.NotNull(timeUntil);
        Assert.True(timeUntil.Value.TotalMilliseconds > 0);
        Assert.True(timeUntil.Value.TotalMilliseconds <= 100);
    }
    
    [Fact]
    public void FireDue_FiresExpiredTimers()
    {
        var timer = new AnimationTimer();
        var fired = false;
        
        // Schedule with minimum delay (16ms)
        timer.Schedule(TimeSpan.FromMilliseconds(1), () => fired = true);
        
        // Wait for timer to expire
        Thread.Sleep(20);
        
        timer.FireDue();
        
        Assert.True(fired);
        Assert.False(timer.HasScheduledTimers);
    }
    
    [Fact]
    public void FireDue_DoesNotFireFutureTimers()
    {
        var timer = new AnimationTimer();
        var fired = false;
        
        timer.Schedule(TimeSpan.FromSeconds(5), () => fired = true);
        timer.FireDue();
        
        Assert.False(fired);
        Assert.True(timer.HasScheduledTimers);
    }
    
    [Fact]
    public void Schedule_ClampsToMinimum16ms()
    {
        var timer = new AnimationTimer();
        timer.Schedule(TimeSpan.FromMilliseconds(1), () => { });
        
        var timeUntil = timer.GetTimeUntilNextDue();
        
        // Should be close to 16ms minimum, not 1ms
        Assert.NotNull(timeUntil);
        Assert.True(timeUntil.Value.TotalMilliseconds >= 15); // Allow 1ms tolerance
    }
    
    [Fact]
    public void FireDue_FiresMultipleExpiredTimers()
    {
        var timer = new AnimationTimer();
        var count = 0;
        
        timer.Schedule(TimeSpan.FromMilliseconds(1), () => count++);
        timer.Schedule(TimeSpan.FromMilliseconds(1), () => count++);
        timer.Schedule(TimeSpan.FromMilliseconds(1), () => count++);
        
        Thread.Sleep(20);
        timer.FireDue();
        
        Assert.Equal(3, count);
        Assert.False(timer.HasScheduledTimers);
    }
    
    [Fact]
    public void GetTimeUntilNextDue_ReturnsZeroWhenTimerPastDue()
    {
        var timer = new AnimationTimer();
        timer.Schedule(TimeSpan.FromMilliseconds(1), () => { });
        
        // Wait for timer to be past due
        Thread.Sleep(20);
        
        var timeUntil = timer.GetTimeUntilNextDue();
        
        Assert.NotNull(timeUntil);
        Assert.Equal(TimeSpan.Zero, timeUntil.Value);
    }
    
    [Fact]
    public void MultipleTimers_GetTimeReturnsEarliest()
    {
        var timer = new AnimationTimer();
        
        timer.Schedule(TimeSpan.FromMilliseconds(500), () => { });
        timer.Schedule(TimeSpan.FromMilliseconds(100), () => { }); // Earlier
        timer.Schedule(TimeSpan.FromMilliseconds(300), () => { });
        
        var timeUntil = timer.GetTimeUntilNextDue();
        
        Assert.NotNull(timeUntil);
        // Should be ~100ms or less (whichever is minimum after clamping)
        Assert.True(timeUntil.Value.TotalMilliseconds <= 100);
    }
}
