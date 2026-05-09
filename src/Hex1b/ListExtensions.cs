namespace Hex1b;

using Hex1b.Events;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for building ListWidget.
/// </summary>
public static class ListExtensions
{
    /// <summary>
    /// Creates a List with the specified items.
    /// </summary>
    public static ListWidget List<TParent>(
        this WidgetContext<TParent> context,
        IReadOnlyList<string> items)
        where TParent : Hex1bWidget
        => new(items);
}
