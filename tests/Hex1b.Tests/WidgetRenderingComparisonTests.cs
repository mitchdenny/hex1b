using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// A/B comparison tests that verify widgets render identically via
/// Legacy (Hex1bRenderContext) and Surface (SurfaceRenderContext) paths.
/// </summary>
/// <remarks>
/// These tests ensure that the Surface-based rendering produces the same
/// visual output as the Legacy ANSI-based rendering. Each test:
/// 1. Creates a widget and its corresponding node
/// 2. Renders to both Legacy context (captured to terminal) and Surface context
/// 3. Compares the resulting cells
/// </remarks>
public class WidgetRenderingComparisonTests
{
    #region Test Infrastructure

    /// <summary>
    /// Helper that renders a node to both Legacy and Surface contexts and compares results.
    /// </summary>
    /// <param name="node">The node to render.</param>
    /// <param name="width">Terminal width.</param>
    /// <param name="height">Terminal height.</param>
    /// <param name="theme">Optional theme.</param>
    /// <param name="expectedContent">Content to wait for before comparing (ensures frame is rendered).</param>
    private async Task AssertRenderingMatches(
        Hex1bNode node,
        int width,
        int height,
        Hex1bTheme? theme = null,
        string? expectedContent = null)
    {
        theme ??= Hex1bThemes.Default;
        
        // Arrange node
        node.Measure(new Constraints(0, width, 0, height));
        node.Arrange(new Rect(0, 0, width, height));
        
        // Render via Legacy path (to terminal snapshot)
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(width, height)
            .Build();
        
        var legacyContext = new Hex1bRenderContext(workload, theme);
        legacyContext.SetCursorPosition(node.Bounds.X, node.Bounds.Y);
        node.Render(legacyContext);
        
        // Wait for expected content to appear (reliable frame sync)
        Hex1bTerminalSnapshot legacySnapshot;
        if (!string.IsNullOrEmpty(expectedContent))
        {
            legacySnapshot = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText(expectedContent), TimeSpan.FromSeconds(2), $"'{expectedContent}' to appear")
                .Capture("final")
                .Build()
                .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        }
        else
        {
            // For empty/minimal content, use a short wait with verification
            await new Hex1bTerminalInputSequenceBuilder()
                .Wait(TimeSpan.FromMilliseconds(100))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
            legacySnapshot = terminal.CreateSnapshot();
        }
        
        // Render via Surface path
        var surface = new Surface(width, height);
        var surfaceContext = new SurfaceRenderContext(surface, theme);
        surfaceContext.SetCursorPosition(node.Bounds.X, node.Bounds.Y);
        node.Render(surfaceContext);
        
        // Compare cell by cell
        var differences = new List<string>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var legacyCell = legacySnapshot.GetCell(x, y);
                var surfaceCell = surface[x, y];
                
                // Compare characters
                var legacyChar = legacyCell.Character ?? " ";
                var surfaceChar = surfaceCell.Character ?? " ";
                
                // Normalize empty characters
                if (string.IsNullOrEmpty(legacyChar)) legacyChar = " ";
                if (string.IsNullOrEmpty(surfaceChar)) surfaceChar = " ";
                
                if (legacyChar != surfaceChar)
                {
                    differences.Add($"[{x},{y}] char: Legacy='{legacyChar}' Surface='{surfaceChar}'");
                }
                
                // Compare foreground (if both are set)
                if (legacyCell.Foreground != null && surfaceCell.Foreground != null)
                {
                    if (!ColorsMatch(legacyCell.Foreground.Value, surfaceCell.Foreground.Value))
                    {
                        differences.Add($"[{x},{y}] fg: Legacy={legacyCell.Foreground} Surface={surfaceCell.Foreground}");
                    }
                }
                
                // Compare background (if both are set)
                if (legacyCell.Background != null && surfaceCell.Background != null)
                {
                    if (!ColorsMatch(legacyCell.Background.Value, surfaceCell.Background.Value))
                    {
                        differences.Add($"[{x},{y}] bg: Legacy={legacyCell.Background} Surface={surfaceCell.Background}");
                    }
                }
            }
        }
        
        if (differences.Count > 0)
        {
            var message = $"Rendering mismatch between Legacy and Surface:\n" +
                         $"First 10 differences:\n" +
                         string.Join("\n", differences.Take(10));
            Assert.Fail(message);
        }
    }
    
    /// <summary>
    /// Helper to compare colors with tolerance for default handling.
    /// </summary>
    private static bool ColorsMatch(Hex1bColor legacy, Hex1bColor surface)
    {
        if (legacy.IsDefault && surface.IsDefault) return true;
        if (legacy.IsDefault != surface.IsDefault) return false;
        
        // Compare RGB values
        return legacy.R == surface.R && 
               legacy.G == surface.G && 
               legacy.B == surface.B;
    }
    
    /// <summary>
    /// Helper that renders a widget through full reconciliation and compares.
    /// </summary>
    private async Task AssertWidgetRenderingMatches<TWidget>(
        TWidget widget,
        int width,
        int height,
        Hex1bTheme? theme = null) where TWidget : Hex1bWidget
    {
        theme ??= Hex1bThemes.Default;
        
        // Create a minimal Hex1bApp to do proper reconciliation
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(width, height)
            .WithHex1bApp((app, options) =>
            {
                options.Theme = theme;
                return ctx => widget;
            })
            .Build();
        
        // Run one frame to get rendering
        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100); // Let one frame render
        
        var legacySnapshot = terminal.CreateSnapshot();
        
        // Now render to surface using the same widget structure
        // We need to reconcile the widget to get the node
        var surface = new Surface(width, height);
        var surfaceContext = new SurfaceRenderContext(surface, theme);
        
        // For now, just compare the terminal snapshot line by line
        // Full widget reconciliation comparison would require more infrastructure
        
        // Stop the app
        // Note: Can't easily stop without a reference, so we'll use a timeout approach
    }

    #endregion

    #region TextBlockNode Tests

    [Fact]
    public async Task TextBlockNode_SimpleText_RendersIdentically()
    {
        var node = new TextBlockNode { Text = "Hello World" };
        await AssertRenderingMatches(node, 40, 5, expectedContent: "Hello World");
    }

    [Fact]
    public async Task TextBlockNode_EmptyText_RendersIdentically()
    {
        var node = new TextBlockNode { Text = "" };
        // Empty text has no expected content, will use short wait
        await AssertRenderingMatches(node, 40, 5);
    }

    [Fact]
    public async Task TextBlockNode_UnicodeCharacters_RendersIdentically()
    {
        var node = new TextBlockNode { Text = "Hello 世界! 🎉" };
        await AssertRenderingMatches(node, 40, 5, expectedContent: "Hello");
    }

    #endregion

    #region ButtonNode Tests

    [Fact]
    public async Task ButtonNode_Unfocused_RendersIdentically()
    {
        var node = new ButtonNode { Label = "Click Me" };
        await AssertRenderingMatches(node, 20, 3, expectedContent: "Click Me");
    }

    [Fact]
    public async Task ButtonNode_Focused_RendersIdentically()
    {
        var node = new ButtonNode { Label = "Click Me", IsFocused = true };
        await AssertRenderingMatches(node, 20, 3, expectedContent: "Click Me");
    }

    #endregion

    #region ProgressNode Tests

    [Fact]
    public async Task ProgressNode_ZeroProgress_RendersIdentically()
    {
        // Maximum defaults to 100.0, so Value=0 means 0%
        var node = new ProgressNode { Value = 0.0 };
        // At 0% we have all empty chars
        await AssertRenderingMatches(node, 30, 3, expectedContent: "░");
    }

    [Fact]
    public async Task ProgressNode_HalfProgress_RendersIdentically()
    {
        // Maximum defaults to 100.0, so Value=50 means 50%
        var node = new ProgressNode { Value = 50.0 };
        // At 50% we should have both filled and empty chars
        await AssertRenderingMatches(node, 30, 3, expectedContent: "█");
    }

    [Fact]
    public async Task ProgressNode_FullProgress_RendersIdentically()
    {
        // Maximum defaults to 100.0, so Value=100 means 100%
        var node = new ProgressNode { Value = 100.0 };
        // At 100%, all blocks should be filled
        await AssertRenderingMatches(node, 30, 3, expectedContent: "█");
    }

    #endregion

    #region HStackNode / VStackNode Tests

    [Fact]
    public async Task HStackNode_MultipleChildren_RendersIdentically()
    {
        var node = new HStackNode
        {
            Children = 
            [
                new TextBlockNode { Text = "First" },
                new TextBlockNode { Text = " | " },
                new TextBlockNode { Text = "Second" }
            ]
        };
        await AssertRenderingMatches(node, 40, 3, expectedContent: "First");
    }

    [Fact]
    public async Task VStackNode_MultipleChildren_RendersIdentically()
    {
        var node = new VStackNode
        {
            Children = 
            [
                new TextBlockNode { Text = "Line 1" },
                new TextBlockNode { Text = "Line 2" },
                new TextBlockNode { Text = "Line 3" }
            ]
        };
        await AssertRenderingMatches(node, 40, 10, expectedContent: "Line 1");
    }

    #endregion

    #region BorderNode Tests

    [Fact]
    public async Task BorderNode_WithTitle_RendersIdentically()
    {
        var node = new BorderNode
        {
            Title = "Test Border",
            Child = new TextBlockNode { Text = "Content" }
        };
        await AssertRenderingMatches(node, 30, 5, expectedContent: "Test Border");
    }

    [Fact]
    public async Task BorderNode_WithoutTitle_RendersIdentically()
    {
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Content" }
        };
        await AssertRenderingMatches(node, 30, 5, expectedContent: "Content");
    }

    #endregion

    #region ListNode Tests

    [Fact]
    public async Task ListNode_WithItems_RendersIdentically()
    {
        var node = new ListNode
        {
            Items = ["Apple", "Banana", "Cherry"],
            SelectedIndex = 1
        };
        await AssertRenderingMatches(node, 20, 5, expectedContent: "Banana");
    }

    #endregion
}
