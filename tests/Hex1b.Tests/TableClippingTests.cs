using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for table clipping when inside a window that is resized.
/// </summary>
public class TableClippingTests
{
    /// <summary>
    /// When a window containing a table is resized so the table is clipped,
    /// the table borders should NOT render outside the window border.
    /// </summary>
    [Fact]
    public async Task Table_InsideResizedWindow_ClippedToWindowBounds()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(80, 24)
            .Build();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new WindowPanelWidget(
                    new VStackWidget([
                        new ButtonWidget("Open Table Window").OnClick(e =>
                        {
                            var handle = e.Windows.Window(_ => new TableWidget<Employee>
                                {
                                    Data = SampleEmployees.ToList(),
                                    HeaderBuilder = h => [h.Cell("ID").Fixed(5), h.Cell("Name").Fixed(15), h.Cell("Dept").Fixed(15)],
                                    RowBuilder = (r, emp, _) => [r.Cell(emp.Id.ToString()), r.Cell(emp.Name), r.Cell(emp.Department)]
                                })
                                .Title("Employees")
                                .Size(50, 12)
                                .Position(new WindowPositionSpec(WindowPosition.TopLeft, OffsetX: 5, OffsetY: 3))
                                .Resizable();
                            e.Windows.Open(handle);
                        })
                    ])
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Open the window
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Open Table Window"), TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("Employees"), TimeSpan.FromSeconds(2))
            .Capture("table-before-resize")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Now resize the window by dragging the right edge left
        // Window is at x=5, width=50, so right edge is at x=54
        // Drag from (54, 8) to (30, 8) - shrinking width by 24
        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(54, 8, 30, 8)
            .WaitUntil(s =>
            {
                // Verify the window has actually been resized.
                // After drag, the right window border should have moved from column 54
                // to approximately column 30. Column 54 should now be empty/space.
                var line = s.GetLine(8);
                return line.Length > 54 && line[54] == ' ';
            }, TimeSpan.FromSeconds(2), "Window right edge should move from column 54 after resize drag")
            .Capture("table-after-resize")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        var snapshot = terminal.CreateSnapshot();
        
        // Write all lines to debug
        var debugPath = "/tmp/table-clipping-debug.txt";
        var lines = new System.Text.StringBuilder();
        for (int y = 0; y < 24; y++)
        {
            var line = snapshot.GetLine(y);
            lines.AppendLine($"Line {y:D2}: |{line}|");
        }
        System.IO.File.WriteAllText(debugPath, lines.ToString());
        
        // After resize: window spans x=5 to x=30 (width 26).
        // The window border is at x=30, so columns 31+ should be empty.
        // Check that table border characters don't leak beyond the new window boundary.
        
        var clippingFailed = false;
        var diagnostics = new System.Text.StringBuilder();
        diagnostics.AppendLine("After resize, checking lines 4-12 at column 31+ (outside window):");
        for (int y = 4; y < 13; y++) // Window content area (roughly)
        {
            var line = snapshot.GetLine(y);
            diagnostics.AppendLine($"  Line {y} (len={line.Length}): '{(line.Length > 25 ? line.Substring(25, Math.Min(20, line.Length - 25)) : "")}'");
            
            // Check columns beyond the new window boundary (31+) for table border leaks
            var boxDrawingChars = new[] { '│', '┼', '├', '┤', '─', '┬', '┴', '┌', '┐', '└', '┘' };
            for (int x = 31; x < Math.Min(55, line.Length); x++)
            {
                if (boxDrawingChars.Contains(line[x]))
                {
                    diagnostics.AppendLine($"  CLIPPING BUG: Line {y} has box char '{line[x]}' at x={x} (outside window boundary)");
                    clippingFailed = true;
                }
            }
        }
        
        // Exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
        
        Assert.False(clippingFailed, $"Table content rendered outside window bounds.\n{diagnostics}");
    }
    
    private record Employee(int Id, string Name, string Department);
    
    private static readonly Employee[] SampleEmployees =
    [
        new(1, "Alice Smith", "Engineering"),
        new(2, "Bob Jones", "Marketing"),
        new(3, "Carol White", "Engineering"),
        new(4, "David Brown", "Sales"),
        new(5, "Eve Davis", "Engineering")
    ];
}
