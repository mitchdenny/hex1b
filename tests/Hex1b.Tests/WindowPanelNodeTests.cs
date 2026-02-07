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
    public void Measure_FillsAvailableSpace()
    {
        var node = new WindowPanelNode();

        var size = node.Measure(new Constraints(0, 80, 0, 24));

        // WindowPanel fills available space
        Assert.Equal(80, size.Width);
        Assert.Equal(24, size.Height);
    }

    #endregion

    #region Arrangement Tests

    [Fact]
    public void Arrange_SetsBoundsOnNode()
    {
        var node = new WindowPanelNode();
        node.Measure(new Constraints(0, 80, 0, 24));

        node.Arrange(new Rect(0, 0, 80, 24));

        Assert.Equal(0, node.Bounds.X);
        Assert.Equal(0, node.Bounds.Y);
        Assert.Equal(80, node.Bounds.Width);
        Assert.Equal(24, node.Bounds.Height);
    }

    [Fact]
    public void Arrange_CentersWindowsWithNoPosition()
    {
        var node = new WindowPanelNode();
        var handle = node.Windows.Window(_ => new TextBlockWidget("Hello"))
            .Title("Test")
            .Size(40, 15);
        var entry = node.Windows.Open(handle);
        
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
        var handle = node.Windows.Window(_ => new TextBlockWidget("Hello"))
            .Title("Test")
            .Size(30, 10)
            .Position(5, 3);
        var entry = node.Windows.Open(handle);
        
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
        var handle = node.Windows.Window(_ => new TextBlockWidget("Hello"))
            .Title("Test")
            .Size(30, 10)
            .Position(100, 50);
        var entry = node.Windows.Open(handle);
        
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
    public void GetFocusableNodes_WithNoWindows_ReturnsEmpty()
    {
        var node = new WindowPanelNode();

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    [Fact]
    public void GetFocusableNodes_WithModalWindow_OnlyReturnsModalFocusables()
    {
        var node = new WindowPanelNode();

        // Add a modal window with a button
        var windowButton = new ButtonNode { Label = "Window Button" };
        var windowContent = new VStackNode { Children = [windowButton] };
        var handle = node.Windows.Window(_ => new TextBlockWidget("Hello"))
            .Title("Modal")
            .Modal();
        var entry = node.Windows.Open(handle);
        var windowNode = new WindowNode { Entry = entry, Content = windowContent, IsModal = true };
        node.WindowNodes.Add(windowNode);

        var focusables = node.GetFocusableNodes().ToList();

        // Should contain modal window node and its button only
        Assert.Equal(2, focusables.Count);
        Assert.Contains(windowNode, focusables);
        Assert.Contains(windowButton, focusables);
    }

    [Fact]
    public void GetFocusableNodes_WithNonModalWindows_ReturnsWindowFocusables()
    {
        var node = new WindowPanelNode();

        // Add a non-modal window with a button
        var windowButton = new ButtonNode { Label = "Window Button" };
        var windowContent = new VStackNode { Children = [windowButton] };
        var handle = node.Windows.Window(_ => new TextBlockWidget("Hello"))
            .Title("Window");
        var entry = node.Windows.Open(handle);
        var windowNode = new WindowNode { Entry = entry, Content = windowContent };
        node.WindowNodes.Add(windowNode);

        var focusables = node.GetFocusableNodes().ToList();

        // Should contain window node itself and window button
        Assert.Equal(2, focusables.Count);
        Assert.Contains(windowNode, focusables);
        Assert.Contains(windowButton, focusables);
    }

    #endregion

    #region GetChildren Tests

    [Fact]
    public void GetChildren_ReturnsWindows()
    {
        var node = new WindowPanelNode();
        
        var handle = node.Windows.Window(_ => new TextBlockWidget("Hello"))
            .Title("Window");
        var entry = node.Windows.Open(handle);
        var windowNode = new WindowNode { Entry = entry };
        node.WindowNodes.Add(windowNode);

        var children = node.GetChildren().ToList();

        Assert.Single(children);
        Assert.Same(windowNode, children[0]);
    }

    [Fact]
    public void GetChildren_WithNoContent_ReturnsOnlyWindows()
    {
        var node = new WindowPanelNode();
        
        var handle = node.Windows.Window(_ => new TextBlockWidget("Hello"))
            .Title("Window");
        var entry = node.Windows.Open(handle);
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

    #region Phase 6: Modal Dialog Tests

    [Fact]
    public void GetFocusableNodes_WithNestedModals_ReturnsOnlyTopmostModalFocusables()
    {
        var node = new WindowPanelNode();

        // Add first modal
        var modal1Button = new ButtonNode { Label = "Modal 1 Button" };
        var modal1Content = new VStackNode { Children = [modal1Button] };
        var handle1 = node.Windows.Window(_ => new TextBlockWidget("Hello"))
            .Title("Modal 1")
            .Modal();
        var entry1 = node.Windows.Open(handle1);
        var modal1Node = new WindowNode { Entry = entry1, Content = modal1Content, IsModal = true };
        node.WindowNodes.Add(modal1Node);

        // Add second modal (nested on top of first)
        var modal2Button = new ButtonNode { Label = "Modal 2 Button" };
        var modal2Content = new VStackNode { Children = [modal2Button] };
        var handle2 = node.Windows.Window(_ => new TextBlockWidget("Hello"))
            .Title("Modal 2")
            .Modal();
        var entry2 = node.Windows.Open(handle2);
        var modal2Node = new WindowNode { Entry = entry2, Content = modal2Content, IsModal = true };
        node.WindowNodes.Add(modal2Node);

        var focusables = node.GetFocusableNodes().ToList();

        // Should contain only the topmost (last) modal's focusables
        Assert.Equal(2, focusables.Count);
        Assert.Contains(modal2Node, focusables);
        Assert.Contains(modal2Button, focusables);
        Assert.DoesNotContain(modal1Node, focusables);
        Assert.DoesNotContain(modal1Button, focusables);
    }

    [Fact]
    public void GetFocusableNodes_ClosingTopModal_ExposesModalBelow()
    {
        var node = new WindowPanelNode();

        // Add first modal
        var modal1Button = new ButtonNode { Label = "Modal 1 Button" };
        var modal1Content = new VStackNode { Children = [modal1Button] };
        var handle1 = node.Windows.Window(_ => new TextBlockWidget("Hello"))
            .Title("Modal 1")
            .Modal();
        var entry1 = node.Windows.Open(handle1);
        var modal1Node = new WindowNode { Entry = entry1, Content = modal1Content, IsModal = true };
        node.WindowNodes.Add(modal1Node);

        // Add second modal
        var modal2Button = new ButtonNode { Label = "Modal 2 Button" };
        var modal2Content = new VStackNode { Children = [modal2Button] };
        var handle2 = node.Windows.Window(_ => new TextBlockWidget("Hello"))
            .Title("Modal 2")
            .Modal();
        var entry2 = node.Windows.Open(handle2);
        var modal2Node = new WindowNode { Entry = entry2, Content = modal2Content, IsModal = true };
        node.WindowNodes.Add(modal2Node);

        // Close the top modal
        node.WindowNodes.Remove(modal2Node);
        node.Windows.Close(entry2);

        var focusables = node.GetFocusableNodes().ToList();

        // Now modal 1 should be accessible
        Assert.Equal(2, focusables.Count);
        Assert.Contains(modal1Node, focusables);
        Assert.Contains(modal1Button, focusables);
    }

    #endregion
}
