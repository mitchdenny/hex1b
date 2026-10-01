namespace Hex1b;

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
