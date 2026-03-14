using System.Globalization;
using Hex1b.Layout;
using Hex1b.Nodes;

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
    /// Optional title displayed above the calendar (e.g. "March 2026").
    /// When null, no title row is shown.
    /// </summary>
    internal string? Title { get; init; }

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var gridWidget = BuildGridWidget();
        return await gridWidget.ReconcileAsync(existingNode, context);
    }

    internal override Type GetExpectedNodeType() => typeof(GridNode);

    /// <summary>
    /// Builds the internal <see cref="GridWidget"/> representing the calendar month layout.
    /// </summary>
    internal GridWidget BuildGridWidget()
    {
        var firstOfMonth = new DateOnly(Month.Year, Month.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(Month.Year, Month.Month);
        var today = Today ?? DateOnly.FromDateTime(DateTime.Today);

        // Calculate the offset of day 1 in the grid
        var firstDayOffset = ((int)firstOfMonth.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;

        // Calculate how many week rows we need
        var totalSlots = firstDayOffset + daysInMonth;
        var weekRows = (totalSlots + 6) / 7; // ceiling division

        var cells = new List<GridCellWidget>();
        var currentRow = 0;

        // Header row with day-of-week labels
        if (ShowHeader)
        {
            for (int col = 0; col < 7; col++)
            {
                var dayOfWeek = (DayOfWeek)(((int)FirstDayOfWeek + col) % 7);
                var label = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[(int)dayOfWeek];
                cells.Add(new GridCellWidget(new TextBlockWidget(label.PadLeft(3).PadRight(4)))
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

            var dayText = day.ToString().PadLeft(2);
            if (isToday)
            {
                dayText = $"[{dayText}]";
            }
            else
            {
                dayText = $" {dayText} ";
            }

            cells.Add(new GridCellWidget(new TextBlockWidget(dayText))
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

        return new GridWidget(cells, columnDefs, rowDefs);
    }
}
