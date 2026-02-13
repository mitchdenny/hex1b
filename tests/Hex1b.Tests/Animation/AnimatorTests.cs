using Hex1b.Animation;

namespace Hex1b.Tests.Animation;

public class AnimatorTests
{
    // --- Hex1bAnimator base tests ---

    [Fact]
    public void Start_SetsRunning()
    {
        var animator = new OpacityAnimator();
        animator.Start();

        Assert.True(animator.IsRunning);
        Assert.False(animator.IsPaused);
        Assert.False(animator.IsCompleted);
        Assert.Equal(0.0, animator.RawProgress);
    }

    [Fact]
    public void Advance_ProgressesOverDuration()
    {
        var animator = new OpacityAnimator { Duration = TimeSpan.FromMilliseconds(100) };
        animator.Start();

        animator.Advance(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0.5, animator.RawProgress, 6);
        Assert.True(animator.IsRunning);
    }

    [Fact]
    public void Advance_CompletesAtDuration()
    {
        var animator = new OpacityAnimator { Duration = TimeSpan.FromMilliseconds(100) };
        animator.Start();

        animator.Advance(TimeSpan.FromMilliseconds(100));

        Assert.Equal(1.0, animator.RawProgress, 6);
        Assert.True(animator.IsCompleted);
        Assert.False(animator.IsRunning);
    }

    [Fact]
    public void Advance_ClampsToOneWithoutRepeat()
    {
        var animator = new OpacityAnimator { Duration = TimeSpan.FromMilliseconds(100) };
        animator.Start();

        animator.Advance(TimeSpan.FromMilliseconds(200));

        Assert.Equal(1.0, animator.RawProgress, 6);
        Assert.True(animator.IsCompleted);
    }

    [Fact]
    public void Advance_Repeat_WrapsProgress()
    {
        var animator = new OpacityAnimator { Duration = TimeSpan.FromMilliseconds(100), Repeat = true };
        animator.Start();

        animator.Advance(TimeSpan.FromMilliseconds(150));

        Assert.Equal(0.5, animator.RawProgress, 6);
        Assert.True(animator.IsRunning);
    }

    [Fact]
    public void Advance_RepeatReverse_PingPongs()
    {
        var animator = new OpacityAnimator
        {
            Duration = TimeSpan.FromMilliseconds(100),
            Repeat = true,
            Reverse = true,
        };
        animator.Start();

        // Advance to 150ms: should be at 0.5 on the way back (1.0 - 0.5)
        animator.Advance(TimeSpan.FromMilliseconds(150));

        Assert.Equal(0.5, animator.RawProgress, 6);
        Assert.True(animator.IsRunning);
    }

    [Fact]
    public void Pause_StopsAdvancing()
    {
        var animator = new OpacityAnimator { Duration = TimeSpan.FromMilliseconds(100) };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(30));
        animator.Pause();

        var progressAtPause = animator.RawProgress;
        animator.Advance(TimeSpan.FromMilliseconds(50));

        Assert.Equal(progressAtPause, animator.RawProgress, 6);
        Assert.True(animator.IsPaused);
    }

    [Fact]
    public void Resume_ContinuesAdvancing()
    {
        var animator = new OpacityAnimator { Duration = TimeSpan.FromMilliseconds(100) };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(30));
        animator.Pause();
        animator.Resume();

        animator.Advance(TimeSpan.FromMilliseconds(30));

        Assert.Equal(0.6, animator.RawProgress, 6);
        Assert.True(animator.IsRunning);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var animator = new OpacityAnimator();
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(50));
        animator.Reset();

        Assert.Equal(0.0, animator.RawProgress, 6);
        Assert.False(animator.IsRunning);
        Assert.False(animator.IsPaused);
        Assert.False(animator.IsCompleted);
    }

    [Fact]
    public void Restart_ResetsAndStarts()
    {
        var animator = new OpacityAnimator { Duration = TimeSpan.FromMilliseconds(100) };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(100));
        Assert.True(animator.IsCompleted);

        animator.Restart();

        Assert.Equal(0.0, animator.RawProgress, 6);
        Assert.True(animator.IsRunning);
    }

    [Fact]
    public void Progress_AppliesEasing()
    {
        var animator = new OpacityAnimator
        {
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = Easing.EaseInQuad,
        };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(50));

        // Raw: 0.5, Eased: 0.25 (quad)
        Assert.Equal(0.5, animator.RawProgress, 6);
        Assert.Equal(0.25, animator.Progress, 6);
    }

    // --- NumericAnimator tests ---

    [Fact]
    public void NumericAnimator_Double_Interpolates()
    {
        var animator = new NumericAnimator<double>
        {
            From = 10.0, To = 20.0,
            Duration = TimeSpan.FromMilliseconds(100),
        };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(50));

        Assert.Equal(15.0, animator.Value, 6);
    }

    [Fact]
    public void NumericAnimator_Float_Interpolates()
    {
        var animator = new NumericAnimator<float>
        {
            From = 0f, To = 100f,
            Duration = TimeSpan.FromMilliseconds(100),
        };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(25));

        Assert.Equal(25f, animator.Value, 1);
    }

    [Fact]
    public void NumericAnimator_Int_Rounds()
    {
        var animator = new NumericAnimator<int>
        {
            From = 0, To = 10,
            Duration = TimeSpan.FromMilliseconds(100),
        };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(33));

        // 0.33 * 10 = 3.3 → rounds to 3
        Assert.Equal(3, animator.Value);
    }

    [Fact]
    public void NumericAnimator_WithEasing_AppliesEasingToValue()
    {
        var animator = new NumericAnimator<double>
        {
            From = 0.0, To = 100.0,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = Easing.EaseInQuad,
        };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(50));

        // Eased progress = 0.25, value = 25.0
        Assert.Equal(25.0, animator.Value, 6);
    }

    // --- OpacityAnimator tests ---

    [Fact]
    public void OpacityAnimator_DefaultsFromZeroToOne()
    {
        var animator = new OpacityAnimator();

        Assert.Equal(0.0, animator.From);
        Assert.Equal(1.0, animator.To);
    }

    [Fact]
    public void OpacityAnimator_InterpolatesCorrectly()
    {
        var animator = new OpacityAnimator { Duration = TimeSpan.FromMilliseconds(100) };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(75));

        Assert.Equal(0.75, animator.Value, 6);
    }
}
