using Hex1b.Events;
using Hex1b.Input;
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
    /// When true, renders without gridlines (compact mode for DatePicker embedding).
    /// When false (default), renders with gridlines around each cell.
    /// </summary>
    internal bool IsCompact { get; init; }

    /// <summary>
    /// When true, highlights the current day (from <see cref="Today"/>) with theme colors.
    /// Defaults to false (opt-in).
    /// </summary>
    internal bool HighlightCurrentDay { get; init; }

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
    /// The initial day to pre-select when the calendar is first created.
    /// If null (default), no day is selected until the user picks one.
    /// </summary>
    internal int? InitialSelectedDay { get; init; }

    /// <summary>
    /// The day to focus when the calendar is rendered. Used by DatePicker to
    /// direct focus to the selected or current day cell.
    /// </summary>
    internal int? FocusDay { get; init; }

    /// <summary>
    /// Optional Tab/Shift-Tab handler added to each day cell.
    /// Used by DatePicker to dismiss the popup on Tab instead of cycling cells.
    /// </summary>
    internal Action<InputBindingsBuilder>? CellTabBindings { get; init; }

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
        var isNewNode = existingNode is not CalendarNode;
        var node = existingNode as CalendarNode ?? new CalendarNode();

        var daysInMonth = DateTime.DaysInMonth(Month.Year, Month.Month);
        node.Month = new DateOnly(Month.Year, Month.Month, 1);
        node.DaysInMonth = daysInMonth;
        node.FirstDayOfWeek = FirstDayOfWeek;
        node.SourceWidget = this;

        // Apply initial selected day on first creation
        if (isNewNode && InitialSelectedDay.HasValue)
        {
            node.SelectedDay = InitialSelectedDay.Value;
        }

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
            var columnTracker = new HeaderColumnTracker();
            for (int col = 0; col < 7; col++)
            {
                var dayOfWeek = (DayOfWeek)(((int)FirstDayOfWeek + col) % 7);
                cells.Add(new GridCellWidget(new CalendarHeaderWidget(dayOfWeek, columnTracker))
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
                var showCurrentDay = HighlightCurrentDay && isToday;
                var dayWidget = new CalendarDayWidget(capturedDay, showCurrentDay, isSelected, ic.IsFocused, ic.IsHovered);

                if (dayBuilder != null)
                {
                    var dayContext = new CalendarDayContext(capturedDate, isToday, isSelected, isWeekend, capturedDate.DayOfWeek);
                    var customContent = dayBuilder(dayContext);

                    if (customContent != null)
                    {
                        return new HStackWidget([dayWidget, customContent]);
                    }
                }

                return dayWidget;
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
            })
            .WithInputBindings(bindings =>
            {
                bindings.Key(Hex1bKey.LeftArrow).Action(ctx => NavigateCalendarGrid(ctx, -1, daysInMonth), "Left");
                bindings.Key(Hex1bKey.RightArrow).Action(ctx => NavigateCalendarGrid(ctx, 1, daysInMonth), "Right");
                bindings.Key(Hex1bKey.UpArrow).Action(ctx => NavigateCalendarGrid(ctx, -7, daysInMonth), "Up");
                bindings.Key(Hex1bKey.DownArrow).Action(ctx => NavigateCalendarGrid(ctx, 7, daysInMonth), "Down");
                CellTabBindings?.Invoke(bindings);
            });

            if (FocusDay == capturedDay)
            {
                interactable = interactable with { RequestFocus = true };
            }

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

        // Row sizing: Content keeps rows sized to their content. Fill rows would
        // expand to consume all available height, which causes lockups when a
        // parent (e.g. VStack) passes unconstrained MaxHeight.
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
    /// Moves focus by <paramref name="delta"/> positions relative to the currently
    /// focused node in the focusables list.
    /// </summary>
    private static Task NavigateCalendarGrid(InputBindingActionContext ctx, int delta, int cellCount)
    {
        var focusables = ctx.Focusables;
        var focused = ctx.FocusedNode;
        if (focused == null) return Task.CompletedTask;

        var idx = -1;
        for (int i = 0; i < focusables.Count; i++)
        {
            if (focusables[i] == focused) { idx = i; break; }
        }

        if (idx < 0) return Task.CompletedTask;

        var target = idx + delta;
        if (target >= 0 && target < focusables.Count)
        {
            ctx.Focus(focusables[target]);
        }

        return Task.CompletedTask;
    }

}
