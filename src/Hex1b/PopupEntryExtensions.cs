using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for <see cref="PopupEntry"/> fluent configuration.
/// </summary>
public static class PopupEntryExtensions
{
    /// <summary>
    /// Marks this popup as a barrier that stops cascade dismissal.
    /// </summary>
    /// <remarks>
    /// When clicking away on a non-barrier popup, the stack unwinds to the nearest barrier.
    /// When clicking away on a barrier popup, only that single layer is dismissed.
    /// </remarks>
    /// <param name="entry">The popup entry to mark as a barrier.</param>
    /// <returns>The popup stack for method chaining.</returns>
    public static PopupStack AsBarrier(this PopupEntry entry)
    {
        entry.IsBarrier = true;
        return entry.Stack;
    }
}
