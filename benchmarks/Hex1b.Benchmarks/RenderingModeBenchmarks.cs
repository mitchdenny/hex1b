using BenchmarkDotNet.Attributes;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Benchmarks;

/// <summary>
/// Benchmarks comparing Legacy (ANSI string) vs Surface rendering paths.
/// These benchmarks measure the full rendering pipeline from node render to output generation.
/// </summary>
[MemoryDiagnoser]
public class RenderingModeBenchmarks
{
    // Simple widget tree
    private TextBlockNode _textBlockNode = null!;
    private ButtonNode _buttonNode = null!;
    
    // Complex widget tree
    private VStackNode _simpleLayoutNode = null!;
    private BorderNode _nestedLayoutNode = null!;
    private ZStackNode _complexLayoutNode = null!;
    
    // Render contexts
    private Hex1bRenderContext _legacyContext = null!;
    private SurfaceRenderContext _surfaceContext = null!;
    private Surface _surface = null!;
    private Surface _previousSurface = null!;
    
    private Hex1bTheme _theme = null!;
    private Hex1bAppWorkloadAdapter _workload = null!;

    private const int TerminalWidth = 80;
    private const int TerminalHeight = 24;

    [GlobalSetup]
    public void Setup()
    {
        _theme = Hex1bThemes.Default;
        _workload = new Hex1bAppWorkloadAdapter();
        
        // Setup simple nodes
        _textBlockNode = new TextBlockNode { Text = "Hello World! This is a test of the rendering pipeline." };
        _textBlockNode.Measure(new Constraints(0, TerminalWidth, 0, TerminalHeight));
        _textBlockNode.Arrange(new Rect(0, 0, TerminalWidth, 1));
        
        _buttonNode = new ButtonNode { Label = "Click Me", IsFocused = true };
        _buttonNode.Measure(new Constraints(0, 20, 0, 3));
        _buttonNode.Arrange(new Rect(0, 0, 20, 3));
        
        // Setup simple layout (VStack with 5 text blocks)
        _simpleLayoutNode = new VStackNode
        {
            Children = Enumerable.Range(0, 5).Select(i => 
                new TextBlockNode { Text = $"Line {i + 1}: This is some sample content for benchmarking." } as Hex1bNode
            ).ToList()
        };
        _simpleLayoutNode.Measure(new Constraints(0, TerminalWidth, 0, TerminalHeight));
        _simpleLayoutNode.Arrange(new Rect(0, 0, TerminalWidth, TerminalHeight));
        
        // Setup nested layout (Border with VStack inside)
        _nestedLayoutNode = new BorderNode
        {
            Title = "Benchmark Panel",
            Child = new VStackNode
            {
                Children =
                [
                    new TextBlockNode { Text = "Header Content" },
                    new HStackNode
                    {
                        Children =
                        [
                            new ButtonNode { Label = "OK" },
                            new ButtonNode { Label = "Cancel" }
                        ]
                    },
                    new ProgressNode { Value = 50, Maximum = 100 },
                    new TextBlockNode { Text = "Footer Content" }
                ]
            }
        };
        _nestedLayoutNode.Measure(new Constraints(0, TerminalWidth, 0, TerminalHeight));
        _nestedLayoutNode.Arrange(new Rect(0, 0, TerminalWidth, TerminalHeight));
        
        // Setup complex layout (ZStack with multiple layers)
        _complexLayoutNode = new ZStackNode
        {
            Children =
            [
                // Background layer
                new VStackNode
                {
                    Children = Enumerable.Range(0, 20).Select(i =>
                        new TextBlockNode { Text = new string('░', TerminalWidth) } as Hex1bNode
                    ).ToList()
                },
                // Content layer
                new BorderNode
                {
                    Title = "Main Content",
                    Child = new VStackNode
                    {
                        Children =
                        [
                            new TextBlockNode { Text = "Application Title" },
                            new ListNode { Items = ["Item 1", "Item 2", "Item 3", "Item 4", "Item 5"], SelectedIndex = 2 },
                            new HStackNode
                            {
                                Children =
                                [
                                    new ButtonNode { Label = "Select", IsFocused = true },
                                    new ButtonNode { Label = "Cancel" }
                                ]
                            }
                        ]
                    }
                }
            ]
        };
        _complexLayoutNode.Measure(new Constraints(0, TerminalWidth, 0, TerminalHeight));
        _complexLayoutNode.Arrange(new Rect(0, 0, TerminalWidth, TerminalHeight));
        
        // Setup render contexts
        _legacyContext = new Hex1bRenderContext(_workload, _theme);
        _surface = new Surface(TerminalWidth, TerminalHeight);
        _previousSurface = new Surface(TerminalWidth, TerminalHeight);
        _surfaceContext = new SurfaceRenderContext(_surface, _theme);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _workload.Dispose();
    }

    #region Simple Node Rendering

    [Benchmark(Baseline = true)]
    public void TextBlock_Legacy()
    {
        _legacyContext.SetCursorPosition(0, 0);
        _textBlockNode.Render(_legacyContext);
    }

    [Benchmark]
    public void TextBlock_Surface()
    {
        _surface.Clear();
        _surfaceContext.SetCursorPosition(0, 0);
        _textBlockNode.Render(_surfaceContext);
    }

    [Benchmark]
    public string TextBlock_Surface_WithDiff()
    {
        _surface.Clear();
        _surfaceContext.SetCursorPosition(0, 0);
        _textBlockNode.Render(_surfaceContext);
        
        var diff = SurfaceComparer.Compare(_previousSurface, _surface);
        return SurfaceComparer.ToAnsiString(diff);
    }

    #endregion

    #region Button Rendering

    [Benchmark]
    public void Button_Legacy()
    {
        _legacyContext.SetCursorPosition(0, 0);
        _buttonNode.Render(_legacyContext);
    }

    [Benchmark]
    public void Button_Surface()
    {
        _surface.Clear();
        _surfaceContext.SetCursorPosition(0, 0);
        _buttonNode.Render(_surfaceContext);
    }

    #endregion

    #region Simple Layout Rendering

    [Benchmark]
    public void SimpleLayout_Legacy()
    {
        RenderTreeLegacy(_simpleLayoutNode);
    }

    [Benchmark]
    public void SimpleLayout_Surface()
    {
        _surface.Clear();
        RenderTreeSurface(_simpleLayoutNode);
    }

    [Benchmark]
    public string SimpleLayout_Surface_WithDiff()
    {
        _surface.Clear();
        RenderTreeSurface(_simpleLayoutNode);
        
        var diff = SurfaceComparer.Compare(_previousSurface, _surface);
        return SurfaceComparer.ToAnsiString(diff);
    }

    #endregion

    #region Nested Layout Rendering

    [Benchmark]
    public void NestedLayout_Legacy()
    {
        RenderTreeLegacy(_nestedLayoutNode);
    }

    [Benchmark]
    public void NestedLayout_Surface()
    {
        _surface.Clear();
        RenderTreeSurface(_nestedLayoutNode);
    }

    [Benchmark]
    public string NestedLayout_Surface_WithDiff()
    {
        _surface.Clear();
        RenderTreeSurface(_nestedLayoutNode);
        
        var diff = SurfaceComparer.Compare(_previousSurface, _surface);
        return SurfaceComparer.ToAnsiString(diff);
    }

    #endregion

    #region Complex Layout Rendering

    [Benchmark]
    public void ComplexLayout_Legacy()
    {
        RenderTreeLegacy(_complexLayoutNode);
    }

    [Benchmark]
    public void ComplexLayout_Surface()
    {
        _surface.Clear();
        RenderTreeSurface(_complexLayoutNode);
    }

    [Benchmark]
    public string ComplexLayout_Surface_WithDiff()
    {
        _surface.Clear();
        RenderTreeSurface(_complexLayoutNode);
        
        var diff = SurfaceComparer.Compare(_previousSurface, _surface);
        return SurfaceComparer.ToAnsiString(diff);
    }

    #endregion

    #region Incremental Update Simulation

    [Benchmark]
    public string IncrementalUpdate_SparseChange()
    {
        // Simulate a sparse change (only a few cells different)
        _surface.Clear();
        RenderTreeSurface(_simpleLayoutNode);
        
        // Previous surface has almost the same content
        _previousSurface.Clear();
        RenderTreeSurface(_simpleLayoutNode);
        
        // Change just a few cells
        _surface.WriteText(0, 0, "CHANGED", Hex1bColor.Red, null);
        
        var diff = SurfaceComparer.Compare(_previousSurface, _surface);
        return SurfaceComparer.ToAnsiString(diff);
    }

    [Benchmark]
    public string IncrementalUpdate_NoChange()
    {
        // Simulate no change (same content)
        _surface.Clear();
        RenderTreeSurface(_simpleLayoutNode);
        
        _previousSurface.Clear();
        RenderTreeSurface(_simpleLayoutNode);
        
        var diff = SurfaceComparer.Compare(_previousSurface, _surface);
        return SurfaceComparer.ToAnsiString(diff);
    }

    #endregion

    #region Output Efficiency

    [Benchmark]
    public int OutputSize_Legacy_FullScreen()
    {
        // Legacy generates ANSI for everything every frame
        var sb = new System.Text.StringBuilder();
        for (int y = 0; y < TerminalHeight; y++)
        {
            for (int x = 0; x < TerminalWidth; x++)
            {
                sb.Append($"\x1b[{y + 1};{x + 1}H");  // Move cursor
                sb.Append("\x1b[37;40m");  // Set colors
                sb.Append(' ');  // Character
            }
        }
        return sb.Length;
    }

    [Benchmark]
    public int OutputSize_Surface_FullScreen()
    {
        // Surface generates optimized diff output
        _surface.Fill(new Rect(0, 0, TerminalWidth, TerminalHeight), 
            new SurfaceCell(" ", Hex1bColor.White, Hex1bColor.Black));
        _previousSurface.Clear();
        
        var diff = SurfaceComparer.Compare(_previousSurface, _surface);
        var output = SurfaceComparer.ToAnsiString(diff);
        return output.Length;
    }

    [Benchmark]
    public int OutputSize_Surface_NoChange()
    {
        // When nothing changes, Surface outputs nothing
        _surface.Fill(new Rect(0, 0, TerminalWidth, TerminalHeight), 
            new SurfaceCell(" ", Hex1bColor.White, Hex1bColor.Black));
        _previousSurface.Fill(new Rect(0, 0, TerminalWidth, TerminalHeight), 
            new SurfaceCell(" ", Hex1bColor.White, Hex1bColor.Black));
        
        var diff = SurfaceComparer.Compare(_previousSurface, _surface);
        var output = SurfaceComparer.ToAnsiString(diff);
        return output.Length;  // Should be 0 or minimal
    }

    [Benchmark]
    public int OutputSize_Surface_SparseUpdate()
    {
        // Typical case: only a few cells changed
        _surface.Fill(new Rect(0, 0, TerminalWidth, TerminalHeight), 
            new SurfaceCell(" ", Hex1bColor.White, Hex1bColor.Black));
        _previousSurface.Fill(new Rect(0, 0, TerminalWidth, TerminalHeight), 
            new SurfaceCell(" ", Hex1bColor.White, Hex1bColor.Black));
        
        // Change just 5% of cells
        for (int y = 0; y < TerminalHeight; y += 4)
        {
            _surface.WriteText(0, y, "UPDATED", Hex1bColor.Red, null);
        }
        
        var diff = SurfaceComparer.Compare(_previousSurface, _surface);
        var output = SurfaceComparer.ToAnsiString(diff);
        return output.Length;
    }

    #endregion

    #region Helper Methods

    private void RenderTreeLegacy(Hex1bNode node)
    {
        _legacyContext.SetCursorPosition(node.Bounds.X, node.Bounds.Y);
        node.Render(_legacyContext);
        
        // Render children recursively
        foreach (var child in GetChildren(node))
        {
            RenderTreeLegacy(child);
        }
    }

    private void RenderTreeSurface(Hex1bNode node)
    {
        _surfaceContext.SetCursorPosition(node.Bounds.X, node.Bounds.Y);
        node.Render(_surfaceContext);
        
        // Render children recursively
        foreach (var child in GetChildren(node))
        {
            RenderTreeSurface(child);
        }
    }

    private static IEnumerable<Hex1bNode> GetChildren(Hex1bNode node)
    {
        return node switch
        {
            VStackNode vstack => vstack.Children,
            HStackNode hstack => hstack.Children,
            ZStackNode zstack => zstack.Children,
            BorderNode border => border.Child != null ? [border.Child] : [],
            _ => []
        };
    }

    #endregion
}
