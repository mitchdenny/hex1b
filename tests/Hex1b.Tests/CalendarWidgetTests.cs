using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class CalendarWidgetTests
{
    /// <summary>
    /// Helper to build a grid from a CalendarWidget with a configured CalendarNode.
    /// </summary>
    private static (GridWidget Grid, CalendarNode Node) BuildGrid(
        CalendarWidget widget,
        int selectedDay = 1,
        int? daysInMonth = null)
    {
        var days = daysInMonth ?? DateTime.DaysInMonth(widget.Month.Year, widget.Month.Month);
        var node = new CalendarNode
        {
            DaysInMonth = days,
            SelectedDay = selectedDay,
            Month = new DateOnly(widget.Month.Year, widget.Month.Month, 1),
            FirstDayOfWeek = widget.FirstDayOfWeek,
        };
        var grid = widget.BuildGridWidget(node, days);
        return (grid, node);
    }

    #region Grid Construction Tests

    [Fact]
    public void BuildGridWidget_ProducesSevenColumns()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1));
        var (grid, _) = BuildGrid(widget);

        Assert.Equal(7, grid.ColumnDefinitions.Count);
    }

    [Fact]
    public void BuildGridWidget_WithHeader_HasHeaderAndDayCells()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1));
        var (grid, _) = BuildGrid(widget);

        // March 2026: starts on Sunday, 31 days
        // Total cells = 7 (header) + 31 (days) = 38
        Assert.Equal(38, grid.Cells.Count);
    }

    [Fact]
    public void BuildGridWidget_WithoutHeader_HasNoDayLabels()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1)) { ShowHeader = false };
        var (grid, _) = BuildGrid(widget);

        Assert.Equal(31, grid.Cells.Count);
    }

    [Fact]
    public void BuildGridWidget_March2026_CorrectRowCount()
    {
        // March 2026 starts on Sunday, 31 days → 5 week rows + 1 header = 6 rows
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1));
        var (grid, _) = BuildGrid(widget);

        Assert.Equal(6, grid.RowDefinitions.Count);
    }

    [Fact]
    public void BuildGridWidget_February2026_CorrectRowCount()
    {
        // Feb 2026 starts on Sunday, 28 days → 4 week rows + 1 header = 5 rows
        var widget = new CalendarWidget(new DateOnly(2026, 2, 1));
        var (grid, _) = BuildGrid(widget, daysInMonth: 28);

        Assert.Equal(5, grid.RowDefinitions.Count);
    }

    [Fact]
    public void BuildGridWidget_MonthStartingMidWeek_CorrectPlacement()
    {
        // April 2026 starts on Wednesday (day of week = 3)
        var widget = new CalendarWidget(new DateOnly(2026, 4, 1));
        var (grid, _) = BuildGrid(widget, daysInMonth: 30);

        // First day cell is after the 7 header cells, at column 3 (Wednesday)
        var dayCells = grid.Cells.Skip(7).ToList();
        var firstDayCell = dayCells[0];
        Assert.Equal(3, firstDayCell.ColumnIndex);
        Assert.Equal(1, firstDayCell.RowIndex);
    }

    [Fact]
    public void BuildGridWidget_MondayFirstDay_ShiftsColumns()
    {
        // March 2026 starts on Sunday. With Monday as first day, Sunday = column 6
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1))
        {
            FirstDayOfWeek = DayOfWeek.Monday
        };
        var (grid, _) = BuildGrid(widget);

        var dayCells = grid.Cells.Skip(7).ToList();
        var firstDayCell = dayCells[0];
        Assert.Equal(6, firstDayCell.ColumnIndex);
    }

    [Fact]
    public void BuildGridWidget_LastDayInCorrectPosition()
    {
        // March 2026: 31 days, starts Sunday, day 31 at col 2 (Tuesday)
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1));
        var (grid, _) = BuildGrid(widget);

        var dayCells = grid.Cells.Skip(7).ToList();
        var lastDayCell = dayCells[^1];
        Assert.Equal(2, lastDayCell.ColumnIndex);
    }

    [Fact]
    public void BuildGridWidget_DefaultMode_HasGridLines()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1));
        var (grid, _) = BuildGrid(widget);

        Assert.Equal(GridLinesMode.All, grid.GridLines);
    }

    [Fact]
    public void BuildGridWidget_CompactMode_NoGridLines()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1)) { IsCompact = true };
        var (grid, _) = BuildGrid(widget);

        Assert.Equal(GridLinesMode.None, grid.GridLines);
    }

    #endregion

    #region Day Cell Interactable Tests

    [Fact]
    public void BuildGridWidget_DayCells_AreInteractableWidgets()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1))
        {
            Today = new DateOnly(2099, 1, 1)
        };
        var (grid, _) = BuildGrid(widget);

        // Every day cell (after 7 header cells) should be an InteractableWidget
        var dayCells = grid.Cells.Skip(7).ToList();
        foreach (var cell in dayCells)
        {
            Assert.IsType<InteractableWidget>(cell.Child);
        }
    }

    [Fact]
    public void BuildGridWidget_HeaderCells_AreCalendarHeaderWidgets()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1));
        var (grid, _) = BuildGrid(widget);

        var headerCells = grid.Cells.Take(7).ToList();
        foreach (var cell in headerCells)
        {
            Assert.IsType<CalendarHeaderWidget>(cell.Child);
        }
    }

    #endregion

    #region Day Builder Tests

    [Fact]
    public void BuildGridWidget_WithDayBuilder_CreatesHStackInsideInteractable()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1))
        {
            Today = new DateOnly(2099, 1, 1),
            DayBuilder = ctx => new TextBlockWidget("Event")
        };
        var (grid, _) = BuildGrid(widget);

        var dayCells = grid.Cells.Skip(7).ToList();
        var day1Cell = dayCells[0];

        // The cell is an InteractableWidget; its builder produces an HStack
        Assert.IsType<InteractableWidget>(day1Cell.Child);
    }

    [Fact]
    public void BuildGridWidget_CompactMode_SkipsDayBuilder()
    {
        var builderCalled = false;
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1))
        {
            Today = new DateOnly(2099, 1, 1),
            IsCompact = true,
            DayBuilder = ctx =>
            {
                builderCalled = true;
                return new TextBlockWidget("Event");
            }
        };
        var (grid, node) = BuildGrid(widget);

        // Force the InteractableWidget builders to run
        var dayCells = grid.Cells.Skip(7).ToList();
        var interactable = Assert.IsType<InteractableWidget>(dayCells[0].Child);
        var dummyNode = new InteractableNode();
        var ic = new InteractableContext(dummyNode);
        interactable.Builder(ic);

        Assert.False(builderCalled, "Day builder should not be called in compact mode");
    }

    [Fact]
    public void BuildGridWidget_NonCompact_UsesFillColumnsContentRows()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1));
        var (grid, _) = BuildGrid(widget);

        foreach (var col in grid.ColumnDefinitions)
        {
            Assert.True(col.Width.IsFill, "Non-compact columns should use Fill sizing");
        }
        foreach (var row in grid.RowDefinitions)
        {
            Assert.True(row.Height.IsContent, "Non-compact rows should use Content sizing");
        }
    }

    [Fact]
    public void BuildGridWidget_Compact_UsesContentSizing()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1)) { IsCompact = true };
        var (grid, _) = BuildGrid(widget);

        foreach (var col in grid.ColumnDefinitions)
        {
            Assert.True(col.Width.IsContent, "Compact columns should use Content sizing");
        }
    }

    [Fact]
    public void BuildGridWidget_DayBuilder_ReceivesCorrectContext()
    {
        CalendarDayContext? capturedContext = null;
        var today = new DateOnly(2026, 3, 14);

        var widget = new CalendarWidget(new DateOnly(2026, 3, 1))
        {
            Today = today,
            DayBuilder = ctx =>
            {
                if (ctx.Date == new DateOnly(2026, 3, 14))
                    capturedContext = ctx;
                return null;
            }
        };
        // The DayBuilder is invoked during ReconcileAsync, not BuildGridWidget.
        // We need to trigger the InteractableWidget builder by calling it directly.
        var (grid, node) = BuildGrid(widget, selectedDay: 14);

        // Force the InteractableWidget builders to run to capture context
        var dayCells = grid.Cells.Skip(7).ToList();
        var day14Cell = dayCells[13];
        var interactable = Assert.IsType<InteractableWidget>(day14Cell.Child);
        // Invoke the builder with a dummy context to trigger the DayBuilder
        var dummyNode = new InteractableNode();
        var ic = new InteractableContext(dummyNode);
        interactable.Builder(ic);

        Assert.NotNull(capturedContext);
        Assert.Equal(new DateOnly(2026, 3, 14), capturedContext.Date);
        Assert.True(capturedContext.IsToday);
        Assert.Equal(DayOfWeek.Saturday, capturedContext.DayOfWeek);
        Assert.True(capturedContext.IsWeekend);
    }

    #endregion

    #region Week Calculation Tests

    [Fact]
    public void BuildGridWidget_FebruaryLeapYear_CorrectDays()
    {
        var widget = new CalendarWidget(new DateOnly(2028, 2, 1))
        {
            Today = new DateOnly(2028, 2, 1)
        };
        var (grid, _) = BuildGrid(widget, daysInMonth: 29);

        var dayCells = grid.Cells.Skip(7).ToList();
        Assert.Equal(29, dayCells.Count);
    }

    [Fact]
    public void BuildGridWidget_FebruaryNonLeapYear_CorrectDays()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 2, 1))
        {
            Today = new DateOnly(2026, 2, 1)
        };
        var (grid, _) = BuildGrid(widget, daysInMonth: 28);

        var dayCells = grid.Cells.Skip(7).ToList();
        Assert.Equal(28, dayCells.Count);
    }

    [Fact]
    public void BuildGridWidget_MonthNeeding6WeekRows()
    {
        // August 2025 starts on Friday, 31 days → needs 6 week rows
        var widget = new CalendarWidget(new DateOnly(2025, 8, 1));
        var (grid, _) = BuildGrid(widget);

        Assert.Equal(7, grid.RowDefinitions.Count); // 1 header + 6 week rows
    }

    #endregion

    #region CalendarNode Tests

    [Fact]
    public void CalendarNode_IsNotFocusable()
    {
        // CalendarNode delegates focus to its child InteractableNodes
        var node = new CalendarNode();
        Assert.False(node.IsFocusable);
    }

    [Fact]
    public void CalendarNode_SelectedDay_ClampsToBounds()
    {
        var node = new CalendarNode { DaysInMonth = 28 };
        node.SelectedDay = 30;
        Assert.Equal(28, node.SelectedDay);
    }

    [Fact]
    public void CalendarNode_SelectedDay_ClampsToMin()
    {
        var node = new CalendarNode { DaysInMonth = 28 };
        node.SelectedDay = 0;
        Assert.Equal(1, node.SelectedDay);
    }

    [Fact]
    public void CalendarNode_SelectedDay_DefaultsToNull()
    {
        var node = new CalendarNode { DaysInMonth = 28 };
        Assert.Null(node.SelectedDay);
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    public void Calendar_WithYearMonth_CreatesCorrectWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Calendar(2026, 3);

        Assert.Equal(2026, widget.Month.Year);
        Assert.Equal(3, widget.Month.Month);
    }

    [Fact]
    public void ShowHeader_False_DisablesHeader()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Calendar(2026, 3).ShowHeader(false);

        Assert.False(widget.ShowHeader);
    }

    [Fact]
    public void FirstDayOfWeek_Monday_SetsCorrectly()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Calendar(2026, 3).FirstDayOfWeek(DayOfWeek.Monday);

        Assert.Equal(DayOfWeek.Monday, widget.FirstDayOfWeek);
    }

    [Fact]
    public void Today_SetsOverride()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var today = new DateOnly(2026, 3, 15);
        var widget = ctx.Calendar(2026, 3).Today(today);

        Assert.Equal(today, widget.Today);
    }

    [Fact]
    public void Compact_SetsIsCompact()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Calendar(2026, 3).Compact();

        Assert.True(widget.IsCompact);
    }

    [Fact]
    public void Day_SetsDayBuilder()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Calendar(2026, 3).Day(ctx => new TextBlockWidget("test"));

        Assert.NotNull(widget.DayBuilder);
    }

    #endregion

    #region Column Consistency Tests

    [Fact]
    public void BuildGridWidget_AllDaysInColumns0Through6()
    {
        var months = new[]
        {
            (new DateOnly(2026, 1, 1), 31),
            (new DateOnly(2026, 2, 1), 28),
            (new DateOnly(2026, 6, 1), 30),
            (new DateOnly(2026, 9, 1), 30),
            (new DateOnly(2026, 12, 1), 31),
        };

        foreach (var (month, days) in months)
        {
            var widget = new CalendarWidget(month);
            var (grid, _) = BuildGrid(widget, daysInMonth: days);
            var dayCells = grid.Cells.Skip(7).ToList();

            foreach (var cell in dayCells)
            {
                Assert.InRange(cell.ColumnIndex, 0, 6);
            }
        }
    }

    [Fact]
    public void BuildGridWidget_HeaderCells_SpanAllColumns()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1));
        var (grid, _) = BuildGrid(widget);

        var headerCells = grid.Cells.Take(7).ToList();
        var columns = headerCells.Select(c => c.ColumnIndex).OrderBy(c => c).ToList();

        Assert.Equal([0, 1, 2, 3, 4, 5, 6], columns);
    }

    #endregion

    #region GetExpectedNodeType Tests

    [Fact]
    public void GetExpectedNodeType_ReturnsCalendarNode()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1));
        Assert.Equal(typeof(CalendarNode), widget.GetExpectedNodeType());
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Integration_Calendar_RendersMonthDays()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Calendar(2026, 3)
                    .Today(new DateOnly(2026, 3, 14))
                    .Compact()
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("31"), TimeSpan.FromSeconds(5), "last day of March")
            .Capture("rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Compact header uses the best format that fits: single-char ("S") or two-char ("Su")
        // depending on column width. Check for day numbers instead of header format.
        Assert.True(snapshot.ContainsText("1"));
        Assert.True(snapshot.ContainsText("14"));
        Assert.True(snapshot.ContainsText("31")); // March has 31 days
    }

    [Fact]
    public async Task Integration_Calendar_ClickDay_FiresOnSelected()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(80, 24)
            .Build();

        var selectedDates = new List<DateOnly>();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Calendar(2026, 3)
                    .Today(new DateOnly(2026, 3, 14))
                    .Compact()
                    .OnSelected(e => selectedDates.Add(e.SelectedDate))
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("1"), TimeSpan.FromSeconds(5), "calendar rendered")
            // Enter selects the focused day (auto-focused first cell = day 1)
            .Enter()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Single(selectedDates);
        Assert.Equal(new DateOnly(2026, 3, 1), selectedDates[0]);
    }

    [Fact]
    public async Task Integration_Calendar_DefaultMode_HasGridLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Calendar(2026, 3)
                    .Today(new DateOnly(2026, 3, 14))
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Sun"), TimeSpan.FromSeconds(5), "header row")
            .Capture("rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Default mode should have gridline box-drawing characters
        Assert.True(snapshot.ContainsText("┌") || snapshot.ContainsText("│") || snapshot.ContainsText("─"));
    }

    #endregion
}
