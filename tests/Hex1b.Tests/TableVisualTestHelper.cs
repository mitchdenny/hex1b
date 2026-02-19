using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Helper methods for building tables from test cases and rendering via the full Hex1bTerminal stack.
/// </summary>
public static class TableVisualTestHelper
{
    /// <summary>
    /// Test data item for visual tests.
    /// </summary>
    public record TestProduct(int Id, string Name, string Category, decimal Price, int Stock);
    
    /// <summary>
    /// Generates test data with the specified row count.
    /// </summary>
    public static List<TestProduct> GenerateTestData(int rowCount)
    {
        var data = new List<TestProduct>(rowCount);
        
        string[] categories = ["Electronics", "Clothing", "Food", "Books", "Toys"];
        string[] names = ["Laptop", "Mouse", "Keyboard", "Monitor", "Headphones", "Shirt", "Pants", "Bread", "Milk", "Novel"];
        
        for (var i = 0; i < rowCount; i++)
        {
            data.Add(new TestProduct(
                Id: i + 1,
                Name: names[i % names.Length] + (i >= names.Length ? $" {i / names.Length + 1}" : ""),
                Category: categories[i % categories.Length],
                Price: 9.99m + (i * 10),
                Stock: 10 + (i % 100)
            ));
        }
        
        return data;
    }
    
    /// <summary>
    /// Builds a TableWidget configured according to the test case.
    /// </summary>
    public static TableWidget<TestProduct> BuildTableWidget(
        RootContext ctx,
        TableVisualTestCase testCase, 
        List<TestProduct> data)
    {
        var widget = ctx.Table(data)
            .RowKey(p => p.Id)
            .Header(h =>
            [
                h.Cell("Name").Width(SizeHint.Fixed(15)),
                h.Cell("Category").Width(SizeHint.Fixed(12)),
                h.Cell("Price").Width(SizeHint.Fixed(10)).Align(Alignment.Right),
                h.Cell("Stock").Width(SizeHint.Fixed(6)).Align(Alignment.Right)
            ])
            .Row((r, item, state) =>
            [
                r.Cell(item.Name),
                r.Cell(item.Category),
                r.Cell($"${item.Price:F2}"),
                r.Cell(item.Stock.ToString())
            ]);
        
        // Apply render mode
        widget = widget with { RenderMode = testCase.Mode };
        
        // Configure selection if needed
        if (testCase.HasSelection)
        {
            widget = widget.SelectionColumn();
            if (testCase.SelectedRows != null)
            {
                var selectedSet = testCase.SelectedRows.ToHashSet();
                widget = widget.SelectionColumn(
                    isSelected: item => selectedSet.Contains(item.Id - 1), // Id is 1-based
                    onChanged: (_, _) => { });
            }
        }
        
        // Set focused row if specified
        if (testCase.FocusedRow >= 0 && testCase.FocusedRow < data.Count)
        {
            widget = widget.Focus(data[testCase.FocusedRow].Id);
        }
        
        return widget;
    }
    
    /// <summary>
    /// Renders a table using the full Hex1bTerminal stack and returns both ANSI and text representations.
    /// </summary>
    public static async Task<(string Ansi, string Text)> RenderTableAsync(
        TableVisualTestCase testCase, 
        CancellationToken cancellationToken = default)
    {
        var data = GenerateTestData(testCase.RowCount);
        
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHeadless()
            .WithDimensions(testCase.Width, testCase.Height)
            .WithHex1bApp((app, options) => ctx => BuildTableWidget(ctx, testCase, data))
            .Build();
        
        // Start terminal/app
        var runTask = terminal.RunAsync(cancellationToken);
        
        // Build input sequence to wait for render
        var waitBuilder = new Hex1bTerminalInputSequenceBuilder();
        
        // Wait for initial render
        if (testCase.RowCount > 0)
        {
            waitBuilder.WaitUntil(s => s.ContainsText("Name"), TimeSpan.FromSeconds(5), "Wait for table header");
        }
        else
        {
            waitBuilder.WaitUntil(s => s.ContainsText("No data"), TimeSpan.FromSeconds(5), "Wait for empty table");
        }
        
        // Apply scroll position if needed
        if (testCase.ScrollPosition > 0)
        {
            for (var i = 0; i < testCase.ScrollPosition; i++)
            {
                waitBuilder.Key(Hex1bKey.DownArrow);
            }
            
            // For large scroll positions where the viewport actually needs to scroll past
            // the initial view, use WaitUntil to verify the scroll completed. This is
            // critical in CI where processing hundreds of key events takes real time.
            var visibleRows = Math.Max(1, (testCase.Height - 4) / 2);
            var effectiveFinalRow = testCase.FocusedRow >= 0 
                ? Math.Min(testCase.FocusedRow + testCase.ScrollPosition, testCase.RowCount - 1)
                : Math.Min(testCase.ScrollPosition, testCase.RowCount - 1);
            var viewportWillScroll = effectiveFinalRow > visibleRows * 2 
                && testCase.FocusedRow < testCase.ScrollPosition;
            
            if (viewportWillScroll)
            {
                var checkIndex = Math.Min(effectiveFinalRow - visibleRows / 2, data.Count - 1);
                var expectedName = data[checkIndex].Name;
                waitBuilder.WaitUntil(
                    s => s.ContainsText(expectedName),
                    TimeSpan.FromSeconds(30),
                    $"Wait for scrolled content ({expectedName})");
            }
            else
            {
                waitBuilder.Wait(TimeSpan.FromMilliseconds(100));
            }
        }
        
        // Wait a bit for final render to stabilize
        waitBuilder.Wait(TimeSpan.FromMilliseconds(50));
        
        // Apply the wait/scroll sequence
        await waitBuilder.Build().ApplyAsync(terminal, cancellationToken);
        
        // Capture the screen BEFORE sending exit
        using var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();
        var ansi = GetAnsiFromSnapshot(snapshot);
        
        // Now exit the app
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cancellationToken);
        
        // Wait for run to complete
        try
        {
            await runTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when Ctrl+C is sent
        }
        
        return (ansi, text);
    }
    
    /// <summary>
    /// Extracts ANSI-formatted text from a terminal snapshot.
    /// </summary>
    private static string GetAnsiFromSnapshot(Hex1bTerminalSnapshot snapshot)
    {
        var sb = new System.Text.StringBuilder();
        
        for (var y = 0; y < snapshot.Height; y++)
        {
            for (var x = 0; x < snapshot.Width; x++)
            {
                var cell = snapshot.GetCell(x, y);
                
                // Write foreground color if set
                if (cell.Foreground.HasValue)
                {
                    var fg = cell.Foreground.Value;
                    sb.Append($"\x1b[38;2;{fg.R};{fg.G};{fg.B}m");
                }
                
                // Write background color if set
                if (cell.Background.HasValue)
                {
                    var bg = cell.Background.Value;
                    sb.Append($"\x1b[48;2;{bg.R};{bg.G};{bg.B}m");
                }
                
                // Write character
                var ch = cell.Character;
                sb.Append(string.IsNullOrEmpty(ch) ? " " : ch);
                
                // Reset if we had colors
                if (cell.Foreground.HasValue || cell.Background.HasValue)
                {
                    sb.Append("\x1b[0m");
                }
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Renders and compares against baseline. Returns null if match, or error message if mismatch.
    /// If baseline doesn't exist, creates it and returns a message indicating this.
    /// </summary>
    public static async Task<string?> RenderAndCompareAsync(
        TableVisualTestCase testCase, 
        bool updateBaselines = false,
        CancellationToken cancellationToken = default)
    {
        var (ansi, text) = await RenderTableAsync(testCase, cancellationToken);
        
        var existingAnsi = BaselineManager.LoadBaseline(testCase.BaselineName, ansi: true);
        var existingText = BaselineManager.LoadBaseline(testCase.BaselineName, ansi: false);
        
        if (updateBaselines || existingAnsi == null || existingText == null)
        {
            BaselineManager.SaveBaseline(testCase.BaselineName, ansi, ansi: true);
            BaselineManager.SaveBaseline(testCase.BaselineName, text, ansi: false);
            
            if (existingAnsi == null || existingText == null)
                return $"BASELINE CREATED: {testCase.BaselineName} (run again to verify)";
            
            return null; // Updated existing baseline
        }
        
        // Compare text first (easier to debug)
        var textDiff = BaselineManager.CompareBaseline(existingText, text);
        if (textDiff != null)
            return $"Text baseline mismatch for {testCase.BaselineName}:\n{textDiff}";
        
        // Then compare ANSI
        var ansiDiff = BaselineManager.CompareBaseline(existingAnsi, ansi);
        if (ansiDiff != null)
            return $"ANSI baseline mismatch for {testCase.BaselineName}:\n{ansiDiff}";
        
        return null; // Match
    }
}
