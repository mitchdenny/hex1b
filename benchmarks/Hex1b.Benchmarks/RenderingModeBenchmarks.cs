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

    #region Multi-Frame Caching Benchmarks
    
    /// <summary>
    /// Benchmarks 100 frames with 0% dirty rate (completely static UI).
    /// This is the best case for caching - all cache hits after first frame.
    /// </summary>
    [Benchmark]
    public (long cacheHits, long cacheMisses) MultiFrame_100Frames_0PercentDirty()
    {
        var surface = new Surface(TerminalWidth, TerminalHeight);
        var context = new SurfaceRenderContext(surface, _theme) { CachingEnabled = true };
        
        // Create a VStack with nested content
        var root = CreateCacheableTree();
        MeasureAndArrange(root);
        
        long totalHits = 0, totalMisses = 0;
        
        // Simulate 100 frames with no changes
        for (int frame = 0; frame < 100; frame++)
        {
            surface.Clear();
            context.ResetCacheStats();
            context.RenderChild(root);
            totalHits += context.CacheHits;
            totalMisses += context.CacheMisses;
            
            // Clear dirty at end of frame (simulates Hex1bApp behavior)
            ClearAllDirty(root);
        }
        
        return (totalHits, totalMisses);
    }
    
    /// <summary>
    /// Benchmarks 100 frames with 5% dirty rate (typical: cursor blink, clock).
    /// One node marked dirty each frame to simulate realistic updates.
    /// </summary>
    [Benchmark]
    public (long cacheHits, long cacheMisses) MultiFrame_100Frames_5PercentDirty()
    {
        var surface = new Surface(TerminalWidth, TerminalHeight);
        var context = new SurfaceRenderContext(surface, _theme) { CachingEnabled = true };
        
        // Create a tree with 20 children
        var children = Enumerable.Range(0, 20)
            .Select(i => new TextBlockNode { Text = $"Item {i}" } as Hex1bNode)
            .ToList();
        var root = new VStackNode { Children = children };
        MeasureAndArrange(root);
        
        long totalHits = 0, totalMisses = 0;
        
        // First frame: everything is dirty (cold cache)
        surface.Clear();
        context.ResetCacheStats();
        context.RenderChild(root);
        totalMisses += context.CacheMisses;
        totalHits += context.CacheHits;
        ClearAllDirty(root);
        
        // Subsequent frames: mark ~1 node dirty (5% of 20 = 1)
        for (int frame = 1; frame < 100; frame++)
        {
            // Mark one child dirty per frame (rotating)
            children[frame % 20].MarkDirty();
            
            surface.Clear();
            context.ResetCacheStats();
            context.RenderChild(root);
            totalMisses += context.CacheMisses;
            totalHits += context.CacheHits;
            
            // Clear dirty at end of frame
            ClearAllDirty(root);
        }
        
        return (totalHits, totalMisses);
    }
    
    /// <summary>
    /// Benchmarks 100 frames with 50% dirty rate (heavy updates).
    /// Half the nodes change each frame.
    /// </summary>
    [Benchmark]
    public (long cacheHits, long cacheMisses) MultiFrame_100Frames_50PercentDirty()
    {
        var surface = new Surface(TerminalWidth, TerminalHeight);
        var context = new SurfaceRenderContext(surface, _theme) { CachingEnabled = true };
        
        var children = Enumerable.Range(0, 20)
            .Select(i => new TextBlockNode { Text = $"Item {i}" } as Hex1bNode)
            .ToList();
        var root = new VStackNode { Children = children };
        MeasureAndArrange(root);
        
        long totalHits = 0, totalMisses = 0;
        
        for (int frame = 0; frame < 100; frame++)
        {
            // Mark 50% of children dirty (even indices on even frames, odd on odd)
            for (int i = frame % 2; i < 20; i += 2)
            {
                children[i].MarkDirty();
            }
            
            surface.Clear();
            context.ResetCacheStats();
            context.RenderChild(root);
            totalMisses += context.CacheMisses;
            totalHits += context.CacheHits;
            
            // Clear dirty at end of frame
            ClearAllDirty(root);
        }
        
        return (totalHits, totalMisses);
    }
    
    /// <summary>
    /// Benchmarks 100 frames with 100% dirty rate (full redraw every frame).
    /// This is the worst case for caching - should verify no regression.
    /// </summary>
    [Benchmark]
    public (long cacheHits, long cacheMisses) MultiFrame_100Frames_100PercentDirty()
    {
        var surface = new Surface(TerminalWidth, TerminalHeight);
        var context = new SurfaceRenderContext(surface, _theme) { CachingEnabled = true };
        
        var children = Enumerable.Range(0, 20)
            .Select(i => new TextBlockNode { Text = $"Item {i}" } as Hex1bNode)
            .ToList();
        var root = new VStackNode { Children = children };
        MeasureAndArrange(root);
        
        long totalHits = 0, totalMisses = 0;
        
        for (int frame = 0; frame < 100; frame++)
        {
            // Mark ALL children dirty
            foreach (var child in children)
            {
                child.MarkDirty();
            }
            root.MarkDirty();
            
            surface.Clear();
            context.ResetCacheStats();
            context.RenderChild(root);
            totalMisses += context.CacheMisses;
            totalHits += context.CacheHits;
            
            // Clear dirty at end of frame (though we mark everything dirty next frame anyway)
            ClearAllDirty(root);
        }
        
        return (totalHits, totalMisses);
    }
    
    /// <summary>
    /// Baseline: 100 frames with caching DISABLED.
    /// Used to compare against cached versions.
    /// </summary>
    [Benchmark]
    public int MultiFrame_100Frames_NoCaching()
    {
        var surface = new Surface(TerminalWidth, TerminalHeight);
        var context = new SurfaceRenderContext(surface, _theme) { CachingEnabled = false };
        
        var children = Enumerable.Range(0, 20)
            .Select(i => new TextBlockNode { Text = $"Item {i}" } as Hex1bNode)
            .ToList();
        var root = new VStackNode { Children = children };
        MeasureAndArrange(root);
        
        int renderedFrames = 0;
        for (int frame = 0; frame < 100; frame++)
        {
            surface.Clear();
            context.RenderChild(root);
            ClearAllDirty(root);  // For fair comparison with cached versions
            renderedFrames++;
        }
        
        return renderedFrames;
    }
    
    /// <summary>
    /// Creates a realistic widget tree for caching benchmarks.
    /// </summary>
    private Hex1bNode CreateCacheableTree()
    {
        return new BorderNode
        {
            Title = "Cache Test",
            Child = new VStackNode
            {
                Children =
                [
                    new TextBlockNode { Text = "Header" },
                    new HStackNode
                    {
                        Children =
                        [
                            new TextBlockNode { Text = "Col 1" },
                            new TextBlockNode { Text = "Col 2" },
                            new TextBlockNode { Text = "Col 3" }
                        ]
                    },
                    new TextBlockNode { Text = "Row 1: Some data here" },
                    new TextBlockNode { Text = "Row 2: More data" },
                    new TextBlockNode { Text = "Row 3: And more" },
                    new ButtonNode { Label = "OK" },
                    new TextBlockNode { Text = "Footer" }
                ]
            }
        };
    }
    
    private void MeasureAndArrange(Hex1bNode node)
    {
        node.Measure(new Constraints(0, TerminalWidth, 0, TerminalHeight));
        node.Arrange(new Rect(0, 0, TerminalWidth, TerminalHeight));
    }
    
    /// <summary>
    /// Clears dirty flags on a node and all its descendants.
    /// This simulates end-of-frame behavior in Hex1bApp.
    /// </summary>
    private static void ClearAllDirty(Hex1bNode node)
    {
        node.ClearDirty();
        foreach (var child in GetAllChildren(node))
        {
            ClearAllDirty(child);
        }
    }
    
    /// <summary>
    /// Gets all direct children of a node for recursive operations.
    /// </summary>
    private static IEnumerable<Hex1bNode> GetAllChildren(Hex1bNode node)
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
