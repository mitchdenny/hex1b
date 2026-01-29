namespace Hex1b.Widgets;

/// <summary>
/// Defines a spinner animation style as a sequence of frames.
/// </summary>
/// <remarks>
/// <para>
/// SpinnerStyle provides built-in styles via static properties (e.g., <see cref="Dots"/>, <see cref="Line"/>)
/// and supports custom styles via the constructor.
/// </para>
/// <para>
/// Each style includes a suggested <see cref="Interval"/> for smooth animation and an
/// <see cref="AutoReverse"/> flag to control whether the animation ping-pongs or loops.
/// </para>
/// </remarks>
/// <example>
/// <para>Using a built-in style:</para>
/// <code>
/// ctx.Spinner(SpinnerStyle.Dots, frameIndex)
/// </code>
/// <para>Creating a custom style:</para>
/// <code>
/// var moonPhases = new SpinnerStyle(
///     TimeSpan.FromMilliseconds(500),
///     "🌑", "🌒", "🌓", "🌔", "🌕", "🌖", "🌗", "🌘");
/// ctx.Spinner(moonPhases, frameIndex)
/// </code>
/// </example>
public sealed class SpinnerStyle
{
    /// <summary>
    /// Gets the animation frames for this spinner style.
    /// </summary>
    public IReadOnlyList<string> Frames { get; }

    /// <summary>
    /// Gets the suggested interval between frame changes for smooth animation.
    /// </summary>
    /// <remarks>
    /// This is a hint for timing. Use with <see cref="AnimationExtensions.RedrawAfter{TWidget}(TWidget, int)"/>
    /// for automatic animation.
    /// </remarks>
    public TimeSpan Interval { get; }

    /// <summary>
    /// Gets whether the animation should reverse (ping-pong) instead of looping.
    /// </summary>
    /// <remarks>
    /// When true: 0,1,2,3,2,1,0,1,2,3... (ping-pong)
    /// When false: 0,1,2,3,0,1,2,3... (loop)
    /// </remarks>
    public bool AutoReverse { get; }

    /// <summary>
    /// Creates a custom spinner style with default settings (80ms, no auto-reverse).
    /// </summary>
    /// <param name="frames">The animation frames to cycle through.</param>
    /// <exception cref="ArgumentException">Thrown when frames is empty.</exception>
    public SpinnerStyle(params string[] frames)
        : this(TimeSpan.FromMilliseconds(80), autoReverse: false, frames)
    {
    }

    /// <summary>
    /// Creates a custom spinner style with the specified interval.
    /// </summary>
    /// <param name="interval">Suggested time between frame changes.</param>
    /// <param name="frames">The animation frames to cycle through.</param>
    public SpinnerStyle(TimeSpan interval, params string[] frames)
        : this(interval, autoReverse: false, frames)
    {
    }

    /// <summary>
    /// Creates a custom spinner style with the specified interval and auto-reverse setting.
    /// </summary>
    /// <param name="interval">Suggested time between frame changes.</param>
    /// <param name="autoReverse">Whether to ping-pong instead of loop.</param>
    /// <param name="frames">The animation frames to cycle through.</param>
    /// <exception cref="ArgumentException">Thrown when frames is empty.</exception>
    public SpinnerStyle(TimeSpan interval, bool autoReverse, params string[] frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Length == 0)
            throw new ArgumentException("At least one frame is required.", nameof(frames));
        Interval = interval;
        AutoReverse = autoReverse;
        Frames = frames;
    }

    /// <summary>Classic ASCII line spinner: | / - \ (loops)</summary>
    public static SpinnerStyle Line { get; } = new(
        TimeSpan.FromMilliseconds(100), autoReverse: false,
        "|", "/", "-", "\\");

    /// <summary>Braille dot pattern (loops)</summary>
    public static SpinnerStyle Dots { get; } = new(
        TimeSpan.FromMilliseconds(80), autoReverse: false,
        "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏");

    /// <summary>Scrolling braille dots (loops)</summary>
    public static SpinnerStyle DotsScrolling { get; } = new(
        TimeSpan.FromMilliseconds(80), autoReverse: false,
        "⣾", "⣽", "⣻", "⢿", "⡿", "⣟", "⣯", "⣷");

    /// <summary>Arrow rotation (loops)</summary>
    public static SpinnerStyle Arrow { get; } = new(
        TimeSpan.FromMilliseconds(100), autoReverse: false,
        "←", "↖", "↑", "↗", "→", "↘", "↓", "↙");

    /// <summary>Bouncing bar (auto-reverses for smooth bounce)</summary>
    public static SpinnerStyle Bounce { get; } = new(
        TimeSpan.FromMilliseconds(80), autoReverse: true,
        "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█");

    /// <summary>Quarter circle rotation (loops)</summary>
    public static SpinnerStyle Circle { get; } = new(
        TimeSpan.FromMilliseconds(120), autoReverse: false,
        "◐", "◓", "◑", "◒");

    /// <summary>Square corner rotation (loops)</summary>
    public static SpinnerStyle Square { get; } = new(
        TimeSpan.FromMilliseconds(120), autoReverse: false,
        "◰", "◳", "◲", "◱");

    /// <summary>Horizontal bar growth (auto-reverses)</summary>
    public static SpinnerStyle GrowHorizontal { get; } = new(
        TimeSpan.FromMilliseconds(80), autoReverse: true,
        "▏", "▎", "▍", "▌", "▋", "▊", "▉", "█");

    /// <summary>Vertical bar growth (auto-reverses)</summary>
    public static SpinnerStyle GrowVertical { get; } = new(
        TimeSpan.FromMilliseconds(80), autoReverse: true,
        "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█");

    /// <summary>Bouncing ball in brackets (loops)</summary>
    public static SpinnerStyle BouncingBall { get; } = new(
        TimeSpan.FromMilliseconds(100), autoReverse: false,
        "[●    ]", "[ ●   ]", "[  ●  ]", "[   ● ]", "[    ●]",
        "[   ● ]", "[  ●  ]", "[ ●   ]");

    /// <summary>Loading bar (loops)</summary>
    public static SpinnerStyle LoadingBar { get; } = new(
        TimeSpan.FromMilliseconds(120), autoReverse: false,
        "[     ]", "[=    ]", "[==   ]", "[===  ]", "[==== ]", "[=====]");

    /// <summary>Segmented blocks (loops)</summary>
    public static SpinnerStyle Segments { get; } = new(
        TimeSpan.FromMilliseconds(100), autoReverse: false,
        "▱▱▱▱▱", "▰▱▱▱▱", "▰▰▱▱▱", "▰▰▰▱▱", "▰▰▰▰▱", "▰▰▰▰▰");

    /// <summary>
    /// Gets the frame at the given index, handling auto-reverse if enabled.
    /// </summary>
    /// <param name="frameIndex">The frame index (any integer, wraps/bounces automatically).</param>
    /// <returns>The frame string at that index.</returns>
    public string GetFrame(int frameIndex)
    {
        if (AutoReverse && Frames.Count > 1)
        {
            // Ping-pong: 0,1,2,3,2,1,0,1,2,3,2,1...
            var cycleLength = (Frames.Count - 1) * 2;
            var pos = ((frameIndex % cycleLength) + cycleLength) % cycleLength;

            return pos < Frames.Count ? Frames[pos] : Frames[cycleLength - pos];
        }
        else
        {
            // Simple loop: 0,1,2,3,0,1,2,3...
            var count = Frames.Count;
            return Frames[((frameIndex % count) + count) % count];
        }
    }
}
