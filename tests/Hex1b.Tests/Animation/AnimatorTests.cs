using Hex1b.Animation;

namespace Hex1b.Tests.Animation;

[TestClass]
public class AnimatorTests
{
    // --- Hex1bAnimator base tests ---

    [TestMethod]
    public void Start_SetsRunning()
    {
        var animator = new NumericAnimator<double>();
        animator.Start();

        Assert.IsTrue(animator.IsRunning);
        Assert.IsFalse(animator.IsPaused);
        Assert.IsFalse(animator.IsCompleted);
        Assert.AreEqual(0.0, animator.RawProgress);
    }

    [TestMethod]
    public void Advance_ProgressesOverDuration()
    {
        var animator = new NumericAnimator<double> { Duration = TimeSpan.FromMilliseconds(100) };
        animator.Start();

        animator.Advance(TimeSpan.FromMilliseconds(50));

        Assert.AreEqual(0.5, animator.RawProgress, 6);
        Assert.IsTrue(animator.IsRunning);
    }

    [TestMethod]
    public void Advance_CompletesAtDuration()
    {
        var animator = new NumericAnimator<double> { Duration = TimeSpan.FromMilliseconds(100) };
        animator.Start();

        animator.Advance(TimeSpan.FromMilliseconds(100));

        Assert.AreEqual(1.0, animator.RawProgress, 6);
        Assert.IsTrue(animator.IsCompleted);
        Assert.IsFalse(animator.IsRunning);
    }

    [TestMethod]
    public void Advance_ClampsToOneWithoutRepeat()
    {
        var animator = new NumericAnimator<double> { Duration = TimeSpan.FromMilliseconds(100) };
        animator.Start();

        animator.Advance(TimeSpan.FromMilliseconds(200));

        Assert.AreEqual(1.0, animator.RawProgress, 6);
        Assert.IsTrue(animator.IsCompleted);
    }

    [TestMethod]
    public void Advance_Repeat_WrapsProgress()
    {
        var animator = new NumericAnimator<double> { Duration = TimeSpan.FromMilliseconds(100), Repeat = true };
        animator.Start();

        animator.Advance(TimeSpan.FromMilliseconds(150));

        Assert.AreEqual(0.5, animator.RawProgress, 6);
        Assert.IsTrue(animator.IsRunning);
    }

    [TestMethod]
    public void Advance_RepeatReverse_PingPongs()
    {
        var animator = new NumericAnimator<double>
        {
            Duration = TimeSpan.FromMilliseconds(100),
            Repeat = true,
            Reverse = true,
        };
        animator.Start();

        // Advance to 150ms: should be at 0.5 on the way back (1.0 - 0.5)
        animator.Advance(TimeSpan.FromMilliseconds(150));

        Assert.AreEqual(0.5, animator.RawProgress, 6);
        Assert.IsTrue(animator.IsRunning);
    }

    [TestMethod]
    public void Pause_StopsAdvancing()
    {
        var animator = new NumericAnimator<double> { Duration = TimeSpan.FromMilliseconds(100) };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(30));
        animator.Pause();

        var progressAtPause = animator.RawProgress;
        animator.Advance(TimeSpan.FromMilliseconds(50));

        Assert.AreEqual(progressAtPause, animator.RawProgress, 6);
        Assert.IsTrue(animator.IsPaused);
    }

    [TestMethod]
    public void Resume_ContinuesAdvancing()
    {
        var animator = new NumericAnimator<double> { Duration = TimeSpan.FromMilliseconds(100) };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(30));
        animator.Pause();
        animator.Resume();

        animator.Advance(TimeSpan.FromMilliseconds(30));

        Assert.AreEqual(0.6, animator.RawProgress, 6);
        Assert.IsTrue(animator.IsRunning);
    }

    [TestMethod]
    public void Reset_ClearsState()
    {
        var animator = new NumericAnimator<double>();
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(50));
        animator.Reset();

        Assert.AreEqual(0.0, animator.RawProgress, 6);
        Assert.IsFalse(animator.IsRunning);
        Assert.IsFalse(animator.IsPaused);
        Assert.IsFalse(animator.IsCompleted);
    }

    [TestMethod]
    public void Restart_ResetsAndStarts()
    {
        var animator = new NumericAnimator<double> { Duration = TimeSpan.FromMilliseconds(100) };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(100));
        Assert.IsTrue(animator.IsCompleted);

        animator.Restart();

        Assert.AreEqual(0.0, animator.RawProgress, 6);
        Assert.IsTrue(animator.IsRunning);
    }

    [TestMethod]
    public void Progress_AppliesEasing()
    {
        var animator = new NumericAnimator<double>
        {
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = Easing.EaseInQuad,
        };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(50));

        // Raw: 0.5, Eased: 0.25 (quad)
        Assert.AreEqual(0.5, animator.RawProgress, 6);
        Assert.AreEqual(0.25, animator.Progress, 6);
    }

    // --- NumericAnimator tests ---

    [TestMethod]
    public void NumericAnimator_Double_Interpolates()
    {
        var animator = new NumericAnimator<double>
        {
            From = 10.0, To = 20.0,
            Duration = TimeSpan.FromMilliseconds(100),
        };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(50));

        Assert.AreEqual(15.0, animator.Value, 6);
    }

    [TestMethod]
    public void NumericAnimator_Float_Interpolates()
    {
        var animator = new NumericAnimator<float>
        {
            From = 0f, To = 100f,
            Duration = TimeSpan.FromMilliseconds(100),
        };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(25));

        Assert.AreEqual(25f, animator.Value, 1);
    }

    [TestMethod]
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
        Assert.AreEqual(3, animator.Value);
    }

    [TestMethod]
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
        Assert.AreEqual(25.0, animator.Value, 6);
    }

    // --- NumericAnimator<double> tests ---

    [TestMethod]
    public void NumericAnimatorDouble_DefaultsFromZeroToZero()
    {
        var animator = new NumericAnimator<double>();

        Assert.AreEqual(0.0, animator.From);
        Assert.AreEqual(0.0, animator.To);
    }

    [TestMethod]
    public void NumericAnimatorDouble_InterpolatesCorrectly()
    {
        var animator = new NumericAnimator<double> { From = 0.0, To = 1.0, Duration = TimeSpan.FromMilliseconds(100) };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(75));

        Assert.AreEqual(0.75, animator.Value, 6);
    }

    // --- AnimateTo (retargeting) tests ---

    [TestMethod]
    public void AnimateTo_StartsFromCurrentValue()
    {
        var animator = new NumericAnimator<double>
        {
            From = 0.0, To = 100.0,
            Duration = TimeSpan.FromMilliseconds(100),
        };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(50)); // At 50.0

        animator.AnimateTo(0.0); // Retarget back to 0

        Assert.AreEqual(50.0, animator.From, 1);
        Assert.AreEqual(0.0, animator.To, 1);
        Assert.IsTrue(animator.IsRunning);
        Assert.AreEqual(50.0, animator.Value, 1); // Starts at current position
    }

    [TestMethod]
    public void AnimateTo_SameTarget_DoesNotRestart()
    {
        var animator = new NumericAnimator<double>
        {
            From = 0.0, To = 1.0,
            Duration = TimeSpan.FromMilliseconds(100),
        };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(50)); // Progress = 0.5

        var progressBefore = animator.RawProgress;
        animator.AnimateTo(1.0); // Same target — should not restart

        Assert.AreEqual(progressBefore, animator.RawProgress, 6);
    }

    [TestMethod]
    public void AnimateTo_CompletedAnimation_NewTarget_Restarts()
    {
        var animator = new NumericAnimator<double>
        {
            From = 0.0, To = 1.0,
            Duration = TimeSpan.FromMilliseconds(100),
        };
        animator.Start();
        animator.Advance(TimeSpan.FromMilliseconds(100)); // Completed at 1.0
        Assert.IsTrue(animator.IsCompleted);

        animator.AnimateTo(0.0); // New target

        Assert.IsTrue(animator.IsRunning);
        Assert.AreEqual(1.0, animator.From, 6); // Starts from where we were
        Assert.AreEqual(0.0, animator.To, 6);
    }
}
