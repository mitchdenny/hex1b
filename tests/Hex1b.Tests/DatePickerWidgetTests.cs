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

    [Fact]
    public async Task Integration_DatePicker_ArrowKeys_DiagnosticRepro()
    {
        // Comprehensive repro: proves arrow key navigation works through the full
        // year→month→calendar flow with visual focus feedback
        using var workload = new Hex1bAppWorkloadAdapter();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(60, 25)
            .Build();

        var selectedDates = new List<DateOnly>();

        using var app = new Hex1bApp(
            ctx =>
            {
                var toggle = ctx.ToggleSwitch(["Mode A", "Mode B"]);
                var picker = ctx.DatePicker()
                    .Format("yyyy-MM-dd")
                    .OnSelected(e => selectedDates.Add(e.SelectedDate));
                return Task.FromResult<Hex1bWidget>(
                    new VStackWidget([toggle, picker]));
            },
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Full single-sequence flow: open popup → arrow nav years → select →
        // arrow nav months → select → arrow nav days → select → verify date
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            // Open popup: Tab to picker trigger, then Enter to open
            .WaitUntil(s => s.ContainsText("Select date..."), TimeSpan.FromSeconds(5), "trigger button")
            .Tab()
            .Enter()
            .WaitUntil(s => s.ContainsText("–"), TimeSpan.FromSeconds(5), "year grid")
            // Arrow navigate in year grid: Right moves to next year cell
            .Right()
            .Right()
            .Down()
            // Enter to select the navigated year → month grid
            .Enter()
            .WaitUntil(s => s.ContainsText("Month"), TimeSpan.FromSeconds(3), "month grid after year arrow nav")
            // Arrow navigate in month grid: Right moves to next month
            .Right()
            .Right()
            .Down()
            // Enter to select month → calendar day grid
            .Enter()
            .WaitUntil(s => s.ContainsText("Day"), TimeSpan.FromSeconds(3), "calendar after month arrow nav")
            // Arrow navigate calendar days
            .Right()
            .Down()
            // Enter to select day → popup closes, date selected
            .Enter()
            .WaitUntil(s => !s.ContainsText("Day"), TimeSpan.FromSeconds(3), "popup closed after day selection")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // A date was selected via pure keyboard arrow navigation
        Assert.Single(selectedDates);

        var date = selectedDates[0];
        // Focus starts at currentYear (index 5 in the 12-year grid).
        // Right(2) → index 7, Down(4) → index 11
        var today = DateTime.Today;
        var expectedYear = today.Year - 5 + 11; // YearPageStart + 11 = currentYear + 6
        Assert.Equal(expectedYear, date.Year);
    }

    [Fact]
    public async Task Integration_DatePicker_YearGrid_EdgeNavigation_PageTransitions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(60, 25)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.DatePicker().Format("yyyy-MM-dd")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var currentYear = DateTime.Today.Year;
        var firstPageStart = currentYear - 5;
        var prevPageStart = firstPageStart - 12;

        // 2026 at index 5 (row 1, col 1): Left twice → col 0, then page back
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Select date..."), TimeSpan.FromSeconds(5), "trigger")
            .Enter()
            .WaitUntil(s => s.ContainsText("–"), TimeSpan.FromSeconds(5), "year grid")
            .Capture("initial")
            // Left to col 0
            .Left()
            .WaitUntil(_ => true, TimeSpan.FromMilliseconds(200), "settle")
            // Left from col 0 → page backward
            .Left()
            .WaitUntil(s => s.ContainsText($"{prevPageStart}"), TimeSpan.FromSeconds(3), "page back")
            .Capture("after_page_back")
            // Right from col 3 → page forward
            .Right()
            .WaitUntil(s => s.ContainsText($"{firstPageStart}–"), TimeSpan.FromSeconds(3), "page forward")
            .Capture("after_page_forward")
            .Ctrl().Key(Hex1bKey.C)
            .Build();

        await sequence.ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var captures = sequence.Steps.OfType<CaptureStep>().ToList();
        var output = TestContext.Current.TestOutputHelper;
        foreach (var cap in captures)
        {
            output?.WriteLine($"=== {cap.Name} ===");
            output?.WriteLine(cap.CapturedSnapshot!.GetScreenText());
        }

        Assert.True(captures[0].CapturedSnapshot!.ContainsText($"{firstPageStart}–{firstPageStart + 11}"),
            "Initial should be on first page");
        Assert.True(captures[1].CapturedSnapshot!.ContainsText($"{prevPageStart}–{prevPageStart + 11}"),
            $"After page back should show {prevPageStart}–{prevPageStart + 11}");
        Assert.True(captures[2].CapturedSnapshot!.ContainsText($"{firstPageStart}–{firstPageStart + 11}"),
            "After page forward should be back on first page");
    }

    [Fact]
    public async Task Integration_DatePicker_YearGrid_LeftEdge_PageBack_FocusesCorrectYear()
    {
        // Regression test: pressing LEFT twice from the current year should page back
        // and focus the rightmost cell in the same row (index 7), not retain the old
        // focus position (index 4).
        //
        // Root cause: the LEFT-at-col-0 handler sets YearFocusCellIndex and triggers a
        // page rebuild, but the previously focused InteractableNode retains IsFocused=true
        // because the RequestFocus reconciliation path never clears stale focus on other
        // nodes. When two nodes both have IsFocused=true the FocusRing safety-net keeps
        // the last one, but if that safety-net didn't exist or ran in a different order
        // the first (stale) node would win, yielding the wrong year.
        using var workload = new Hex1bAppWorkloadAdapter();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(60, 25)
            .Build();

        var currentYear = DateTime.Today.Year;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.DatePicker().Format("yyyy-MM-dd")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Year grid layout (4 cols × 3 rows), page starts at currentYear - 5:
        //   Row 0: [cy-5] [cy-4] [cy-3] [cy-2]
        //   Row 1: [cy-1] [cy  ] [cy+1] [cy+2]   ← focus starts on cy (index 5, col 1)
        //   Row 2: [cy+3] [cy+4] [cy+5] [cy+6]
        //
        // LEFT → cy-1 (index 4, col 0). LEFT again → page back, should land on
        // rightmost col of same row = index 7 = year (cy-17)+7 = cy-10.
        // BUG: was landing on index 4 = year cy-13 due to stale IsFocused.
        var expectedYear = currentYear - 10;
        var buggyYear = currentYear - 13;
        var prevPageStart = currentYear - 5 - 12;

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Select date..."), TimeSpan.FromSeconds(5), "trigger button")
            .Enter()
            .WaitUntil(s => s.ContainsText("–"), TimeSpan.FromSeconds(5), "year grid")
            // LEFT once: move from current year (col 1) to col 0
            .Left()
            .WaitUntil(_ => true, TimeSpan.FromMilliseconds(200), "settle after first left")
            // LEFT again: at col 0, triggers page backward
            .Left()
            .WaitUntil(s => s.ContainsText($"{prevPageStart}"), TimeSpan.FromSeconds(3), "page back rendered")
            // ENTER: select the focused year → advances to month grid
            .Enter()
            .WaitUntil(s => s.ContainsText("Month"), TimeSpan.FromSeconds(3), "month grid")
            .Capture("month-grid-after-page-back")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // The month grid shows DisplayYear as a label. After selecting the focused year,
        // it should be cy-10 (row 1, col 3 on prev page), NOT cy-13 (row 1, col 0).
        Assert.True(snapshot.ContainsText(expectedYear.ToString()),
            $"Month grid should show year {expectedYear} (index 7, col 3), " +
            $"not {buggyYear} (index 4, col 0 — stale focus position).");
    }

    [Fact]
    public async Task Integration_DatePicker_YearGrid_RightEdge_PageForward_FocusesCorrectYear()
    {
        // Same issue as left-edge but for RIGHT at col 3 → page forward.
        // Focus should land on leftmost col of same row, not retain old position.
        using var workload = new Hex1bAppWorkloadAdapter();

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(60, 25)
            .Build();

        var currentYear = DateTime.Today.Year;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.DatePicker().Format("yyyy-MM-dd")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Focus starts on currentYear (index 5, row 1, col 1).
        // RIGHT twice: col 1 → col 2 → col 3.
        // RIGHT again from col 3: page forward, should land on col 0, same row 1.
        // Target year = (currentYear - 5 + 12) + 4 = currentYear + 11.
        var nextPageStart = currentYear - 5 + 12;
        var expectedYear = nextPageStart + 4; // row 1, col 0

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Select date..."), TimeSpan.FromSeconds(5), "trigger button")
            .Enter()
            .WaitUntil(s => s.ContainsText("–"), TimeSpan.FromSeconds(5), "year grid")
            // RIGHT twice: col 1 → col 2 → col 3
            .Right()
            .WaitUntil(_ => true, TimeSpan.FromMilliseconds(200), "settle")
            .Right()
            .WaitUntil(_ => true, TimeSpan.FromMilliseconds(200), "settle")
            // RIGHT again: at col 3, triggers page forward
            .Right()
            .WaitUntil(s => s.ContainsText($"{nextPageStart}"), TimeSpan.FromSeconds(3), "page forward rendered")
            // ENTER: select the focused year
            .Enter()
            .WaitUntil(s => s.ContainsText("Month"), TimeSpan.FromSeconds(3), "month grid")
            .Capture("month-grid-after-page-forward")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText(expectedYear.ToString()),
            $"Month grid should show year {expectedYear} (row 1, col 0 on next page).");
    }

    #endregion
}
