namespace Hex1b.Automation;

/// <summary>
/// Options for configuring test sequence execution.
/// </summary>
public sealed class Hex1bTerminalInputSequenceOptions
{
    /// <summary>
    /// Default options instance.
    /// </summary>
    public static Hex1bTerminalInputSequenceOptions Default { get; } = new();

    /// <summary>
    /// How frequently to poll when waiting for conditions.
    /// Default is 250ms.
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(250);

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

    /// <summary>
    /// Multiplier applied to all WaitUntil timeouts. Useful for CI environments
    /// where CPU contention makes timing-sensitive tests flaky.
    /// Default is 1.0 (no scaling). Set via <c>HEX1B_TEST_TIMEOUT_MULTIPLIER</c>
    /// environment variable, or override directly.
    /// </summary>
    public double TimeoutMultiplier { get; init; } = GetDefaultTimeoutMultiplier();

    private static double GetDefaultTimeoutMultiplier()
    {
        var envValue = Environment.GetEnvironmentVariable("HEX1B_TEST_TIMEOUT_MULTIPLIER");
        if (envValue is not null && double.TryParse(envValue, out var multiplier) && multiplier > 0)
            return multiplier;
        return 1.0;
    }
}
