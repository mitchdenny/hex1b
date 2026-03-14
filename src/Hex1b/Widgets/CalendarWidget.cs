using System.Globalization;
using Hex1b.Events;
using Hex1b.Input;
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
    /// <summary>Rebindable action: Move selection left (previous day).</summary>
    public static readonly ActionId MoveLeft = new($"{nameof(CalendarWidget)}.{nameof(MoveLeft)}");
    /// <summary>Rebindable action: Move selection right (next day).</summary>
    public static readonly ActionId MoveRight = new($"{nameof(CalendarWidget)}.{nameof(MoveRight)}");
    /// <summary>Rebindable action: Move selection up (previous week).</summary>
    public static readonly ActionId MoveUp = new($"{nameof(CalendarWidget)}.{nameof(MoveUp)}");
    /// <summary>Rebindable action: Move selection down (next week).</summary>
    public static readonly ActionId MoveDown = new($"{nameof(CalendarWidget)}.{nameof(MoveDown)}");
    /// <summary>Rebindable action: Select the current day.</summary>
    public static readonly ActionId Select = new($"{nameof(CalendarWidget)}.{nameof(Select)}");

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

        // Wire up the select action
        if (SelectedHandler != null)
        {
            node.SelectAction = async ctx =>
            {
                var selectedDate = new DateOnly(Month.Year, Month.Month, node.SelectedDay);
                var args = new CalendarDateSelectedEventArgs(this, node, ctx, selectedDate);
                await SelectedHandler(args);
            };
        }
        else
        {
            node.SelectAction = null;
        }

        // Build the inner grid widget
        var gridWidget = BuildGridWidget(node.SelectedDay, daysInMonth);

        // Reconcile the grid as a child of this node
        node.Child = await context.ReconcileChildAsync(node.Child, gridWidget, node);

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(CalendarNode);

    /// <summary>
    /// Builds the internal <see cref="GridWidget"/> representing the calendar month layout.
    /// </summary>
    internal GridWidget BuildGridWidget(int selectedDay, int daysInMonth)
    {
        var firstOfMonth = new DateOnly(Month.Year, Month.Month, 1);
        var today = Today ?? DateOnly.FromDateTime(DateTime.Today);

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

        // Day cells
        for (int day = 1; day <= daysInMonth; day++)
        {
            var dayOffset = firstDayOffset + (day - 1);
            var col = dayOffset % 7;
            var row = currentRow + (dayOffset / 7);

            var dateForDay = new DateOnly(Month.Year, Month.Month, day);
            var isToday = dateForDay == today;
            var isSelected = day == selectedDay;
            var isWeekend = dateForDay.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            // Build the day number text with ANSI styling
            var dayText = BuildDayText(day, isToday, isSelected);

            // Check for Day builder content
            Hex1bWidget cellContent;
            if (DayBuilder != null)
            {
                var dayContext = new CalendarDayContext(dateForDay, isToday, isSelected, isWeekend, dateForDay.DayOfWeek);
                var customContent = DayBuilder(dayContext);

                if (customContent != null)
                {
                    // HStack: [day number, custom content]
                    cellContent = new HStackWidget([new TextBlockWidget(dayText), customContent]);
                }
                else
                {
                    cellContent = new TextBlockWidget(dayText);
                }
            }
            else
            {
                cellContent = new TextBlockWidget(dayText);
            }

            cells.Add(new GridCellWidget(cellContent)
                .Row(row).Column(col));
        }

        // Build column definitions: 7 columns, all content-sized
        var columnDefs = new GridColumnDefinition[7];
        for (int i = 0; i < 7; i++)
        {
            columnDefs[i] = new GridColumnDefinition(SizeHint.Content);
        }

        // Build row definitions
        var totalRows = currentRow + weekRows;
        var rowDefs = new GridRowDefinition[totalRows];
        for (int i = 0; i < totalRows; i++)
        {
            rowDefs[i] = new GridRowDefinition(SizeHint.Content);
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
    private static string BuildDayText(int day, bool isToday, bool isSelected)
    {
        var dayStr = day.ToString().PadLeft(2);

        if (isToday && isSelected)
        {
            // Both today and selected: use reverse video
            return $"\x1b[7m {dayStr} \x1b[0m";
        }
        else if (isToday)
        {
            // Today: inverted
            return $"\x1b[7m {dayStr} \x1b[0m";
        }
        else if (isSelected)
        {
            // Selected: reverse video (visually distinct when focused)
            return $"\x1b[7m {dayStr} \x1b[0m";
        }
        else
        {
            return $" {dayStr} ";
        }
    }
}
