using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class DatePickerWidgetTests
{
    #region DatePickerNode State Tests

    [Fact]
    public void Node_DefaultStep_IsYear()
    {
        var node = new DatePickerNode();
        Assert.Equal(PickerStep.Year, node.Step);
    }

    [Fact]
    public void SelectYear_AdvancesToMonthStep()
    {
        var node = new DatePickerNode();
        node.SelectYear(2026);

        Assert.Equal(PickerStep.Month, node.Step);
        Assert.Equal(2026, node.DisplayYear);
    }

    [Fact]
    public void SelectMonth_AdvancesToCalendarStep()
    {
        var node = new DatePickerNode();
        node.SelectYear(2026);
        node.SelectMonth(3);

        Assert.Equal(PickerStep.Calendar, node.Step);
        Assert.Equal(3, node.DisplayMonth);
    }

    [Fact]
    public void GoBack_FromCalendar_ReturnsToMonth()
    {
        var node = new DatePickerNode();
        node.SelectYear(2026);
        node.SelectMonth(3);

        var result = node.GoBack();

        Assert.True(result);
        Assert.Equal(PickerStep.Month, node.Step);
    }

    [Fact]
    public void GoBack_FromMonth_ReturnsToYear()
    {
        var node = new DatePickerNode();
        node.SelectYear(2026);

        var result = node.GoBack();

        Assert.True(result);
        Assert.Equal(PickerStep.Year, node.Step);
    }

    [Fact]
    public void GoBack_FromYear_ReturnsFalse()
    {
        var node = new DatePickerNode();

        var result = node.GoBack();

        Assert.False(result);
        Assert.Equal(PickerStep.Year, node.Step);
    }

    [Fact]
    public void PageYearsForward_ShiftsBy12()
    {
        var node = new DatePickerNode { YearPageStart = 2020 };
        node.PageYearsForward();

        Assert.Equal(2032, node.YearPageStart);
    }

    [Fact]
    public void PageYearsBackward_ShiftsBy12()
    {
        var node = new DatePickerNode { YearPageStart = 2020 };
        node.PageYearsBackward();

        Assert.Equal(2008, node.YearPageStart);
    }

    [Fact]
    public void ResetStep_ResetsToYear_CenteredOnSelectedDate()
    {
        var node = new DatePickerNode
        {
            SelectedDate = new DateOnly(2026, 6, 15),
        };
        node.SelectYear(2026);
        node.SelectMonth(6);

        node.ResetStep();

        Assert.Equal(PickerStep.Year, node.Step);
        Assert.Equal(2021, node.YearPageStart); // 2026 - 5
    }

    [Fact]
    public void ResetStep_NoSelectedDate_CenteredOnCurrentYear()
    {
        var node = new DatePickerNode();
        node.SelectYear(2026);

        node.ResetStep();

        Assert.Equal(PickerStep.Year, node.Step);
        Assert.Equal(DateTime.Today.Year - 5, node.YearPageStart);
    }

    #endregion

    #region Display Text Tests

    [Fact]
    public void GetDisplayText_NoDate_ReturnsPlaceholder()
    {
        var node = new DatePickerNode();

        var text = node.GetDisplayText("Choose date");

        Assert.Equal("Choose date", text);
    }

    [Fact]
    public void GetDisplayText_NoDate_DefaultPlaceholder()
    {
        var node = new DatePickerNode();

        var text = node.GetDisplayText(null);

        Assert.Equal("Select date...", text);
    }

    [Fact]
    public void GetDisplayText_WithDate_UsesShortDateByDefault()
    {
        var node = new DatePickerNode
        {
            SelectedDate = new DateOnly(2026, 3, 15),
        };

        var text = node.GetDisplayText("placeholder");

        Assert.Equal(new DateOnly(2026, 3, 15).ToShortDateString(), text);
    }

    [Fact]
    public void GetDisplayText_WithDate_UsesCustomFormat()
    {
        var node = new DatePickerNode
        {
            SelectedDate = new DateOnly(2026, 3, 15),
            DateFormat = "yyyy-MM-dd",
        };

        var text = node.GetDisplayText("placeholder");

        Assert.Equal("2026-03-15", text);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Integration_DatePicker_ShowsPlaceholder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.DatePicker().Placeholder("Pick a date")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Pick a date"), TimeSpan.FromSeconds(5), "placeholder")
            .Capture("rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("Pick a date"));
    }

    [Fact]
    public async Task Integration_DatePicker_OpensYearGrid()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(40, 20)
            .Build();

        var currentYear = DateTime.Today.Year;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.DatePicker()
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Click the button to open the popup, then wait for year grid
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Select date..."), TimeSpan.FromSeconds(5), "trigger button")
            .Enter()
            .WaitUntil(s => s.ContainsText(currentYear.ToString()), TimeSpan.FromSeconds(5), "year grid")
            .Capture("yearGrid")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText(currentYear.ToString()));
        // Border title now shows the year range
        Assert.True(snapshot.ContainsText("–"));
    }

    [Fact]
    public async Task Integration_DatePicker_FullFlow_SelectsDate()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(40, 20)
            .Build();

        var selectedDates = new List<DateOnly>();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.DatePicker()
                    .Format("yyyy-MM-dd")
                    .OnSelected(e => selectedDates.Add(e.SelectedDate))
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Select date..."), TimeSpan.FromSeconds(5), "trigger button")
            // Open popup → year grid
            .Enter()
            .WaitUntil(s => s.ContainsText("–"), TimeSpan.FromSeconds(5), "year grid")
            // Select current year (auto-focused)
            .Enter()
            .WaitUntil(s => s.ContainsText("Month"), TimeSpan.FromSeconds(5), "month grid")
            // Select first month (auto-focused)
            .Enter()
            .WaitUntil(s => s.ContainsText("Day"), TimeSpan.FromSeconds(5), "calendar")
            // Select first day (auto-focused)
            .Enter()
            .WaitUntil(s => !s.ContainsText("Day"), TimeSpan.FromSeconds(5), "popup closed")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Single(selectedDates);
    }

    [Fact]
    public async Task Integration_DatePicker_ArrowKeys_NavigateYearGrid()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(40, 20)
            .Build();

        var selectedDates = new List<DateOnly>();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.DatePicker()
                    .Format("yyyy-MM-dd")
                    .OnSelected(e => selectedDates.Add(e.SelectedDate))
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Open popup, then press Right to move to next year cell, then Enter to select
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Select date..."), TimeSpan.FromSeconds(5), "trigger button")
            .Enter()
            .WaitUntil(s => s.ContainsText("–"), TimeSpan.FromSeconds(5), "year grid")
            .Right()  // Move to second year cell
            .WaitUntil(_ => true, TimeSpan.FromMilliseconds(200), "settle")
            .Enter()  // Select the second year
            .WaitUntil(s => s.ContainsText("Month"), TimeSpan.FromSeconds(3), "month grid after arrow nav")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task Integration_DatePicker_ArrowKeys_WithSurroundingWidgets()
    {
        // Mirrors CalendarDemo layout: ToggleSwitch + DatePicker
        using var workload = new Hex1bAppWorkloadAdapter();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(60, 25)
            .Build();

        using var app = new Hex1bApp(
            ctx =>
            {
                var toggle = ctx.ToggleSwitch(["Option A", "Option B", "Option C"]);
                var picker = ctx.DatePicker()
                    .Format("yyyy-MM-dd");
                return Task.FromResult<Hex1bWidget>(
                    new VStackWidget([toggle, picker]));
            },
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Tab once to reach DatePicker trigger button, open popup, try arrow keys
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Select date..."), TimeSpan.FromSeconds(5), "trigger button")
            .Tab()  // ToggleSwitch → DatePicker trigger
            .WaitUntil(_ => true, TimeSpan.FromMilliseconds(100), "settle")
            .Enter()
            .WaitUntil(s => s.ContainsText("–"), TimeSpan.FromSeconds(5), "year grid")
            .Right()  // Arrow key navigates to next year cell
            .WaitUntil(_ => true, TimeSpan.FromMilliseconds(200), "settle after right")
            .Enter()  // Select that year → advance to month grid
            .WaitUntil(s => s.ContainsText("Month"), TimeSpan.FromSeconds(3), "month grid after arrow nav")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    #endregion
}
