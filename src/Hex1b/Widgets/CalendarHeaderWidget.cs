using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Internal widget for a calendar day-of-week header cell. The corresponding
/// <see cref="CalendarHeaderNode"/> selects the best label format at render
/// time based on the narrowest column width across all headers.
/// </summary>
internal sealed record CalendarHeaderWidget(DayOfWeek DayOfWeek, HeaderColumnTracker ColumnTracker) : Hex1bWidget
{
    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as CalendarHeaderNode ?? new CalendarHeaderNode();
        node.DayOfWeek = DayOfWeek;
        node.ColumnTracker = ColumnTracker;
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(CalendarHeaderNode);
}
