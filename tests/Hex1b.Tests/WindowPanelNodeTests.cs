using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for WindowPanelNode layout and window management.
/// </summary>
public class WindowPanelNodeTests
{
    #region Basic Tests

    [Fact]
    public void Constructor_InitializesWindowManager()
    {
        var node = new WindowPanelNode();

        Assert.NotNull(node.Windows);
        Assert.Equal(0, node.Windows.Count);
    }

    [Fact]
    public void Constructor_InitializesEmptyWindowNodes()
    {
        var node = new WindowPanelNode();

        Assert.NotNull(node.WindowNodes);
        Assert.Empty(node.WindowNodes);
    }

    #endregion

    #region Measurement Tests

    [Fact]
    public void Measure_WithContent_ReturnsContentSize()
    {
        var content = new TextBlockNode { Text = "Hello World" };
        var node = new WindowPanelNode { Content = content };

        var size = node.Measure(new Constraints(0, 80, 0, 24));

        // TextBlockNode measures as 11x1
        Assert.Equal(11, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_WithNoContent_ReturnsZero()
    {
        var node = new WindowPanelNode();

        var size = node.Measure(new Constraints(0, 80, 0, 24));

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    #endregion

    #region Arrangement Tests

    [Fact]
    public void Arrange_PositionsContentAtBounds()
    {
        var content = new TextBlockNode { Text = "Hello" };
        var node = new WindowPanelNode { Content = content };
        node.Measure(new Constraints(0, 80, 0, 24));

        node.Arrange(new Rect(0, 0, 80, 24));

        Assert.Equal(0, content.Bounds.X);
        Assert.Equal(0, content.Bounds.Y);
    }

    [Fact]
    public void Arrange_CentersWindowsWithNoPosition()
    {
        var node = new WindowPanelNode();
        var entry = node.Windows.Open("test", "Test", () => new TextBlockWidget("Hello"), width: 40, height: 15);
        
        // Add window node manually for testing (normally done via reconciliation)
        var windowNode = new WindowNode { Entry = entry };
        windowNode.Measure(Constraints.Unbounded);
        node.WindowNodes.Add(windowNode);

        node.Arrange(new Rect(0, 0, 80, 24));

        // Window should be centered: (80-40)/2 = 20, (24-15)/2 = 4
        Assert.Equal(20, windowNode.Bounds.X);
        Assert.Equal(4, windowNode.Bounds.Y);
    }

    [Fact]
    public void Arrange_UsesSpecifiedPosition()
    {
        var node = new WindowPanelNode();
        var entry = node.Windows.Open("test", "Test", () => new TextBlockWidget("Hello"), 
            width: 30, height: 10, x: 5, y: 3);
        
        var windowNode = new WindowNode { Entry = entry };
        windowNode.Measure(Constraints.Unbounded);
        node.WindowNodes.Add(windowNode);

        node.Arrange(new Rect(0, 0, 80, 24));

        Assert.Equal(5, windowNode.Bounds.X);
        Assert.Equal(3, windowNode.Bounds.Y);
    }

    [Fact]
    public void Arrange_ClampsWindowToPanelBounds()
    {
        var node = new WindowPanelNode();
        // Position window outside bounds
        var entry = node.Windows.Open("test", "Test", () => new TextBlockWidget("Hello"),
            width: 30, height: 10, x: 100, y: 50);
        
        var windowNode = new WindowNode { Entry = entry };
        windowNode.Measure(Constraints.Unbounded);
        node.WindowNodes.Add(windowNode);

        node.Arrange(new Rect(0, 0, 80, 24));

        // Should be clamped to fit within bounds
        // X should be max 80-30 = 50
        // Y should be max 24-10 = 14
        Assert.True(windowNode.Bounds.X <= 50);
        Assert.True(windowNode.Bounds.Y <= 14);
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void GetFocusableNodes_WithNoWindows_ReturnsContentFocusables()
    {
        var button = new ButtonNode { Label = "Click" };
        var content = new VStackNode { Children = [button] };
        var node = new WindowPanelNode { Content = content };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Same(button, focusables[0]);
    }

    [Fact]
    public void GetFocusableNodes_WithModalWindow_OnlyReturnsModalFocusables()
    {
        var contentButton = new ButtonNode { Label = "Content Button" };
        var content = new VStackNode { Children = [contentButton] };
        var node = new WindowPanelNode { Content = content };

        // Add a modal window with a button
        var windowButton = new ButtonNode { Label = "Window Button" };
        var windowContent = new VStackNode { Children = [windowButton] };
        var entry = node.Windows.Open("modal", "Modal", () => new TextBlockWidget("Hello"), isModal: true);
        var windowNode = new WindowNode { Entry = entry, Content = windowContent, IsModal = true };
        node.WindowNodes.Add(windowNode);

        var focusables = node.GetFocusableNodes().ToList();

        // Should contain modal window node and its button only
        Assert.Equal(2, focusables.Count);
        Assert.Contains(windowNode, focusables);
        Assert.Contains(windowButton, focusables);
    }

    [Fact]
    public void GetFocusableNodes_WithNonModalWindows_ReturnsAllFocusables()
    {
        var contentButton = new ButtonNode { Label = "Content Button" };
        var content = new VStackNode { Children = [contentButton] };
        var node = new WindowPanelNode { Content = content };

        // Add a non-modal window with a button
        var windowButton = new ButtonNode { Label = "Window Button" };
        var windowContent = new VStackNode { Children = [windowButton] };
        var entry = node.Windows.Open("win", "Window", () => new TextBlockWidget("Hello"));
        var windowNode = new WindowNode { Entry = entry, Content = windowContent };
        node.WindowNodes.Add(windowNode);

        var focusables = node.GetFocusableNodes().ToList();

        // Should contain content button, window node itself, and window button
        Assert.Equal(3, focusables.Count);
        Assert.Contains(contentButton, focusables);
        Assert.Contains(windowNode, focusables);
        Assert.Contains(windowButton, focusables);
    }

    #endregion

    #region GetChildren Tests

    [Fact]
    public void GetChildren_ReturnsContentAndWindows()
    {
        var content = new TextBlockNode { Text = "Content" };
        var node = new WindowPanelNode { Content = content };
        
        var entry = node.Windows.Open("win", "Window", () => new TextBlockWidget("Hello"));
        var windowNode = new WindowNode { Entry = entry };
        node.WindowNodes.Add(windowNode);

        var children = node.GetChildren().ToList();

        Assert.Equal(2, children.Count);
        Assert.Same(content, children[0]);
        Assert.Same(windowNode, children[1]);
    }

    [Fact]
    public void GetChildren_WithNoContent_ReturnsOnlyWindows()
    {
        var node = new WindowPanelNode();
        
        var entry = node.Windows.Open("win", "Window", () => new TextBlockWidget("Hello"));
        var windowNode = new WindowNode { Entry = entry };
        node.WindowNodes.Add(windowNode);

        var children = node.GetChildren().ToList();

        Assert.Single(children);
        Assert.Same(windowNode, children[0]);
    }

    #endregion

    #region IWindowHost Tests

    [Fact]
    public void ImplementsIWindowHost()
    {
        var node = new WindowPanelNode();

        Assert.IsAssignableFrom<IWindowHost>(node);
    }

    [Fact]
    public void Windows_IsSameAsWindowManager()
    {
        var node = new WindowPanelNode();
        var host = (IWindowHost)node;

        Assert.Same(node.Windows, host.Windows);
    }

    #endregion
}
