using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Internal widget for a calendar day cell. Carries state needed by
/// <see cref="CalendarDayNode"/> to apply theme colors during rendering.
/// The node adapts its format based on column width.
/// </summary>
internal sealed record CalendarDayWidget(int Day, bool IsCurrentDay, bool IsSelected, bool IsCellFocused, bool IsCellHovered) : Hex1bWidget
{
    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as CalendarDayNode ?? new CalendarDayNode();
        node.Day = Day;
        node.IsCurrentDay = IsCurrentDay;
        node.IsSelected = IsSelected;
        node.IsCellFocused = IsCellFocused;
        node.IsCellHovered = IsCellHovered;
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(CalendarDayNode);
}
