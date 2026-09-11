namespace Hex1b;

/// <summary>
/// Shared retained-image byte accounting for one terminal screen.
/// </summary>
internal sealed class TerminalGraphicsRetainedBudget
{
    private readonly object _lock = new();
    private long _kgpBytes;
    private long _sixelBytes;

    internal TerminalGraphicsRetainedBudget(long maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        MaximumBytes = maximumBytes;
    }

    internal long MaximumBytes { get; }

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
                return Math.Max(0, MaximumBytes - _kgpBytes);
        }
    }

    internal bool CanSetSixelBytes(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        lock (_lock)
            return bytes <= MaximumBytes - _kgpBytes;
    }

    internal void SetKgpBytes(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        lock (_lock)
        {
            if (bytes > MaximumBytes - _sixelBytes)
                throw new InvalidOperationException("KGP retained bytes exceed the shared screen budget.");
            _kgpBytes = bytes;
        }
    }

    internal void SetSixelBytes(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        lock (_lock)
        {
            if (bytes > MaximumBytes - _kgpBytes)
                throw new InvalidOperationException("Sixel retained bytes exceed the shared screen budget.");
            _sixelBytes = bytes;
        }
    }
}

internal sealed class TerminalGraphicsRetainedBudgetSet
{
    internal TerminalGraphicsRetainedBudgetSet(long maximumBytesPerScreen)
    {
        Main = new TerminalGraphicsRetainedBudget(maximumBytesPerScreen);
        Alternate = new TerminalGraphicsRetainedBudget(maximumBytesPerScreen);
    }

    internal TerminalGraphicsRetainedBudget Main { get; }

    internal TerminalGraphicsRetainedBudget Alternate { get; }
}
