using System.Globalization;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A date picker widget that shows a trigger field displaying the selected date.
/// When activated, opens a popup with a multi-step drill-down: year → month → calendar.
/// </summary>
public sealed record DatePickerWidget : Hex1bWidget
{
    /// <summary>
    /// The initial date to pre-select. If null, no date is selected.
    /// </summary>
    internal DateOnly? InitialDate { get; init; }

    /// <summary>
    /// Placeholder text shown when no date is selected.
    /// </summary>
    internal string Placeholder { get; init; } = "Select date...";

    /// <summary>
    /// Format string for displaying the selected date. If null, uses short date format.
    /// </summary>
    internal string? DateFormat { get; init; }

    /// <summary>
    /// The first day of the week for the calendar step.
    /// </summary>
    internal DayOfWeek FirstDayOfWeek { get; init; } = DayOfWeek.Sunday;

    /// <summary>
    /// Event handler invoked when a date is selected.
    /// </summary>
    internal Func<DatePickerDateSelectedEventArgs, Task>? SelectedHandler { get; init; }

    /// <summary>
    /// Registers a synchronous handler for date selection.
    /// </summary>
    public DatePickerWidget OnSelected(Action<DatePickerDateSelectedEventArgs> handler)
        => this with { SelectedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Registers an asynchronous handler for date selection.
    /// </summary>
    public DatePickerWidget OnSelected(Func<DatePickerDateSelectedEventArgs, Task> handler)
        => this with { SelectedHandler = handler };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as DatePickerNode ?? new DatePickerNode();
        node.SourceWidget = this;
        node.DateFormat = DateFormat;

        // Apply initial date once
        if (!node.HasAppliedInitialDate && InitialDate.HasValue)
        {
            node.SelectedDate = InitialDate.Value;
            node.DisplayYear = InitialDate.Value.Year;
            node.DisplayMonth = InitialDate.Value.Month;
            node.YearPageStart = InitialDate.Value.Year - 5;
            node.HasAppliedInitialDate = true;
        }
        else if (!node.HasAppliedInitialDate)
        {
            var today = DateTime.Today;
            node.YearPageStart = today.Year - 5;
            node.HasAppliedInitialDate = true;
        }

        // Set up the selected handler
        if (SelectedHandler != null)
        {
            node.SelectedAction = args => SelectedHandler(args);
        }
        else
        {
            node.SelectedAction = null;
        }

        // Build the trigger button
        var displayText = $"{node.GetDisplayText(Placeholder)} ▼";
        var button = new ButtonWidget(displayText)
            .OnClick(async e =>
            {
                node.ResetStep();
                e.PushAnchored(AnchorPosition.Below, () => BuildPopupContent(node));
            })
            .WithInputBindings(bindings =>
            {
                bindings.Key(Hex1bKey.DownArrow).Action(ctx =>
                {
                    node.ResetStep();
                    ctx.Popups.PushAnchored(node.Child!, AnchorPosition.Below,
                        () => BuildPopupContent(node), ctx.FocusedNode);
                    return Task.CompletedTask;
                }, "Open picker");
            });

        node.Child = await context.ReconcileChildAsync(node.Child, button, node);
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(DatePickerNode);

    /// <summary>
    /// Builds the popup content based on the current picker step.
    /// Called by the popup system's contentBuilder on each render frame.
    /// </summary>
    private Hex1bWidget BuildPopupContent(DatePickerNode node)
    {
        return node.Step switch
        {
            PickerStep.Year => BuildYearGrid(node),
            PickerStep.Month => BuildMonthGrid(node),
            PickerStep.Calendar => BuildCalendarStep(node),
            _ => BuildYearGrid(node),
        };
    }

    /// <summary>
    /// Builds a 4×3 grid of years with prev/next page navigation.
    /// </summary>
    private Hex1bWidget BuildYearGrid(DatePickerNode node)
    {
        var currentYear = DateTime.Today.Year;
        var selectedYear = node.SelectedDate?.Year;

        // If a page transition set a focus cell index, use that; otherwise target selected/current year
        int? focusCellOverride = node.YearFocusCellIndex;
        node.YearFocusCellIndex = null;

        var focusYear = focusCellOverride == null ? (selectedYear ?? currentYear) : (int?)null;

        var cells = new List<GridCellWidget>();
        for (int i = 0; i < 12; i++)
        {
            var year = node.YearPageStart + i;
            var capturedYear = year;
            var isSelected = year == selectedYear;
            var isCurrent = year == currentYear;
            var isFocusTarget = focusCellOverride != null ? (i == focusCellOverride) : (year == focusYear);

            var cellIndex = i;
            var row = i / 4;
            var interactable = new InteractableWidget(ic =>
            {
                var label = capturedYear.ToString();
                // When no date selected, highlight current year as a visual hint
                var showCurrent = !isSelected && isCurrent && selectedYear == null;
                return new DatePickerCellWidget(label, isSelected, showCurrent, ic.IsFocused, ic.IsHovered);
            })
            .OnClick(_ =>
            {
                node.SelectYear(capturedYear);
                return Task.CompletedTask;
            })
            .WithInputBindings(bindings =>
            {
                var col = cellIndex % 4;
                bindings.Key(Hex1bKey.LeftArrow).Action(ctx =>
                {
                    if (col == 0)
                    {
                        // Clear focus on the departing cell so the rebuilt grid
                        // doesn't have two nodes with IsFocused=true. Without this,
                        // the stale focus at index 4 can win the FocusRing scan
                        // over the RequestFocus target at index row*4+3.
                        if (ctx.FocusedNode != null) ctx.FocusedNode.IsFocused = false;
                        // Land on rightmost column, same row
                        node.YearFocusCellIndex = row * 4 + 3;
                        node.PageYearsBackward();
                        return Task.CompletedTask;
                    }
                    return NavigateGrid(ctx, -1, 12);
                }, "Left");
                bindings.Key(Hex1bKey.RightArrow).Action(ctx =>
                {
                    if (col == 3)
                    {
                        // Same as LEFT edge: clear stale focus before page transition
                        if (ctx.FocusedNode != null) ctx.FocusedNode.IsFocused = false;
                        // Land on leftmost column, same row
                        node.YearFocusCellIndex = row * 4;
                        node.PageYearsForward();
                        return Task.CompletedTask;
                    }
                    return NavigateGrid(ctx, 1, 12);
                }, "Right");
                bindings.Key(Hex1bKey.UpArrow).Action(ctx =>
                {
                    if (row > 0)
                        return NavigateGrid(ctx, -4, 12);
                    return Task.CompletedTask;
                }, "Up");
                bindings.Key(Hex1bKey.DownArrow).Action(ctx =>
                {
                    if (row < 2)
                        return NavigateGrid(ctx, 4, 12);
                    return Task.CompletedTask;
                }, "Down");
                bindings.Key(Hex1bKey.Tab).Action(ctx => DismissAndFocusNext(ctx), "Next widget");
                bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => DismissAndFocusPrevious(ctx), "Previous widget");
            });

            if (isFocusTarget)
            {
                interactable = interactable with { RequestFocus = true };
            }

            cells.Add(new GridCellWidget(interactable).Row(row).Column(cellIndex % 4));
        }

        var columnDefs = Enumerable.Range(0, 4)
            .Select(_ => new GridColumnDefinition(SizeHint.Content))
            .ToArray();
        var rowDefs = Enumerable.Range(0, 3)
            .Select(_ => new GridRowDefinition(SizeHint.Content))
            .ToArray();

        var grid = new GridWidget(cells, columnDefs, rowDefs);

        // Navigation arrows flanking the grid
        var prevIcon = new IconWidget("◀")
            .OnClick(_ =>
            {
                node.PageYearsBackward();
                return Task.CompletedTask;
            });
        var nextIcon = new IconWidget("▶")
            .OnClick(_ =>
            {
                node.PageYearsForward();
                return Task.CompletedTask;
            });

        // Outer grid: year grid in middle column (added first for focus order)
        var outerCells = new List<GridCellWidget>
        {
            new GridCellWidget(grid).Row(0).Column(1),
            new GridCellWidget(prevIcon).Row(0).Column(0),
            new GridCellWidget(nextIcon).Row(0).Column(2),
        };
        var outerCols = new[]
        {
            new GridColumnDefinition(SizeHint.Content),
            new GridColumnDefinition(SizeHint.Content),
            new GridColumnDefinition(SizeHint.Content),
        };
        var outerRows = new[] { new GridRowDefinition(SizeHint.Content) };
        var layout = new GridWidget(outerCells, outerCols, outerRows);

        return new BorderWidget(layout)
            .Title($"{node.YearPageStart}–{node.YearPageStart + 11}")
            .WithInputBindings(bindings =>
            {
                bindings.Key(Hex1bKey.Escape).Action(DismissPopup, "Close picker");
                bindings.Key(Hex1bKey.Backspace).Action(DismissPopup, "Close picker");
                bindings.Key(Hex1bKey.PageUp).Action(_ =>
                {
                    node.PageYearsBackward();
                    return Task.CompletedTask;
                }, "Previous years");
                bindings.Key(Hex1bKey.PageDown).Action(_ =>
                {
                    node.PageYearsForward();
                    return Task.CompletedTask;
                }, "Next years");
            });
    }

    /// <summary>
    /// Builds a 4×3 grid of months.
    /// </summary>
    private Hex1bWidget BuildMonthGrid(DatePickerNode node)
    {
        var today = DateTime.Today;
        var selectedMonth = node.SelectedDate?.Month;
        var dtf = CultureInfo.CurrentCulture.DateTimeFormat;
        // Focus target: selected month if viewing selected year, else current month if viewing current year
        int? focusMonth = (node.DisplayYear == node.SelectedDate?.Year) ? selectedMonth
                        : (node.DisplayYear == today.Year) ? today.Month
                        : null;

        var cells = new List<GridCellWidget>();
        for (int i = 0; i < 12; i++)
        {
            var month = i + 1;
            var capturedMonth = month;
            var isSelected = node.DisplayYear == node.SelectedDate?.Year && month == selectedMonth;
            var isCurrent = node.DisplayYear == today.Year && month == today.Month;
            var isFocusTarget = month == focusMonth;
            var label = dtf.AbbreviatedMonthNames[i];
            var row = i / 4;

            var interactable = new InteractableWidget(ic =>
            {
                // When no date selected (or selected year differs), highlight current month
                var showCurrent = !isSelected && isCurrent && selectedMonth == null;
                return new DatePickerCellWidget(label, isSelected, showCurrent, ic.IsFocused, ic.IsHovered);
            })
            .OnClick(_ =>
            {
                node.SelectMonth(capturedMonth);
                return Task.CompletedTask;
            })
            .WithInputBindings(bindings =>
            {
                bindings.Key(Hex1bKey.LeftArrow).Action(ctx => NavigateGrid(ctx, -1, 12), "Left");
                bindings.Key(Hex1bKey.RightArrow).Action(ctx => NavigateGrid(ctx, 1, 12), "Right");
                bindings.Key(Hex1bKey.UpArrow).Action(ctx =>
                {
                    if (row > 0)
                        return NavigateGrid(ctx, -4, 12);
                    return Task.CompletedTask;
                }, "Up");
                bindings.Key(Hex1bKey.DownArrow).Action(ctx =>
                {
                    if (row < 2)
                        return NavigateGrid(ctx, 4, 12);
                    return Task.CompletedTask;
                }, "Down");
                bindings.Key(Hex1bKey.Tab).Action(ctx => DismissAndFocusNext(ctx), "Next widget");
                bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => DismissAndFocusPrevious(ctx), "Previous widget");
            });

            if (isFocusTarget)
            {
                interactable = interactable with { RequestFocus = true };
            }

            var col = i % 4;
            cells.Add(new GridCellWidget(interactable).Row(row).Column(col));
        }

        var columnDefs = Enumerable.Range(0, 4)
            .Select(_ => new GridColumnDefinition(SizeHint.Content))
            .ToArray();
        var rowDefs = Enumerable.Range(0, 3)
            .Select(_ => new GridRowDefinition(SizeHint.Content))
            .ToArray();

        var grid = new GridWidget(cells, columnDefs, rowDefs);

        // Title shows selected year; Escape goes back to year step
        var yearLabel = new TextBlockWidget(node.DisplayYear.ToString());
        var content = new VStackWidget([yearLabel, grid]);

        return new BorderWidget(content).Title("Month")
            .WithInputBindings(bindings =>
            {
                bindings.Key(Hex1bKey.Escape).Action(DismissPopup, "Close picker");
                bindings.Key(Hex1bKey.Backspace).Action(DismissPopup, "Close picker");
            });
    }

    /// <summary>
    /// Builds a compact calendar for the selected year/month.
    /// </summary>
    private Hex1bWidget BuildCalendarStep(DatePickerNode node)
    {
        var month = new DateOnly(node.DisplayYear, node.DisplayMonth, 1);
        var today = DateTime.Today;
        var calendar = new CalendarWidget(month)
            .Compact()
            .OnSelected(args =>
            {
                node.SelectedDate = args.SelectedDate;

                // Dismiss the popup
                args.Context.Popups.Pop();

                // Fire the date picker's own handler
                if (node.SelectedAction != null && node.SourceWidget != null)
                {
                    var pickerArgs = new DatePickerDateSelectedEventArgs(
                        node.SourceWidget, node, args.Context, args.SelectedDate);
                    return node.SelectedAction(pickerArgs);
                }

                return Task.CompletedTask;
            });

        calendar = calendar with { FirstDayOfWeek = FirstDayOfWeek };

        // Highlight current day when viewing the current month
        if (node.DisplayYear == today.Year && node.DisplayMonth == today.Month)
        {
            calendar = calendar with { HighlightCurrentDay = true };
        }

        // Pre-select the day if a date was previously selected in this month
        if (node.SelectedDate is { } sel && sel.Year == node.DisplayYear && sel.Month == node.DisplayMonth)
        {
            calendar = calendar with { InitialSelectedDay = sel.Day };
        }

        // Focus target: selected day if viewing selected month, else today if viewing current month
        int? focusDay = null;
        if (node.SelectedDate is { } s && s.Year == node.DisplayYear && s.Month == node.DisplayMonth)
        {
            focusDay = s.Day;
        }
        else if (node.DisplayYear == today.Year && node.DisplayMonth == today.Month)
        {
            focusDay = today.Day;
        }

        calendar = calendar with
        {
            FocusDay = focusDay,
            CellTabBindings = bindings =>
            {
                bindings.Key(Hex1bKey.Tab).Action(ctx => DismissAndFocusNext(ctx), "Next widget");
                bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => DismissAndFocusPrevious(ctx), "Previous widget");
            }
        };

        var headerText = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(node.DisplayMonth)} {node.DisplayYear}";
        var headerLabel = new TextBlockWidget(headerText);

        var daysInMonth = DateTime.DaysInMonth(node.DisplayYear, node.DisplayMonth);
        var content = new VStackWidget([headerLabel, calendar]);

        return new BorderWidget(content).Title("Day")
            .WithInputBindings(bindings =>
            {
                bindings.Key(Hex1bKey.Escape).Action(DismissPopup, "Close picker");
                bindings.Key(Hex1bKey.Backspace).Action(DismissPopup, "Close picker");
            });
    }

    /// <summary>
    /// Moves focus by <paramref name="delta"/> positions relative to the currently
    /// focused node in the focusables list.
    /// </summary>
    private static Task NavigateGrid(InputBindingActionContext ctx, int delta, int cellCount)
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

    /// <summary>
    /// Dismisses the DatePicker popup and restores focus to the trigger button.
    /// </summary>
    private static Task DismissPopup(InputBindingActionContext ctx)
    {
        if (ctx.Popups.Pop(out var restoreNode) && restoreNode != null)
        {
            restoreNode.IsFocused = true;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Dismisses the DatePicker popup and moves focus to the next sibling widget.
    /// </summary>
    private static Task DismissAndFocusNext(InputBindingActionContext ctx)
    {
        ctx.Popups.Pop();
        ctx.FocusNext();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Dismisses the DatePicker popup and moves focus to the previous sibling widget.
    /// </summary>
    private static Task DismissAndFocusPrevious(InputBindingActionContext ctx)
    {
        ctx.Popups.Pop();
        ctx.FocusPrevious();
        return Task.CompletedTask;
    }
}
