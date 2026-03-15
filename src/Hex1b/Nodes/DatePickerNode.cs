using Hex1b.Events;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// The current step in the date picker drill-down flow.
/// </summary>
public enum PickerStep
{
    Year,
    Month,
    Calendar,
}

/// <summary>
/// Render node for <see cref="DatePickerWidget"/>. Wraps a <see cref="ButtonNode"/>
/// trigger and manages the multi-step picker state (year → month → calendar).
/// Delegates layout, rendering, and focus traversal to the child button.
/// </summary>
public sealed class DatePickerNode : Hex1bNode
{
    /// <summary>
    /// The child button node (trigger field).
    /// </summary>
    public Hex1bNode? Child { get; set; }

    /// <summary>
    /// The source widget that was reconciled into this node.
    /// </summary>
    public DatePickerWidget? SourceWidget { get; set; }

    /// <summary>
    /// The currently selected date, or null if no date is selected.
    /// </summary>
    public DateOnly? SelectedDate { get; set; }

    /// <summary>
    /// The current step in the picker flow.
    /// </summary>
    public PickerStep Step { get; set; } = PickerStep.Year;

    /// <summary>
    /// The year being viewed in the month/calendar steps.
    /// </summary>
    public int DisplayYear { get; set; } = DateTime.Today.Year;

    /// <summary>
    /// The month (1-12) being viewed in the calendar step.
    /// </summary>
    public int DisplayMonth { get; set; } = DateTime.Today.Month;

    /// <summary>
    /// The first year shown on the current year grid page.
    /// </summary>
    public int YearPageStart { get; set; }

    /// <summary>
    /// Cell index (0–11) to focus after a year page change, or null for default targeting.
    /// Cleared after each rebuild so it only applies once.
    /// </summary>
    public int? YearFocusCellIndex { get; set; }

    /// <summary>
    /// Whether the initial date has been applied from the widget.
    /// </summary>
    public bool HasAppliedInitialDate { get; set; }

    /// <summary>
    /// Format string for displaying the selected date.
    /// </summary>
    public string? DateFormat { get; set; }

    /// <summary>
    /// The event handler invoked when a date is selected.
    /// </summary>
    internal Func<DatePickerDateSelectedEventArgs, Task>? SelectedAction { get; set; }

    public override bool IsFocusable => false;

    /// <summary>
    /// Advances from the year step to the month step.
    /// </summary>
    public void SelectYear(int year)
    {
        DisplayYear = year;
        Step = PickerStep.Month;
        MarkDirty();
    }

    /// <summary>
    /// Advances from the month step to the calendar step.
    /// </summary>
    public void SelectMonth(int month)
    {
        DisplayMonth = month;
        Step = PickerStep.Calendar;
        MarkDirty();
    }

    /// <summary>
    /// Goes back one step. Returns false if already at the year step
    /// (caller should dismiss the popup).
    /// </summary>
    public bool GoBack()
    {
        switch (Step)
        {
            case PickerStep.Calendar:
                Step = PickerStep.Month;
                MarkDirty();
                return true;
            case PickerStep.Month:
                Step = PickerStep.Year;
                MarkDirty();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Moves the year page forward by 12 years.
    /// </summary>
    public void PageYearsForward()
    {
        YearPageStart += 12;
        MarkDirty();
    }

    /// <summary>
    /// Moves the year page backward by 12 years.
    /// </summary>
    public void PageYearsBackward()
    {
        YearPageStart -= 12;
        MarkDirty();
    }

    /// <summary>
    /// Resets the picker to the year step for the next popup open.
    /// </summary>
    public void ResetStep()
    {
        Step = PickerStep.Year;
        // Re-center year page on selected date or current year
        var centerYear = SelectedDate?.Year ?? DateTime.Today.Year;
        YearPageStart = centerYear - 5;
    }

    /// <summary>
    /// Gets the display text for the trigger field.
    /// </summary>
    public string GetDisplayText(string? placeholder)
    {
        if (SelectedDate is { } date)
        {
            return DateFormat != null
                ? date.ToString(DateFormat)
                : date.ToShortDateString();
        }

        return placeholder ?? "Select date...";
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
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }
}
