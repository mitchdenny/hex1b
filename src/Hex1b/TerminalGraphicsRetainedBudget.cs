namespace Hex1b;

/// <summary>
/// Shared retained-image byte accounting for one terminal screen.
/// </summary>
internal sealed class TerminalGraphicsRetainedBudget
{
    private readonly object _lock = new();
    private long _kgpBytes;
    private long _kgpReservedBytes;
    private long _sixelBytes;

    internal TerminalGraphicsRetainedBudget(
        long maximumBytes,
        int maximumInputBytes = 1024 * 1024,
        long maximumRasterPixels = 16L * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        MaximumBytes = maximumBytes;
        MaximumInputBytes = maximumInputBytes;
        MaximumRasterPixels = maximumRasterPixels;
    }

    internal long MaximumBytes { get; }
    internal int MaximumInputBytes { get; }
    internal long MaximumRasterPixels { get; }

    internal long KgpBytes
    {
        get
        {
            lock (_lock)
                return _kgpBytes;
        }
    }

    internal long SixelBytes
    {
        get
        {
            lock (_lock)
                return _sixelBytes;
        }
    }

    internal long MaximumKgpBytes
    {
        get
        {
            lock (_lock)
                return Math.Max(0, MaximumBytes - _sixelBytes);
        }
    }

    internal long MaximumSixelBytes
    {
        get
        {
            lock (_lock)
                return Math.Max(0, MaximumBytes - _kgpBytes - _kgpReservedBytes);
        }
    }

    internal bool CanSetSixelBytes(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        lock (_lock)
            return bytes <= MaximumBytes - _kgpBytes - _kgpReservedBytes;
    }

    internal void SetKgpBytes(long bytes, long reservedBytes = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(reservedBytes);
        lock (_lock)
        {
            if (checked(bytes + reservedBytes) > MaximumBytes - _sixelBytes)
                throw new InvalidOperationException("KGP retained bytes exceed the shared screen budget.");
            _kgpBytes = bytes;
            _kgpReservedBytes = reservedBytes;
        }
    }

    internal void SetSixelBytes(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        lock (_lock)
        {
            if (bytes > MaximumBytes - _kgpBytes - _kgpReservedBytes)
                throw new InvalidOperationException("Sixel retained bytes exceed the shared screen budget.");
            _sixelBytes = bytes;
        }
    }
}

internal sealed class TerminalGraphicsRetainedBudgetSet
{
    internal TerminalGraphicsRetainedBudgetSet(
        long maximumBytesPerScreen,
        int maximumInputBytes = 1024 * 1024,
        long maximumRasterPixels = 16L * 1024 * 1024)
    {
        Main = new TerminalGraphicsRetainedBudget(maximumBytesPerScreen, maximumInputBytes, maximumRasterPixels);
        Alternate = new TerminalGraphicsRetainedBudget(maximumBytesPerScreen, maximumInputBytes, maximumRasterPixels);
    }

    internal TerminalGraphicsRetainedBudget Main { get; }

    internal TerminalGraphicsRetainedBudget Alternate { get; }
}
