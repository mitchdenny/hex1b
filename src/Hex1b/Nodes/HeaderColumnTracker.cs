using System.Globalization;
using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Nodes;

/// <summary>
/// Tracks the minimum column width across all calendar headers so they
/// can consistently select the same format level.
/// </summary>
internal sealed class HeaderColumnTracker
{
    public int MinWidth { get; private set; } = int.MaxValue;

    public void Report(int width)
    {
        if (width < MinWidth)
            MinWidth = width;
    }
}
