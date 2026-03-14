using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="CalendarWidget"/>. Wraps an internally-built
/// <see cref="GridNode"/> that contains the actual calendar cells, and manages
/// focus, selection state, and keyboard navigation.
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

    /// <summary>
    /// The async action to execute when a day is selected.
    /// </summary>
    internal Func<InputBindingActionContext, Task>? SelectAction { get; set; }

    private bool _isFocused;
    public override bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
            }
        }
    }

    private bool _isHovered;
    public override bool IsHovered
    {
        get => _isHovered;
        set
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        bindings.Key(Hex1bKey.LeftArrow).Triggers(CalendarWidget.MoveLeft, NavigateLeft, "Previous day");
        bindings.Key(Hex1bKey.RightArrow).Triggers(CalendarWidget.MoveRight, NavigateRight, "Next day");
        bindings.Key(Hex1bKey.UpArrow).Triggers(CalendarWidget.MoveUp, NavigateUp, "Previous week");
        bindings.Key(Hex1bKey.DownArrow).Triggers(CalendarWidget.MoveDown, NavigateDown, "Next week");
        bindings.Key(Hex1bKey.Enter).Triggers(CalendarWidget.Select, SelectDay, "Select day");
        bindings.Key(Hex1bKey.Spacebar).Triggers(CalendarWidget.Select, SelectDay, "Select day");
    }

    private Task NavigateLeft(InputBindingActionContext ctx)
    {
        if (SelectedDay > 1)
            SelectedDay--;
        return Task.CompletedTask;
    }

    private Task NavigateRight(InputBindingActionContext ctx)
    {
        if (SelectedDay < DaysInMonth)
            SelectedDay++;
        return Task.CompletedTask;
    }

    private Task NavigateUp(InputBindingActionContext ctx)
    {
        if (SelectedDay > 7)
            SelectedDay -= 7;
        return Task.CompletedTask;
    }

    private Task NavigateDown(InputBindingActionContext ctx)
    {
        if (SelectedDay + 7 <= DaysInMonth)
            SelectedDay += 7;
        return Task.CompletedTask;
    }

    private async Task SelectDay(InputBindingActionContext ctx)
    {
        if (SelectAction != null)
        {
            await SelectAction(ctx);
        }
    }

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

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        yield return this;
    }
}
