namespace Hex1b;

internal static class UnixPtyStartupOptions
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    internal static TimeSpan ValidateTimeout(TimeSpan value)
    {
        if (value != Timeout.InfiniteTimeSpan && value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(value), value,
                "The Unix PTY startup timeout must be positive or Timeout.InfiniteTimeSpan.");

        return value;
    }
}
