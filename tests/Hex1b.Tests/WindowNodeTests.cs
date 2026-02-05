using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for WindowNode measurement, arrangement, and rendering.
/// </summary>
public class WindowNodeTests
{
    #region Measurement Tests

    [Fact]
    public void Measure_WithEntry_ReturnsEntrySize()
    {
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", () => new TextBlockWidget("Hello"), width: 50, height: 20);
        
        var node = new WindowNode { Entry = entry };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(50, size.Width);
        Assert.Equal(20, size.Height);
    }

    [Fact]
    public void Measure_WithSmallEntry_EnforcesMinimumSize()
    {
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", () => new TextBlockWidget("Hello"), width: 3, height: 2);
        
        var node = new WindowNode { Entry = entry };

        var size = node.Measure(Constraints.Unbounded);

        // Minimum size is 10x5 for border + title bar
        Assert.True(size.Width >= 10);
        Assert.True(size.Height >= 5);
    }

    [Fact]
    public void Measure_RespectsConstraints()
    {
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", () => new TextBlockWidget("Hello"), width: 100, height: 50);
        
        var node = new WindowNode { Entry = entry };

        var size = node.Measure(new Constraints(0, 60, 0, 30));

        Assert.True(size.Width <= 60);
        Assert.True(size.Height <= 30);
    }

    #endregion

    #region Arrangement Tests

    [Fact]
    public void Arrange_SetsNodeBounds()
    {
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", () => new TextBlockWidget("Hello"), width: 40, height: 15);
        
        var node = new WindowNode { Entry = entry };
        node.Measure(Constraints.Unbounded);

        node.Arrange(new Rect(10, 5, 40, 15));

        Assert.Equal(10, node.Bounds.X);
        Assert.Equal(5, node.Bounds.Y);
        Assert.Equal(40, node.Bounds.Width);
        Assert.Equal(15, node.Bounds.Height);
    }

    [Fact]
    public void Arrange_PositionsContentInInnerArea()
    {
        var content = new TextBlockNode { Text = "Content" };
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", () => new TextBlockWidget("Hello"), width: 40, height: 15);
        
        var node = new WindowNode { Entry = entry, Content = content };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(10, 5, 40, 15));

        // Content should be inside the window (accounting for border and title bar)
        // X: 10 + 1 = 11 (inside left border)
        // Y: 5 + 2 = 7 (below top border and title bar)
        Assert.Equal(11, content.Bounds.X);
        Assert.Equal(7, content.Bounds.Y);
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void GetFocusableNodes_ReturnsContentFocusables()
    {
        var button = new ButtonNode { Label = "Click Me" };
        var content = new VStackNode { Children = [button] };
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", () => new TextBlockWidget("Hello"));
        
        var node = new WindowNode { Entry = entry, Content = content };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Same(button, focusables[0]);
    }

    [Fact]
    public void GetFocusableNodes_WithNoContent_ReturnsEmpty()
    {
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", () => new TextBlockWidget("Hello"));
        
        var node = new WindowNode { Entry = entry };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    #endregion

    #region ClipRect Tests

    [Fact]
    public void ClipRect_ReturnsInnerContentArea()
    {
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", () => new TextBlockWidget("Hello"), width: 40, height: 15);
        
        var node = new WindowNode { Entry = entry };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(10, 5, 40, 15));

        var clipRect = node.ClipRect;

        // ClipRect should be the inner content area
        // X: 10 + 1 = 11
        // Y: 5 + 2 = 7 (accounting for border and title bar)
        // Width: 40 - 2 = 38
        // Height: 15 - 3 = 12 (accounting for borders and title bar)
        Assert.Equal(11, clipRect.X);
        Assert.Equal(7, clipRect.Y);
        Assert.Equal(38, clipRect.Width);
        Assert.Equal(12, clipRect.Height);
    }

    #endregion

    #region GetChildren Tests

    [Fact]
    public void GetChildren_ReturnsContent()
    {
        var content = new TextBlockNode { Text = "Hello" };
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", () => new TextBlockWidget("Hello"));
        
        var node = new WindowNode { Entry = entry, Content = content };

        var children = node.GetChildren().ToList();

        Assert.Single(children);
        Assert.Same(content, children[0]);
    }

    [Fact]
    public void GetChildren_WithNoContent_ReturnsEmpty()
    {
        var manager = new WindowManager();
        var entry = manager.Open("test", "Test", () => new TextBlockWidget("Hello"));
        
        var node = new WindowNode { Entry = entry };

        var children = node.GetChildren().ToList();

        Assert.Empty(children);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void IsActive_DefaultsFalse()
    {
        var node = new WindowNode();

        Assert.False(node.IsActive);
    }

    [Fact]
    public void IsModal_DefaultsFalse()
    {
        var node = new WindowNode();

        Assert.False(node.IsModal);
    }

    [Fact]
    public void IsResizable_DefaultsFalse()
    {
        var node = new WindowNode();

        Assert.False(node.IsResizable);
    }

    #endregion

    #region SyncFocusIndex Tests

    [Fact]
    public void SyncFocusIndex_BringsWindowToFront_WhenChildIsFocused()
    {
        var manager = new WindowManager();
        var entry1 = manager.Open("win1", "Window 1", () => new TextBlockWidget("1"));
        var entry2 = manager.Open("win2", "Window 2", () => new TextBlockWidget("2"));

        // Set up window nodes with focusable content
        var button1 = new ButtonNode { Label = "Button 1" };
        var content1 = new VStackNode { Children = [button1] };
        content1.Parent = new WindowNode { Entry = entry1, Content = content1 };
        var node1 = (WindowNode)content1.Parent;
        node1.Content = content1;

        var button2 = new ButtonNode { Label = "Button 2" };
        var content2 = new VStackNode { Children = [button2] };
        content2.Parent = new WindowNode { Entry = entry2, Content = content2 };
        var node2 = (WindowNode)content2.Parent;
        node2.Content = content2;

        // Initially, window 2 should have higher z-index
        Assert.True(entry2.ZIndex > entry1.ZIndex);

        // Simulate focusing a child of window 1 - this triggers SyncFocusIndex
        button1.IsFocused = true;
        node1.SyncFocusIndex();

        // Now window 1 should have higher z-index
        Assert.True(entry1.ZIndex > entry2.ZIndex);
    }

    [Fact]
    public void SyncFocusIndex_DoesNothing_WhenNoChildIsFocused()
    {
        var manager = new WindowManager();
        var entry1 = manager.Open("win1", "Window 1", () => new TextBlockWidget("1"));
        var entry2 = manager.Open("win2", "Window 2", () => new TextBlockWidget("2"));

        var button = new ButtonNode { Label = "Button" };
        var content = new VStackNode { Children = [button] };
        var node = new WindowNode { Entry = entry1, Content = content };

        var initialZ1 = entry1.ZIndex;
        var initialZ2 = entry2.ZIndex;

        // SyncFocusIndex with no focused child should not change z-order
        node.SyncFocusIndex();

        Assert.Equal(initialZ1, entry1.ZIndex);
        Assert.Equal(initialZ2, entry2.ZIndex);
    }

    #endregion
}
