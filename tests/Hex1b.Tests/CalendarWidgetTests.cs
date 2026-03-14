using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class CalendarWidgetTests
{
    #region Grid Construction Tests

    [Fact]
    public void BuildGridWidget_ProducesSevenColumns()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1));
        var grid = widget.BuildGridWidget();

        Assert.Equal(7, grid.ColumnDefinitions.Count);
    }

    [Fact]
    public void BuildGridWidget_WithHeader_HasHeaderRow()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1));
        var grid = widget.BuildGridWidget();

        // Should have 7 header cells + day cells
        // March 2026: starts on Sunday, 31 days → 5 week rows
        // Total cells = 7 (header) + 31 (days) = 38
        Assert.Equal(38, grid.Cells.Count);
    }

    [Fact]
    public void BuildGridWidget_WithoutHeader_HasNoDayLabels()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1)) { ShowHeader = false };
        var grid = widget.BuildGridWidget();

        // Without header, only day cells
        Assert.Equal(31, grid.Cells.Count);
    }

    [Fact]
    public void BuildGridWidget_March2026_CorrectRowCount()
    {
        // March 2026 starts on Sunday, 31 days → 5 week rows + 1 header = 6 rows
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1));
        var grid = widget.BuildGridWidget();

        Assert.Equal(6, grid.RowDefinitions.Count);
    }

    [Fact]
    public void BuildGridWidget_February2026_CorrectRowCount()
    {
        // Feb 2026 starts on Sunday, 28 days → 4 week rows + 1 header = 5 rows
        var widget = new CalendarWidget(new DateOnly(2026, 2, 1));
        var grid = widget.BuildGridWidget();

        Assert.Equal(5, grid.RowDefinitions.Count);
    }

    [Fact]
    public void BuildGridWidget_MonthStartingMidWeek_CorrectPlacement()
    {
        // April 2026 starts on Wednesday (day of week = 3)
        var widget = new CalendarWidget(new DateOnly(2026, 4, 1));
        var grid = widget.BuildGridWidget();

        // Find the cell for day 1 - should be after the 7 header cells
        // With Sunday as first day, Wednesday = column 3
        var dayCells = grid.Cells.Skip(7).ToList(); // skip header
        var firstDayCell = dayCells[0];
        Assert.Equal(3, firstDayCell.ColumnIndex); // Wednesday
        Assert.Equal(1, firstDayCell.RowIndex); // row after header
    }

    [Fact]
    public void BuildGridWidget_MondayFirstDay_ShiftsColumns()
    {
        // March 2026 starts on Sunday. With Monday as first day, Sunday = column 6
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1))
        {
            FirstDayOfWeek = DayOfWeek.Monday
        };
        var grid = widget.BuildGridWidget();

        var dayCells = grid.Cells.Skip(7).ToList();
        var firstDayCell = dayCells[0];
        Assert.Equal(6, firstDayCell.ColumnIndex); // Sunday is last column when Monday is first
    }

    [Fact]
    public void BuildGridWidget_LastDayInCorrectPosition()
    {
        // March 2026: 31 days, starts Sunday
        // Day 31 = offset 30, col = 30 % 7 = 2 (Tuesday)
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1));
        var grid = widget.BuildGridWidget();

        var dayCells = grid.Cells.Skip(7).ToList();
        var lastDayCell = dayCells[^1];
        Assert.Equal(2, lastDayCell.ColumnIndex); // Tuesday
    }

    #endregion

    #region Today Highlighting Tests

    [Fact]
    public void BuildGridWidget_TodayHighlighted_HasBrackets()
    {
        var today = new DateOnly(2026, 3, 15);
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1)) { Today = today };
        var grid = widget.BuildGridWidget();

        // Find day 15 cell (skip 7 header cells, then index 14 = day 15)
        var dayCells = grid.Cells.Skip(7).ToList();
        var day15Cell = dayCells[14]; // 0-indexed, day 15 is index 14

        var textWidget = Assert.IsType<TextBlockWidget>(day15Cell.Child);
        Assert.Contains("[15]", textWidget.Text);
    }

    [Fact]
    public void BuildGridWidget_RegularDay_NoBrackets()
    {
        var today = new DateOnly(2026, 3, 15);
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1)) { Today = today };
        var grid = widget.BuildGridWidget();

        var dayCells = grid.Cells.Skip(7).ToList();
        var day10Cell = dayCells[9]; // day 10

        var textWidget = Assert.IsType<TextBlockWidget>(day10Cell.Child);
        Assert.DoesNotContain("[", textWidget.Text);
        Assert.Contains("10", textWidget.Text);
    }

    #endregion

    #region Week Calculation Tests

    [Fact]
    public void BuildGridWidget_FebruaryLeapYear_CorrectDays()
    {
        // Feb 2028 is a leap year with 29 days
        var widget = new CalendarWidget(new DateOnly(2028, 2, 1)) { Today = new DateOnly(2028, 2, 1) };
        var grid = widget.BuildGridWidget();

        var dayCells = grid.Cells.Skip(7).ToList();
        Assert.Equal(29, dayCells.Count);
    }

    [Fact]
    public void BuildGridWidget_FebruaryNonLeapYear_CorrectDays()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 2, 1)) { Today = new DateOnly(2026, 2, 1) };
        var grid = widget.BuildGridWidget();

        var dayCells = grid.Cells.Skip(7).ToList();
        Assert.Equal(28, dayCells.Count);
    }

    [Fact]
    public void BuildGridWidget_MonthNeeding6WeekRows()
    {
        // August 2025 starts on Friday, 31 days → needs 6 week rows
        // Offset = 5 (Friday), total slots = 5 + 31 = 36, rows = ceil(36/7) = 6
        var widget = new CalendarWidget(new DateOnly(2025, 8, 1));
        var grid = widget.BuildGridWidget();

        Assert.Equal(7, grid.RowDefinitions.Count); // 1 header + 6 week rows
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
        var override_ = new DateOnly(2026, 3, 15);
        var widget = ctx.Calendar(2026, 3).Today(override_);

        Assert.Equal(override_, widget.Today);
    }

    #endregion

    #region Column Consistency Tests

    [Fact]
    public void BuildGridWidget_AllDaysInColumns0Through6()
    {
        // Test multiple months to ensure all day cells are in valid columns
        var months = new[]
        {
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 12, 1),
        };

        foreach (var month in months)
        {
            var widget = new CalendarWidget(month);
            var grid = widget.BuildGridWidget();
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
        var grid = widget.BuildGridWidget();

        var headerCells = grid.Cells.Take(7).ToList();
        var columns = headerCells.Select(c => c.ColumnIndex).OrderBy(c => c).ToList();

        Assert.Equal([0, 1, 2, 3, 4, 5, 6], columns);
    }

    #endregion

    #region GetExpectedNodeType Tests

    [Fact]
    public void GetExpectedNodeType_ReturnsGridNode()
    {
        var widget = new CalendarWidget(new DateOnly(2026, 3, 1));
        Assert.Equal(typeof(GridNode), widget.GetExpectedNodeType());
    }

    #endregion
}
