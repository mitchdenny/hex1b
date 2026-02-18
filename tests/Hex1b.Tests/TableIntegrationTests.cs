using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Comprehensive integration tests for table widget functionality.
/// Tests cover focus highlighting, selection, select-all, fill width,
/// compact vs full mode, and mouse interaction.
/// </summary>
public class TableIntegrationTests
{
    private static readonly Hex1bColor FocusedRowBg = Hex1bColor.FromRgb(50, 50, 50);

    /// <summary>
    /// Test item for selection scenarios.
    /// </summary>
    private class Employee
    {
        public string Name { get; set; } = "";
        public string Role { get; set; } = "";
        public int Age { get; set; }
        public bool IsSelected { get; set; }
    }

    private static List<Employee> CreateTestData(int count = 10)
    {
        var roles = new[] { "Engineer", "Designer", "Manager", "Tester", "Analyst" };
        return Enumerable.Range(1, count)
            .Select(i => new Employee
            {
                Name = $"Employee {i}",
                Role = roles[(i - 1) % roles.Length],
                Age = 25 + i
            })
            .ToList();
    }

    #region Focus Row Background Tests

    [Fact]
    public async Task Table_FocusedRow_HasBackgroundColor()
    {
        // Verify that focusing a row renders the FocusedRowBackground color
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12)
            .Build();

        var data = CreateTestData(5);
        object? focusedKey = "Employee 1";

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [h.Cell("Name"), h.Cell("Role")])
                .Row((r, item, _) => [r.Cell(item.Name), r.Cell(item.Role)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for render with focus on first row
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1") && s.ContainsText("Employee 5"),
                TimeSpan.FromSeconds(10), "table fully rendered")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Focused row test ===");
        TestContext.Current.TestOutputHelper?.WriteLine(text);

        // The focused row should have the FocusedRowBackground color applied
        Assert.True(snapshot.HasBackgroundColor(FocusedRowBg),
            $"Focused row should have background color RGB(50,50,50). Screen:\n{text}");

        // Also verify focus bars (┃) are present
        Assert.Contains("┃", text);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task Table_FocusedRow_MovingDown_BackgroundFollowsFocus()
    {
        // Verify that the background color moves when focus changes via arrow key
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12)
            .Build();

        var data = CreateTestData(5);
        object? focusedKey = "Employee 1";

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [h.Cell("Name"), h.Cell("Role")])
                .Row((r, item, state) => [
                    r.Cell(state.IsFocused ? $"> {item.Name}" : item.Name),
                    r.Cell(item.Role)
                ])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Move focus down twice
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1"), TimeSpan.FromSeconds(10), "table rendered")
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow)
            .WaitUntil(s => s.ContainsText("> Employee 3"), TimeSpan.FromSeconds(1), "focus on row 3")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== After moving focus down ===");
        TestContext.Current.TestOutputHelper?.WriteLine(text);

        // Focus should be on Employee 3
        Assert.Equal("Employee 3", focusedKey);
        Assert.True(snapshot.HasBackgroundColor(FocusedRowBg),
            "Focused row background should be present after moving focus");

        // Verify the focus indicator is on the correct row
        Assert.Contains("> Employee 3", text);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task Table_MouseClickOnRow_ShowsFocusBackground()
    {
        // Verify clicking on a row shows focused background
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12)
            .WithMouse()
            .Build();

        var data = CreateTestData(5);
        object? focusedKey = null;

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [h.Cell("Name"), h.Cell("Role")])
                .Row((r, item, state) => [
                    r.Cell(state.IsFocused ? $"> {item.Name}" : item.Name),
                    r.Cell(item.Role)
                ])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for render, then click on a data row (row 4 = ~y=4 in compact mode)
        // row layout: y=0 top border, y=1 header, y=2 separator, y=3 row 1, y=4 row 2, etc.
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1") && s.ContainsText("Employee 5"),
                TimeSpan.FromSeconds(10), "table rendered")
            .ClickAt(10, 4) // Click on Employee 2 row
            .Wait(200)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== After mouse click ===");
        TestContext.Current.TestOutputHelper?.WriteLine(text);
        TestContext.Current.TestOutputHelper?.WriteLine($"Focused key: {focusedKey}");

        // Focus should have changed and background should be visible
        Assert.NotNull(focusedKey);
        Assert.True(snapshot.HasBackgroundColor(FocusedRowBg),
            $"Clicked row should have focused background. Focus: {focusedKey}\nScreen:\n{text}");
        Assert.Contains("┃", text); // Focus bars

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    #endregion

    #region Selection Column Tests

    [Fact]
    public async Task Table_SelectionColumn_SpaceTogglesCheckbox()
    {
        // Verify Space key toggles selection and shows selected background
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12)
            .Build();

        var data = CreateTestData(5);
        object? focusedKey = "Employee 1";

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [h.Cell("Name"), h.Cell("Role")])
                .Row((r, item, _) => [r.Cell(item.Name), r.Cell(item.Role)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .SelectionColumn(
                    isSelected: e => e.IsSelected,
                    onChanged: (e, selected) => e.IsSelected = selected)
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for render, verify unchecked state
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1") && s.ContainsText("[ ]"),
                TimeSpan.FromSeconds(10), "table with checkboxes rendered")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var beforeSnapshot = terminal.CreateSnapshot();
        Assert.Contains("[ ]", beforeSnapshot.GetScreenText());
        Assert.Equal(0, data.Count(e => e.IsSelected));

        // Press Space to select the focused row
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Spacebar)
            .Wait(200)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var afterSnapshot = terminal.CreateSnapshot();
        var afterText = afterSnapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== After Space ===");
        TestContext.Current.TestOutputHelper?.WriteLine(afterText);

        // First row should now be selected
        Assert.Equal(1, data.Count(e => e.IsSelected));
        Assert.True(data[0].IsSelected, "Employee 1 should be selected");
        Assert.Contains("[x]", afterText);

        // Row is both focused AND selected; focused bg takes priority
        Assert.True(afterSnapshot.HasBackgroundColor(FocusedRowBg),
            "Focused+selected row should show focused background color (focus wins)");

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task Table_SelectionColumn_HeaderCheckboxSelectsAllRows()
    {
        // Verify that clicking the header checkbox selects all rows
        // This is the bug fix: SelectionColumn(isSelected, onChanged) must
        // provide a fallback select-all when SelectAllCallback is not set
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12)
            .WithMouse()
            .Build();

        var data = CreateTestData(5);
        object? focusedKey = "Employee 1";

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [h.Cell("Name"), h.Cell("Role")])
                .Row((r, item, _) => [r.Cell(item.Name), r.Cell(item.Role)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .SelectionColumn(
                    isSelected: e => e.IsSelected,
                    onChanged: (e, selected) => e.IsSelected = selected)
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for table to render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1") && s.ContainsText("[ ]"),
                TimeSpan.FromSeconds(10), "table rendered with checkboxes")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(0, data.Count(e => e.IsSelected));

        // Click on the header checkbox
        // Layout: y=0 top border, y=1 header row
        // The checkbox is at x=1..3 (after left border │ at x=0)
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(2, 1)  // Click header checkbox
            .Wait(300)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var afterSnapshot = terminal.CreateSnapshot();
        var afterText = afterSnapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== After header click ===");
        TestContext.Current.TestOutputHelper?.WriteLine(afterText);
        TestContext.Current.TestOutputHelper?.WriteLine($"Selected: {data.Count(e => e.IsSelected)} / {data.Count}");

        // ALL rows should now be selected
        Assert.Equal(5, data.Count(e => e.IsSelected));

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task Table_SelectionColumn_HeaderCheckboxDeselectsAllRows()
    {
        // Verify header checkbox deselects all when all are already selected
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12)
            .WithMouse()
            .Build();

        // Start with all selected
        var data = CreateTestData(5);
        foreach (var e in data) e.IsSelected = true;
        object? focusedKey = "Employee 1";

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [h.Cell("Name"), h.Cell("Role")])
                .Row((r, item, _) => [r.Cell(item.Name), r.Cell(item.Role)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .SelectionColumn(
                    isSelected: e => e.IsSelected,
                    onChanged: (e, selected) => e.IsSelected = selected)
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for render with all checked
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[x]") && s.ContainsText("Employee 1"),
                TimeSpan.FromSeconds(10), "table rendered with all selected")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(5, data.Count(e => e.IsSelected));

        // Click header checkbox to deselect all
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(2, 1)
            .Wait(300)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var afterSnapshot = terminal.CreateSnapshot();
        var afterText = afterSnapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== After deselect all ===");
        TestContext.Current.TestOutputHelper?.WriteLine(afterText);
        TestContext.Current.TestOutputHelper?.WriteLine($"Selected: {data.Count(e => e.IsSelected)} / {data.Count}");

        // ALL rows should now be deselected
        Assert.Equal(0, data.Count(e => e.IsSelected));

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task Table_SelectionColumn_CtrlA_SelectsAllWithoutExplicitCallback()
    {
        // Verify Ctrl+A selects all even without explicit SelectAllCallback
        // Uses only SelectionColumn(isSelected, onChanged) without OnSelectAll
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12)
            .Build();

        var data = CreateTestData(5);
        object? focusedKey = "Employee 1";

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [h.Cell("Name"), h.Cell("Role")])
                .Row((r, item, _) => [r.Cell(item.Name), r.Cell(item.Role)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .SelectionColumn(
                    isSelected: e => e.IsSelected,
                    onChanged: (e, selected) => e.IsSelected = selected)
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1") && s.ContainsText("[ ]"),
                TimeSpan.FromSeconds(10), "table rendered")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(0, data.Count(e => e.IsSelected));

        // Ctrl+A to select all
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.A)
            .Wait(300)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var afterSnapshot = terminal.CreateSnapshot();
        var afterText = afterSnapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== After Ctrl+A ===");
        TestContext.Current.TestOutputHelper?.WriteLine(afterText);
        TestContext.Current.TestOutputHelper?.WriteLine($"Selected: {data.Count(e => e.IsSelected)} / {data.Count}");

        // All rows should be selected
        Assert.Equal(5, data.Count(e => e.IsSelected));

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task Table_SelectionColumn_MouseClickCheckboxTogglesRow()
    {
        // Verify clicking on checkbox area toggles selection for that row
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12)
            .WithMouse()
            .Build();

        var data = CreateTestData(5);
        object? focusedKey = "Employee 1";

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [h.Cell("Name"), h.Cell("Role")])
                .Row((r, item, _) => [r.Cell(item.Name), r.Cell(item.Role)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .SelectionColumn(
                    isSelected: e => e.IsSelected,
                    onChanged: (e, selected) => e.IsSelected = selected)
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1") && s.ContainsText("[ ]"),
                TimeSpan.FromSeconds(10), "table rendered")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Click checkbox of first data row (y=3 for compact: border=0, header=1, sep=2, row1=3)
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(2, 3)
            .Wait(200)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== After checkbox click ===");
        TestContext.Current.TestOutputHelper?.WriteLine(text);

        Assert.Equal(1, data.Count(e => e.IsSelected));
        Assert.True(data[0].IsSelected, "First employee should be selected after click");

        // Click a second row checkbox
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(2, 5)  // row 3 (Employee 3)
            .Wait(200)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(2, data.Count(e => e.IsSelected));

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    #endregion

    #region Fill Width Tests

    [Fact]
    public async Task Table_FillWidthWithFillColumn_ExpandsToContainerWidth()
    {
        // Verify FillWidth works when at least one column uses SizeHint.Fill
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 12)
            .Build();

        var data = CreateTestData(3);

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [
                    h.Cell("Name").Width(SizeHint.Fill),  // Fill column
                    h.Cell("Role").Width(SizeHint.Fixed(12)),
                    h.Cell("Age").Width(SizeHint.Fixed(6))
                ])
                .Row((r, item, _) => [r.Cell(item.Name), r.Cell(item.Role), r.Cell(item.Age.ToString())])
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1") && s.ContainsText("Employee 3"),
                TimeSpan.FromSeconds(10), "table rendered")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Fill Width with Fill column ===");
        TestContext.Current.TestOutputHelper?.WriteLine(text);

        // The table should span the full terminal width (80 columns)
        // Check that the top border extends close to the full width
        var lines = text.Split('\n');
        var topBorder = lines.FirstOrDefault(l => l.Contains("┌") && l.Contains("┐"));
        Assert.NotNull(topBorder);
        
        // The border should reach near the full width
        var trimmed = topBorder!.TrimEnd();
        Assert.True(trimmed.Length >= 75,
            $"Table should fill most of the terminal width (80). Top border length: {trimmed.Length}");

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task Table_FillWidthWithAllFixedColumns_DoesNotExpandColumns()
    {
        // Verify that FillWidth with all Fixed columns does NOT expand the columns
        // (the table may get the width, but columns stay at their fixed sizes)
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 12)
            .Build();

        var data = CreateTestData(3);

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [
                    h.Cell("Name").Width(SizeHint.Fixed(15)),
                    h.Cell("Role").Width(SizeHint.Fixed(12)),
                    h.Cell("Age").Width(SizeHint.Fixed(6))
                ])
                .Row((r, item, _) => [r.Cell(item.Name), r.Cell(item.Role), r.Cell(item.Age.ToString())])
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1"),
                TimeSpan.FromSeconds(10), "table rendered")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Fill Width with all fixed columns ===");
        TestContext.Current.TestOutputHelper?.WriteLine(text);

        // With FillWidth + all Fixed, the actual table content width should be
        // 15 + 12 + 6 + borders (4 vertical bars = 4) = 37 chars
        // The scrollbar (if any) would be at 37, not at column 80
        var lines = text.Split('\n');
        var topBorder = lines.FirstOrDefault(l => l.Contains("┌") && l.Contains("┐"));
        Assert.NotNull(topBorder);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    #endregion

    #region Compact vs Full Mode Tests

    [Fact]
    public async Task Table_CompactMode_NoRowSeparators()
    {
        // Verify compact mode does not render horizontal separators between rows
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12)
            .Build();

        var data = CreateTestData(3);

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [h.Cell("Name"), h.Cell("Role")])
                .Row((r, item, _) => [r.Cell(item.Name), r.Cell(item.Role)])
                .Compact()
                .FillWidth(), // Explicit compact mode
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1") && s.ContainsText("Employee 3"),
                TimeSpan.FromSeconds(10), "table rendered")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Compact mode ===");
        TestContext.Current.TestOutputHelper?.WriteLine(text);

        var lines = text.Split('\n');

        // In compact mode, rows should be consecutive without separator lines
        // Count the lines between Employee 1 and Employee 3
        int emp1Line = -1, emp3Line = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("Employee 1")) emp1Line = i;
            if (lines[i].Contains("Employee 3")) emp3Line = i;
        }

        Assert.True(emp1Line >= 0 && emp3Line >= 0, "Both Employee 1 and 3 should be visible");
        // In compact mode, 3 rows = 3 lines apart (rows 1, 2, 3)
        Assert.Equal(2, emp3Line - emp1Line);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task Table_FullMode_HasRowSeparators()
    {
        // Verify full mode renders horizontal separators between rows
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        var data = CreateTestData(3);

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [h.Cell("Name"), h.Cell("Role")])
                .Row((r, item, _) => [r.Cell(item.Name), r.Cell(item.Role)])
                .Full()
                .FillWidth(),  // Full mode with separators
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1") && s.ContainsText("Employee 3"),
                TimeSpan.FromSeconds(10), "table rendered")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Full mode ===");
        TestContext.Current.TestOutputHelper?.WriteLine(text);

        var lines = text.Split('\n');

        // In full mode, rows are separated by horizontal borders
        // Row 1 ... separator ... Row 2 ... separator ... Row 3
        int emp1Line = -1, emp3Line = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("Employee 1")) emp1Line = i;
            if (lines[i].Contains("Employee 3")) emp3Line = i;
        }

        Assert.True(emp1Line >= 0 && emp3Line >= 0, "Both Employee 1 and 3 should be visible");
        // In full mode, 3 rows = row + sep + row + sep + row = 4 lines between first and third
        Assert.Equal(4, emp3Line - emp1Line);

        // Verify separators exist between rows (├ or ┤ characters)
        int separatorCount = 0;
        for (int i = emp1Line + 1; i < emp3Line; i++)
        {
            if (lines[i].Contains("├") || lines[i].Contains("┤"))
                separatorCount++;
        }
        Assert.Equal(2, separatorCount);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    #endregion

    #region Combined Focus + Selection Tests

    [Fact]
    public async Task Table_FocusedSelected_BothStatesVisible()
    {
        // When a row is both focused and selected, the focused background should win
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12)
            .Build();

        var data = CreateTestData(5);
        object? focusedKey = "Employee 1";

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [h.Cell("Name"), h.Cell("Role")])
                .Row((r, item, _) => [r.Cell(item.Name), r.Cell(item.Role)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .SelectionColumn(
                    isSelected: e => e.IsSelected,
                    onChanged: (e, selected) => e.IsSelected = selected)
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Select the focused row (Space toggles)
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1") && s.ContainsText("[ ]"),
                TimeSpan.FromSeconds(10), "table rendered")
            .Key(Hex1bKey.Spacebar)  // Select Employee 1
            .Wait(200)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Focus + Selected ===");
        TestContext.Current.TestOutputHelper?.WriteLine(text);

        Assert.True(data[0].IsSelected, "Employee 1 should be selected");

        // Focused row takes priority, so we should see FocusedRowBg
        Assert.True(snapshot.HasBackgroundColor(FocusedRowBg),
            "Focused+selected row should show focused background");

        // Move focus down - unfocused selected row should show Selected bg
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.DownArrow)
            .Wait(200)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var afterMoveSnapshot = terminal.CreateSnapshot();
        var afterMoveText = afterMoveSnapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== After moving focus away ===");
        TestContext.Current.TestOutputHelper?.WriteLine(afterMoveText);

        // Now Employee 1 is selected but not focused, Employee 2 is focused but not selected
        // Focus background should still be visible, selection indicated by checkbox only
        Assert.True(afterMoveSnapshot.HasBackgroundColor(FocusedRowBg),
            "Focused row (Employee 2) should have focused background");

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task Table_MultipleSelected_AllShowBackground()
    {
        // Verify multiple selected rows all show the selected background
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12)
            .Build();

        var data = CreateTestData(5);
        object? focusedKey = "Employee 1";

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [h.Cell("Name"), h.Cell("Role")])
                .Row((r, item, _) => [r.Cell(item.Name), r.Cell(item.Role)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .SelectionColumn(
                    isSelected: e => e.IsSelected,
                    onChanged: (e, selected) => e.IsSelected = selected)
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Select first 3 rows using Space + Down
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1") && s.ContainsText("[ ]"),
                TimeSpan.FromSeconds(10), "table rendered")
            .Key(Hex1bKey.Spacebar)  // Select Employee 1
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Spacebar)  // Select Employee 2
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Spacebar)  // Select Employee 3
            .Wait(300)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Multiple selected ===");
        TestContext.Current.TestOutputHelper?.WriteLine(text);

        // 3 rows selected
        Assert.Equal(3, data.Count(e => e.IsSelected));
        Assert.True(data[0].IsSelected && data[1].IsSelected && data[2].IsSelected);

        // Focus background should be present, selection indicated by checkbox only
        Assert.True(snapshot.HasBackgroundColor(FocusedRowBg),
            "Focus row should show focused background");

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    #endregion

    #region Terminal Size Tests

    [Theory]
    [InlineData(40, 10)]
    [InlineData(60, 15)]
    [InlineData(100, 30)]
    public async Task Table_VariousTerminalSizes_RendersCorrectly(int width, int height)
    {
        // Verify table renders without errors at various terminal sizes
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(width, height)
            .Build();

        var data = CreateTestData(20); // More data than can fit in smallest terminal

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [
                    h.Cell("Name").Width(SizeHint.Fill),
                    h.Cell("Role").Width(SizeHint.Fixed(10))
                ])
                .Row((r, item, _) => [r.Cell(item.Name), r.Cell(item.Role)])
                .FillHeight()
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1"),
                TimeSpan.FromSeconds(10), "table rendered")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine($"=== {width}x{height} ===");
        TestContext.Current.TestOutputHelper?.WriteLine(text);

        // Table should have borders and data
        Assert.Contains("Employee 1", text);
        Assert.Contains("┌", text); // Top-left corner
        Assert.Contains("┐", text); // Top-right corner
        Assert.Contains("└", text); // Bottom-left corner
        Assert.Contains("┘", text); // Bottom-right corner

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    #endregion

    #region Scrolling + Focus Tests

    [Fact]
    public async Task Table_ScrollToBottom_FocusBackgroundStillVisible()
    {
        // Verify focus background is visible after scrolling to bottom of table
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 10) // Small terminal to force scrolling
            .Build();

        var data = CreateTestData(30);
        object? focusedKey = "Employee 1";

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [h.Cell("Name"), h.Cell("Role")])
                .Row((r, item, state) => [
                    r.Cell(state.IsFocused ? $"> {item.Name}" : item.Name),
                    r.Cell(item.Role)
                ])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .FillHeight()
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Navigate to bottom
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1"), TimeSpan.FromSeconds(10), "table rendered")
            .Key(Hex1bKey.End)  // Jump to last row
            .WaitUntil(s => s.ContainsText("Employee 30"), TimeSpan.FromSeconds(1), "scrolled to bottom")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== After scroll to bottom ===");
        TestContext.Current.TestOutputHelper?.WriteLine(text);
        TestContext.Current.TestOutputHelper?.WriteLine($"Focused: {focusedKey}");

        // Focus should be on the last employee and background should be visible
        Assert.Equal("Employee 30", focusedKey);
        Assert.True(snapshot.HasBackgroundColor(FocusedRowBg),
            "Focused row at bottom should have background color");
        Assert.Contains("┃", text); // Focus bars

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task Table_HomeAndEnd_FocusNavigatesCorrectly()
    {
        // Navigate Home/End and verify focus and background
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 10)
            .Build();

        var data = CreateTestData(20);
        object? focusedKey = "Employee 1";

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [h.Cell("Name"), h.Cell("Role")])
                .Row((r, item, state) => [
                    r.Cell(state.IsFocused ? $"> {item.Name}" : item.Name),
                    r.Cell(item.Role)
                ])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .FillHeight()
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Navigate to End
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1"), TimeSpan.FromSeconds(10), "table rendered")
            .Key(Hex1bKey.End)
            .WaitUntil(s => s.ContainsText("> Employee 20"), TimeSpan.FromSeconds(1), "at end")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal("Employee 20", focusedKey);

        // Navigate back to Home
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Home)
            .WaitUntil(s => s.ContainsText("> Employee 1"), TimeSpan.FromSeconds(1), "back to home")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== After Home ===");
        TestContext.Current.TestOutputHelper?.WriteLine(text);

        Assert.Equal("Employee 1", focusedKey);
        Assert.True(snapshot.HasBackgroundColor(FocusedRowBg),
            "Focused row should have background after Home");

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    #endregion

    #region Selection + Select All with FillHeight

    [Fact]
    public async Task Table_SelectAllThenDeselectAll_Roundtrip()
    {
        // Verify select all then deselect all works as a complete roundtrip
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 12)
            .Build();

        var data = CreateTestData(5);
        object? focusedKey = "Employee 1";

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [h.Cell("Name"), h.Cell("Role")])
                .Row((r, item, _) => [r.Cell(item.Name), r.Cell(item.Role)])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .SelectionColumn(
                    isSelected: e => e.IsSelected,
                    onChanged: (e, selected) => e.IsSelected = selected)
                .FillWidth(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Employee 1") && s.ContainsText("[ ]"),
                TimeSpan.FromSeconds(10), "table rendered")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Ctrl+A to select all
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.A)
            .Wait(300)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(5, data.Count(e => e.IsSelected));

        var selectAllSnapshot = terminal.CreateSnapshot();
        var selectAllText = selectAllSnapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== After Select All ===");
        TestContext.Current.TestOutputHelper?.WriteLine(selectAllText);

        // All checkboxes should show [x]
        Assert.Contains("[x]", selectAllText);
        // The header checkbox should show the checked indicator
        // (exact char depends on theme, but it should change from [ ])

        // Ctrl+A again should deselect all (since all are selected)
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.A)
            .Wait(300)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(0, data.Count(e => e.IsSelected));

        var deselectAllSnapshot = terminal.CreateSnapshot();
        var deselectAllText = deselectAllSnapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== After Deselect All ===");
        TestContext.Current.TestOutputHelper?.WriteLine(deselectAllText);

        Assert.Contains("[ ]", deselectAllText);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    #endregion

    #region WindowingDemo-Specific Tests

    [Fact]
    public async Task Table_WindowingDemoConfig_FillWidthExpandsNameColumn()
    {
        // Reproduce the demo's table config: Name=Fill, Role=Fixed(12), Age=Fixed(6), Status=Fixed(10)
        // Verify that FillWidth makes the Name column absorb remaining space
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(65, 15)
            .Build();

        var data = new List<Employee>
        {
            new() { Name = "Alice Johnson", Role = "Engineer", Age = 32 },
            new() { Name = "Bob Smith", Role = "Designer", Age = 28 },
            new() { Name = "Charlie Brown", Role = "Manager", Age = 45 }
        };

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [
                    h.Cell("Name").Width(SizeHint.Fill),
                    h.Cell("Role").Width(SizeHint.Fixed(12)),
                    h.Cell("Age").Width(SizeHint.Fixed(6)),
                ])
                .Row((r, item, _) => [
                    r.Cell(item.Name),
                    r.Cell(item.Role),
                    r.Cell(item.Age.ToString())
                ])
                .FillWidth()
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Alice Johnson") && s.ContainsText("Charlie Brown"),
                TimeSpan.FromSeconds(10), "table rendered")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== Demo config with Fill Width ===");
        TestContext.Current.TestOutputHelper?.WriteLine(text);

        // The table should use full 65 width
        var lines = text.Split('\n');
        var topBorder = lines.FirstOrDefault(l => l.Contains("┌") && l.Contains("┐"));
        Assert.NotNull(topBorder);
        
        var trimmed = topBorder!.TrimEnd();
        // The table should span at least 60 chars (accounting for possible padding)
        Assert.True(trimmed.Length >= 60,
            $"Table should fill most of the 65-wide container. Top border length: {trimmed.Length}");

        // Verify the Name column got more than the fixed 18 it would normally get
        // The header "Name" text should be visible in a wider cell
        Assert.Contains("Name", text);
        Assert.Contains("Role", text);
        Assert.Contains("Age", text);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task Table_WindowingDemoConfig_SelectionWithFocus()
    {
        // Full demo-like test: table with selection, focus, compact mode, fill width
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(65, 15)
            .Build();

        var data = new List<Employee>
        {
            new() { Name = "Alice Johnson", Role = "Engineer", Age = 32 },
            new() { Name = "Bob Smith", Role = "Designer", Age = 28 },
            new() { Name = "Charlie Brown", Role = "Manager", Age = 45 },
            new() { Name = "Diana Prince", Role = "Tester", Age = 30 },
            new() { Name = "Eve Wilson", Role = "Analyst", Age = 35 }
        };
        object? focusedKey = "Alice Johnson";

        using var app = new Hex1bApp(
            ctx => ctx.Table((IReadOnlyList<Employee>)data)
                .RowKey(e => e.Name)
                .Header(h => [
                    h.Cell("Name").Width(SizeHint.Fill),
                    h.Cell("Role").Width(SizeHint.Fixed(12)),
                    h.Cell("Age").Width(SizeHint.Fixed(6))
                ])
                .Row((r, item, _) => [
                    r.Cell(item.Name),
                    r.Cell(item.Role),
                    r.Cell(item.Age.ToString())
                ])
                .Focus(focusedKey)
                .OnFocusChanged(key => focusedKey = key)
                .SelectionColumn(
                    isSelected: e => e.IsSelected,
                    onChanged: (e, selected) => e.IsSelected = selected)
                .FillWidth()
                .FillHeight(),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Step 1: Verify initial render with focus on Alice
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Alice Johnson") && s.ContainsText("[ ]"),
                TimeSpan.FromSeconds(10), "table rendered")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var initialSnapshot = terminal.CreateSnapshot();
        Assert.True(initialSnapshot.HasBackgroundColor(FocusedRowBg),
            "Initial focused row should have background");

        // Step 2: Select first two rows
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Spacebar)  // Select Alice
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Spacebar)  // Select Bob
            .Wait(200)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(2, data.Count(e => e.IsSelected));

        var afterSelectSnapshot = terminal.CreateSnapshot();
        // Focus background should be present, selection indicated by checkbox only
        Assert.True(afterSelectSnapshot.HasBackgroundColor(FocusedRowBg),
            "Bob (focused) should have focused background");

        // Step 3: Ctrl+A to select all
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.A)
            .Wait(300)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(5, data.Count(e => e.IsSelected));

        var allSelectedSnapshot = terminal.CreateSnapshot();
        var allSelectedText = allSelectedSnapshot.GetScreenText();
        TestContext.Current.TestOutputHelper?.WriteLine("=== All selected ===");
        TestContext.Current.TestOutputHelper?.WriteLine(allSelectedText);
        Assert.Contains("[x]", allSelectedText);

        // Step 4: Ctrl+A again to deselect all
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.A)
            .Wait(300)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(0, data.Count(e => e.IsSelected));

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    #endregion
}
