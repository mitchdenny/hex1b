namespace Hex1b.Animation;

/// <summary>
/// Base class for animations that progress over a duration with easing.
/// Provides Start/Pause/Resume/Reset lifecycle and Advance(elapsed) for ticking.
/// </summary>
public abstract class Hex1bAnimator
{
    private double _rawProgress;
    private bool _isRunning;
    private bool _isPaused;

    /// <summary>Duration of the animation.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Easing function applied to raw progress. Defaults to Linear.</summary>
    public Func<double, double> EasingFunction { get; set; } = Easing.Linear;

    /// <summary>Whether the animation repeats after completing.</summary>
    public bool Repeat { get; set; }

    /// <summary>Whether the animation reverses direction on each cycle.</summary>
    public bool Reverse { get; set; }

    /// <summary>Raw progress (0..1) before easing.</summary>
    internal double RawProgress => _rawProgress;

    /// <summary>Eased progress (0..1).</summary>
    public double Progress => EasingFunction(_rawProgress);

    /// <summary>Whether the animation is currently running (not paused, not completed).</summary>
    public bool IsRunning => _isRunning && !_isPaused;

    /// <summary>Whether the animation is paused.</summary>
    public bool IsPaused => _isPaused;

    /// <summary>Whether the animation has completed (progress reached 1.0 and not repeating).</summary>
    public bool IsCompleted => !_isRunning && _rawProgress >= 1.0;

    /// <summary>Starts or restarts the animation from the beginning.</summary>
    public void Start()
    {
        _rawProgress = 0;
        _isRunning = true;
        _isPaused = false;
    }

    /// <summary>Pauses the animation at its current progress.</summary>
    public void Pause()
    {
        if (_isRunning)
            _isPaused = true;
    }

    /// <summary>Resumes a paused animation.</summary>
    public void Resume()
    {
        if (_isRunning && _isPaused)
            _isPaused = false;
    }

    /// <summary>Resets the animation to the beginning without starting it.</summary>
    public void Reset()
    {
        _rawProgress = 0;
        _isRunning = false;
        _isPaused = false;
    }

    /// <summary>Restarts the animation (equivalent to Reset + Start).</summary>
    public void Restart()
    {
        Reset();
        Start();
    }

    /// <summary>
    /// Advances the animation by the given elapsed time.
    /// Called by AnimationCollection during each frame.
    /// </summary>
    internal void Advance(TimeSpan elapsed)
    {
        if (!_isRunning || _isPaused || Duration <= TimeSpan.Zero)
            return;

        _rawProgress += elapsed / Duration;

        if (_rawProgress >= 1.0)
        {
            if (Repeat)
            {
                if (Reverse)
                {
                    // Ping-pong: reflect progress
                    _rawProgress = _rawProgress % 2.0;
                    if (_rawProgress > 1.0)
                        _rawProgress = 2.0 - _rawProgress;
                }
                else
                {
                    _rawProgress %= 1.0;
                }
            }
            else
            {
                _rawProgress = 1.0;
                _isRunning = false;
            }
        }
    }
}
