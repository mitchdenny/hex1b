using System.Globalization;
using Hex1b.Events;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A calendar widget that displays a month view, composed from <see cref="GridWidget"/>.
/// The grid has 7 columns (one per day of the week) and enough rows to display a header
/// plus all weeks of the specified month.
/// </summary>
/// <param name="Month">The month to display. Only the Year and Month components are used.</param>
public sealed record CalendarWidget(DateOnly Month) : Hex1bWidget
{

    /// <summary>
    /// Whether to show the day-of-week header row (Sun, Mon, Tue, etc.). Defaults to true.
    /// </summary>
    internal bool ShowHeader { get; init; } = true;

    /// <summary>
    /// The first day of the week. Defaults to <see cref="DayOfWeek.Sunday"/>.
    /// </summary>
    internal DayOfWeek FirstDayOfWeek { get; init; } = DayOfWeek.Sunday;

    /// <summary>
    /// The "today" date used for highlighting the current day.
    /// Defaults to today's date if not specified.
    /// </summary>
    internal DateOnly? Today { get; init; }

    /// <summary>
    /// When true, renders without gridlines (compact mode for DatePicker embedding).
    /// When false (default), renders with gridlines around each cell.
    /// </summary>
    internal bool IsCompact { get; init; }

    /// <summary>
    /// Optional callback to provide custom content for each day cell.
    /// The callback receives a <see cref="CalendarDayContext"/> and returns
    /// an optional widget to render alongside the day number.
    /// </summary>
    internal Func<CalendarDayContext, Hex1bWidget?>? DayBuilder { get; init; }

    /// <summary>
    /// The async handler for day selection events.
    /// </summary>
    internal Func<CalendarDateSelectedEventArgs, Task>? SelectedHandler { get; init; }

    /// <summary>
    /// Sets a synchronous handler for day selection events.
    /// </summary>
    public CalendarWidget OnSelected(Action<CalendarDateSelectedEventArgs> handler)
        => this with { SelectedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler for day selection events.
    /// </summary>
    public CalendarWidget OnSelected(Func<CalendarDateSelectedEventArgs, Task> handler)
        => this with { SelectedHandler = handler };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as CalendarNode ?? new CalendarNode();

        var daysInMonth = DateTime.DaysInMonth(Month.Year, Month.Month);
        node.Month = new DateOnly(Month.Year, Month.Month, 1);
        node.DaysInMonth = daysInMonth;
        node.FirstDayOfWeek = FirstDayOfWeek;
        node.SourceWidget = this;

        // Clamp selected day to valid range after month change
        if (node.SelectedDay > daysInMonth)
        {
            node.SelectedDay = daysInMonth;
        }

        // Build the inner grid widget with interactive day cells
        var gridWidget = BuildGridWidget(node, daysInMonth);

        // Reconcile the grid as a child of this node
        node.Child = await context.ReconcileChildAsync(node.Child, gridWidget, node);

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(CalendarNode);

    /// <summary>
    /// Builds the internal <see cref="GridWidget"/> representing the calendar month layout.
    /// Each day cell is wrapped in an <see cref="InteractableWidget"/> so it is individually
    /// focusable, clickable, and hoverable.
    /// </summary>
    internal GridWidget BuildGridWidget(CalendarNode node, int daysInMonth)
    {
        var selectedDay = node.SelectedDay;
        var firstOfMonth = new DateOnly(Month.Year, Month.Month, 1);
        var today = Today ?? DateOnly.FromDateTime(DateTime.Today);

        // In compact mode, skip the Day builder entirely
        var dayBuilder = IsCompact ? null : DayBuilder;

        // Calculate the offset of day 1 in the grid
        var firstDayOffset = ((int)firstOfMonth.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;

        // Calculate how many week rows we need
        var totalSlots = firstDayOffset + daysInMonth;
        var weekRows = (totalSlots + 6) / 7;

        var cells = new List<GridCellWidget>();
        var currentRow = 0;

        // Header row with day-of-week labels
        if (ShowHeader)
        {
            for (int col = 0; col < 7; col++)
            {
                var dayOfWeek = (DayOfWeek)(((int)FirstDayOfWeek + col) % 7);
                var label = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[(int)dayOfWeek];
                // Pad to 3 chars, center-ish in 4-char cell
                var headerText = label.Length > 3 ? label[..3] : label;
                headerText = headerText.PadLeft(3).PadRight(4);

                cells.Add(new GridCellWidget(new TextBlockWidget(headerText))
                    .Row(currentRow).Column(col));
            }

            currentRow++;
        }

        // Day cells — each wrapped in InteractableWidget for click/focus support
        for (int day = 1; day <= daysInMonth; day++)
        {
            var dayOffset = firstDayOffset + (day - 1);
            var col = dayOffset % 7;
            var row = currentRow + (dayOffset / 7);

            var dateForDay = new DateOnly(Month.Year, Month.Month, day);
            var isToday = dateForDay == today;
            var isWeekend = dateForDay.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            // Capture for closure
            var capturedDay = day;
            var capturedDate = dateForDay;

            var interactable = new InteractableWidget(ic =>
            {
                var isSelected = capturedDay == node.SelectedDay;
                var dayText = BuildDayText(capturedDay, isToday, isSelected, ic.IsFocused);

                if (dayBuilder != null)
                {
                    var dayContext = new CalendarDayContext(capturedDate, isToday, isSelected, isWeekend, capturedDate.DayOfWeek);
                    var customContent = dayBuilder(dayContext);

                    if (customContent != null)
                    {
                        return new HStackWidget([new TextBlockWidget(dayText), customContent]);
                    }
                }

                return new TextBlockWidget(dayText);
            })
            .OnClick(args =>
            {
                node.SelectedDay = capturedDay;

                if (SelectedHandler != null)
                {
                    var selectedDate = new DateOnly(Month.Year, Month.Month, capturedDay);
                    var eventArgs = new CalendarDateSelectedEventArgs(this, node, args.Context, selectedDate);
                    return SelectedHandler(eventArgs);
                }

                return Task.CompletedTask;
            });

            cells.Add(new GridCellWidget(interactable)
                .Row(row).Column(col));
        }

        // Column sizing: Fill distributes space evenly so all columns are uniform.
        // In compact mode, use Content sizing since all day cells are the same width.
        var colHint = IsCompact ? SizeHint.Content : SizeHint.Fill;
        var columnDefs = new GridColumnDefinition[7];
        for (int i = 0; i < 7; i++)
        {
            columnDefs[i] = new GridColumnDefinition(colHint);
        }

        // Row sizing: Fill distributes space evenly for uniform row heights.
        // In compact mode, use Content sizing for minimal height.
        var rowHint = IsCompact ? SizeHint.Content : SizeHint.Fill;
        var totalRows = currentRow + weekRows;
        var rowDefs = new GridRowDefinition[totalRows];
        for (int i = 0; i < totalRows; i++)
        {
            rowDefs[i] = new GridRowDefinition(rowHint);
        }

        var gridWidget = new GridWidget(cells, columnDefs, rowDefs);

        if (!IsCompact)
        {
            gridWidget = gridWidget with { GridLines = GridLinesMode.All };
        }

        return gridWidget;
    }

    /// <summary>
    /// Builds the ANSI-styled text for a day number.
    /// </summary>
    private static string BuildDayText(int day, bool isToday, bool isSelected, bool isFocused)
    {
        var dayStr = day.ToString().PadLeft(2);

        if (isToday || isSelected || isFocused)
        {
            return $"\x1b[7m {dayStr} \x1b[0m";
        }

        return $" {dayStr} ";
    }
}
