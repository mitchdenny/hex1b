using Hex1b.Input;
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
                
                // Normalize empty characters (including unwritten marker)
                if (string.IsNullOrEmpty(legacyChar)) legacyChar = " ";
                if (string.IsNullOrEmpty(surfaceChar) || surfaceChar == Surfaces.SurfaceCells.UnwrittenMarker) surfaceChar = " ";
                
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
    /// Helper that renders a widget through full Hex1bApp reconciliation and compares
    /// Legacy vs Surface rendering modes.
    /// Use this for composite widgets like Picker that build internal structure during reconciliation.
    /// </summary>
    private async Task AssertWidgetRenderingMatches(
        Hex1bWidget widget,
        int width,
        int height,
        string expectedContent,
        Hex1bTheme? theme = null)
    {
        theme ??= Hex1bThemes.Default;
        
        // Run with Legacy rendering mode
        await using var legacyTerminal = Hex1bTerminal.CreateBuilder()
            .WithHeadless()
            .WithDimensions(width, height)
            .WithRenderingMode(RenderingMode.Legacy)
            .WithHex1bApp((app, options) =>
            {
                options.Theme = theme;
                return ctx => widget;
            })
            .Build();
        
        var legacyRunTask = legacyTerminal.RunAsync(TestContext.Current.CancellationToken);
        
        // Run until content appears, then exit
        var legacySnapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText(expectedContent), TimeSpan.FromSeconds(2), $"'{expectedContent}' to appear (Legacy)")
            .Capture("legacy")
            .Ctrl().Key(Hex1bKey.C)  // Exit the app
            .Build()
            .ApplyWithCaptureAsync(legacyTerminal, TestContext.Current.CancellationToken);
        
        await legacyRunTask;
        
        // Run with Surface rendering mode
        await using var surfaceTerminal = Hex1bTerminal.CreateBuilder()
            .WithHeadless()
            .WithDimensions(width, height)
            .WithRenderingMode(RenderingMode.Surface)
            .WithHex1bApp((app, options) =>
            {
                options.Theme = theme;
                return ctx => widget;
            })
            .Build();
        
        var surfaceRunTask = surfaceTerminal.RunAsync(TestContext.Current.CancellationToken);
        
        var surfaceSnapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText(expectedContent), TimeSpan.FromSeconds(2), $"'{expectedContent}' to appear (Surface)")
            .Capture("surface")
            .Ctrl().Key(Hex1bKey.C)  // Exit the app
            .Build()
            .ApplyWithCaptureAsync(surfaceTerminal, TestContext.Current.CancellationToken);
        
        await surfaceRunTask;
        
        // Compare cell by cell
        var differences = new List<string>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var legacyCell = legacySnapshot.GetCell(x, y);
                var surfaceCell = surfaceSnapshot.GetCell(x, y);
                
                // Compare characters
                if (legacyCell.Character != surfaceCell.Character)
                {
                    differences.Add($"[{x},{y}] char: Legacy='{legacyCell.Character}' Surface='{surfaceCell.Character}'");
                }
                // Compare foreground colors (handle nulls as defaults)
                else if (!ColorsMatch(legacyCell.Foreground ?? Hex1bColor.Default, surfaceCell.Foreground ?? Hex1bColor.Default))
                {
                    differences.Add($"[{x},{y}] fg: Legacy={legacyCell.Foreground} Surface={surfaceCell.Foreground}");
                }
                // Compare background colors (handle nulls as defaults)
                else if (!ColorsMatch(legacyCell.Background ?? Hex1bColor.Default, surfaceCell.Background ?? Hex1bColor.Default))
                {
                    differences.Add($"[{x},{y}] bg: Legacy={legacyCell.Background} Surface={surfaceCell.Background}");
                }
                
                if (differences.Count >= 10) break;
            }
            if (differences.Count >= 10) break;
        }
        
        if (differences.Count > 0)
        {
            var message = $"Rendering mismatch between Legacy and Surface modes:\n" +
                          $"First {Math.Min(10, differences.Count)} differences:\n" +
                          string.Join("\n", differences);
            Assert.Fail(message);
        }
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

    #region TextBoxNode Tests

    [Fact]
    public async Task TextBoxNode_Empty_RendersIdentically()
    {
        var node = new TextBoxNode { Text = "" };
        // TextBox shows placeholder or empty area - just verify render consistency
        await AssertRenderingMatches(node, 30, 3, expectedContent: " ");
    }

    [Fact]
    public async Task TextBoxNode_WithText_RendersIdentically()
    {
        var node = new TextBoxNode { Text = "Hello World" };
        await AssertRenderingMatches(node, 30, 3, expectedContent: "Hello");
    }

    [Fact]
    public async Task TextBoxNode_Focused_RendersIdentically()
    {
        var node = new TextBoxNode { Text = "Focused", IsFocused = true };
        await AssertRenderingMatches(node, 30, 3, expectedContent: "Focused");
    }

    #endregion

    #region ToggleSwitchNode Tests

    [Fact]
    public async Task ToggleSwitchNode_WithOptions_RendersIdentically()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["On", "Off"],
            SelectedIndex = 0
        };
        await AssertRenderingMatches(node, 20, 3, expectedContent: "On");
    }

    [Fact]
    public async Task ToggleSwitchNode_SecondOption_RendersIdentically()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["Yes", "No", "Maybe"],
            SelectedIndex = 2
        };
        await AssertRenderingMatches(node, 25, 3, expectedContent: "Maybe");
    }

    #endregion

    #region HyperlinkNode Tests

    [Fact]
    public async Task HyperlinkNode_Basic_RendersIdentically()
    {
        var node = new HyperlinkNode
        {
            Text = "Click here",
            Uri = "https://example.com"
        };
        await AssertRenderingMatches(node, 20, 3, expectedContent: "Click");
    }

    #endregion

    #region SeparatorNode Tests

    [Fact]
    public async Task SeparatorNode_Horizontal_RendersIdentically()
    {
        // In a VStack context, separator is horizontal
        var node = new SeparatorNode { InferredAxis = LayoutAxis.Vertical };
        await AssertRenderingMatches(node, 20, 3, expectedContent: "─");
    }

    [Fact]
    public async Task SeparatorNode_Vertical_RendersIdentically()
    {
        // In an HStack context, separator is vertical
        var node = new SeparatorNode { InferredAxis = LayoutAxis.Horizontal };
        await AssertRenderingMatches(node, 20, 3, expectedContent: "│");
    }

    #endregion

    #region AlignNode Tests

    [Fact]
    public async Task AlignNode_Center_RendersIdentically()
    {
        var node = new AlignNode
        {
            Alignment = Alignment.Center,
            Child = new TextBlockNode { Text = "Centered" }
        };
        await AssertRenderingMatches(node, 30, 5, expectedContent: "Centered");
    }

    [Fact]
    public async Task AlignNode_TopLeft_RendersIdentically()
    {
        var node = new AlignNode
        {
            Alignment = Alignment.TopLeft,
            Child = new TextBlockNode { Text = "TopLeft" }
        };
        await AssertRenderingMatches(node, 20, 5, expectedContent: "TopLeft");
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

    #region ZStackNode Tests

    [Fact]
    public async Task ZStackNode_SingleChild_RendersIdentically()
    {
        var node = new ZStackNode
        {
            Children = [new TextBlockNode { Text = "Layer 1" }]
        };
        await AssertRenderingMatches(node, 20, 5, expectedContent: "Layer 1");
    }

    [Fact]
    public async Task ZStackNode_MultipleChildren_RendersIdentically()
    {
        var node = new ZStackNode
        {
            Children = 
            [
                new TextBlockNode { Text = "Bottom" },
                new TextBlockNode { Text = "Top" }
            ]
        };
        // Top layer should be visible
        await AssertRenderingMatches(node, 20, 5, expectedContent: "Top");
    }

    #endregion

    #region ScrollNode Tests

    [Fact]
    public async Task ScrollNode_BasicContent_RendersIdentically()
    {
        var node = new ScrollNode
        {
            Child = new TextBlockNode { Text = "Scrollable content" }
        };
        await AssertRenderingMatches(node, 30, 5, expectedContent: "Scrollable");
    }

    [Fact]
    public async Task ScrollNode_WithVStackContent_RendersIdentically()
    {
        var node = new ScrollNode
        {
            Child = new VStackNode
            {
                Children =
                [
                    new TextBlockNode { Text = "Line 1" },
                    new TextBlockNode { Text = "Line 2" },
                    new TextBlockNode { Text = "Line 3" }
                ]
            }
        };
        await AssertRenderingMatches(node, 20, 5, expectedContent: "Line 1");
    }

    #endregion

    #region InfoBarNode Tests

    [Fact]
    public async Task InfoBarNode_SingleSection_RendersIdentically()
    {
        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Status: Ready")]
        };
        await AssertRenderingMatches(node, 30, 3, expectedContent: "Status");
    }

    [Fact]
    public async Task InfoBarNode_MultipleSections_RendersIdentically()
    {
        var node = new InfoBarNode
        {
            Sections = 
            [
                new InfoBarSection("Mode: Normal"),
                new InfoBarSection(" | "),
                new InfoBarSection("Line 1")
            ]
        };
        await AssertRenderingMatches(node, 40, 3, expectedContent: "Mode");
    }

    #endregion

    #region QrCodeNode Tests

    [Fact]
    public async Task QrCodeNode_Basic_RendersIdentically()
    {
        var node = new QrCodeNode { Data = "https://example.com" };
        // QR code uses double-width block characters per module
        await AssertRenderingMatches(node, 80, 40, expectedContent: "█");
    }

    #endregion

    #region DrawerNode Tests

    [Fact]
    public async Task DrawerNode_Collapsed_RendersIdentically()
    {
        var node = new DrawerNode
        {
            IsExpanded = false,
            Content = new TextBlockNode { Text = "Collapsed" }
        };
        await AssertRenderingMatches(node, 30, 5, expectedContent: "Collapsed");
    }

    [Fact]
    public async Task DrawerNode_Expanded_RendersIdentically()
    {
        var node = new DrawerNode
        {
            IsExpanded = true,
            Content = new TextBlockNode { Text = "Expanded content" }
        };
        await AssertRenderingMatches(node, 30, 5, expectedContent: "Expanded");
    }

    #endregion

    #region SplitterNode Tests

    [Fact]
    public async Task SplitterNode_Horizontal_RendersIdentically()
    {
        var node = new SplitterNode
        {
            Orientation = SplitterOrientation.Horizontal,
            FirstSize = 10,
            First = new TextBlockNode { Text = "Left" },
            Second = new TextBlockNode { Text = "Right" }
        };
        await AssertRenderingMatches(node, 30, 5, expectedContent: "Left");
    }

    [Fact]
    public async Task SplitterNode_Vertical_RendersIdentically()
    {
        var node = new SplitterNode
        {
            Orientation = SplitterOrientation.Vertical,
            FirstSize = 3,
            First = new TextBlockNode { Text = "Top" },
            Second = new TextBlockNode { Text = "Bottom" }
        };
        await AssertRenderingMatches(node, 20, 10, expectedContent: "Top");
    }

    #endregion

    #region BackdropNode Tests

    [Fact]
    public async Task BackdropNode_Transparent_RendersIdentically()
    {
        var node = new BackdropNode
        {
            Style = BackdropStyle.Transparent,
            Child = new TextBlockNode { Text = "On backdrop" }
        };
        await AssertRenderingMatches(node, 25, 5, expectedContent: "backdrop");
    }

    [Fact]
    public async Task BackdropNode_Opaque_RendersIdentically()
    {
        var node = new BackdropNode
        {
            Style = BackdropStyle.Opaque,
            BackgroundColor = Hex1bColor.DarkGray,
            Child = new TextBlockNode { Text = "Opaque BG" }
        };
        await AssertRenderingMatches(node, 20, 5, expectedContent: "Opaque");
    }

    #endregion

    #region ThemePanelNode Tests

    [Fact]
    public async Task ThemePanelNode_WithChild_RendersIdentically()
    {
        var node = new ThemePanelNode
        {
            Child = new TextBlockNode { Text = "Themed content" }
        };
        await AssertRenderingMatches(node, 25, 5, expectedContent: "Themed");
    }

    #endregion

    #region ResponsiveNode Tests

    [Fact]
    public async Task ResponsiveNode_FirstBranchActive_RendersIdentically()
    {
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => w > 10, new TextBlockWidget("Wide")),
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Narrow"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Wide" },
                new TextBlockNode { Text = "Narrow" }
            ]
        };
        await AssertRenderingMatches(node, 30, 5, expectedContent: "Wide");
    }

    #endregion

    #region PickerWidget Integration Tests

    [Fact]
    public async Task PickerWidget_Collapsed_RendersIdentically()
    {
        var widget = new PickerWidget(["Option 1", "Option 2", "Option 3"]);
        await AssertWidgetRenderingMatches(widget, 25, 5, expectedContent: "Option 1");
    }

    [Fact]
    public async Task PickerWidget_WithInitialSelection_RendersIdentically()
    {
        var widget = new PickerWidget(["Apple", "Banana", "Cherry"]) { InitialSelectedIndex = 1 };
        await AssertWidgetRenderingMatches(widget, 25, 5, expectedContent: "Banana");
    }

    #endregion

    #region MenuBarWidget Integration Tests

    [Fact]
    public async Task MenuBarWidget_Basic_RendersIdentically()
    {
        var widget = new MenuBarWidget([
            new MenuWidget("File", [
                new MenuItemWidget("New"),
                new MenuItemWidget("Open")
            ]),
            new MenuWidget("Edit", [
                new MenuItemWidget("Cut"),
                new MenuItemWidget("Copy")
            ])
        ]);
        await AssertWidgetRenderingMatches(widget, 40, 5, expectedContent: "File");
    }

    #endregion
}
