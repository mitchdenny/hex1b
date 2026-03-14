using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="CalendarWidget"/>. Wraps an internally-built
/// <see cref="GridNode"/> that contains interactive day cells. The grid's
/// child <see cref="InteractableNode"/>s handle focus and click events
/// individually — this node delegates layout, rendering, and focus traversal
/// to the child grid.
/// </summary>
public sealed class CalendarNode : Hex1bNode
{
    /// <summary>
    /// The child grid node containing the calendar layout.
    /// </summary>
    public Hex1bNode? Child { get; set; }

    /// <summary>
    /// The month being displayed. Only Year and Month are used.
    /// </summary>
    public DateOnly Month { get; set; }

    /// <summary>
    /// The number of days in the displayed month.
    /// </summary>
    public int DaysInMonth { get; set; }

    /// <summary>
    /// The first day of the week for column ordering.
    /// </summary>
    public DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Sunday;

    /// <summary>
    /// The source widget that was reconciled into this node.
    /// </summary>
    public CalendarWidget? SourceWidget { get; set; }

    /// <summary>
    /// The currently selected day (1-based). Preserved across reconciliation.
    /// </summary>
    private int _selectedDay = 1;
    public int SelectedDay
    {
        get => _selectedDay;
        set
        {
            var clamped = Math.Clamp(value, 1, Math.Max(1, DaysInMonth));
            if (_selectedDay != clamped)
            {
                _selectedDay = clamped;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => false;

    protected override Size MeasureCore(Constraints constraints)
    {
        return Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);
        Child?.Arrange(bounds);
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Child != null)
        {
            context.RenderChild(Child);
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null)
            yield return Child;
    }

    /// <summary>
    /// Delegates focusable nodes to the child grid so individual day cells
    /// (wrapped in <see cref="InteractableNode"/>) appear in the focus ring.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }
}
