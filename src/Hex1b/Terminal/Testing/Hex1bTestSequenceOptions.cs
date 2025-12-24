namespace Hex1b.Terminal.Testing;

/// <summary>
/// Options for configuring test sequence execution.
/// </summary>
public sealed class Hex1bTestSequenceOptions
{
    /// <summary>
    /// Default options instance.
    /// </summary>
    public static Hex1bTestSequenceOptions Default { get; } = new();

    /// <summary>
    /// How frequently to poll when waiting for conditions.
    /// Default is 50ms.
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Delay between keystrokes for SlowType.
    /// Default is 50ms.
    /// </summary>
    public TimeSpan SlowTypeDelay { get; init; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Optional TimeProvider for controlling time in tests.
    /// When using FakeTimeProvider, the test should not await ApplyAsync directly.
    /// Instead, run ApplyAsync without await, then advance time using FakeTimeProvider.Advance()
    /// to trigger the Wait steps. Default is null (uses Task.Delay with real time).
    /// </summary>
    public TimeProvider? TimeProvider { get; init; }
}
