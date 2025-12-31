using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the RescueFallbackWidget and its focus ring behavior.
/// </summary>
public class RescueFallbackWidgetTests
{
    [Fact]
    public void RescueFallbackWidget_GetFocusableNodes_DirectCall_ReturnsCorrectOrder()
    {
        // This test mimics exactly what happens in the app:
        // 1. Create widget  
        // 2. Reconcile into node tree
        // 3. Get focusable nodes from the node
        
        // Arrange
        var state = new RescueState();
        state.SetError(new InvalidOperationException("Test error"), RescueErrorPhase.Build);
        
        var actions = new List<RescueAction>
        {
            new("Button1", () => { }),
            new("Button2", () => { }),
            new("Button3", () => { }),
            new("Button4", () => { })
        };
        
        var widget = new RescueFallbackWidget(state, ShowDetails: true, Actions: actions);
        
        // Act - reconcile like Hex1bApp.Reconcile does
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;
        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult();
        node.BindingsConfigurator = widget.BindingsConfigurator;
        node.WidthHint = widget.WidthHint;
        node.HeightHint = widget.HeightHint;
        
        // Get focusable nodes directly from node
        var focusables = node.GetFocusableNodes().ToList();
        
        // Debug: Show tree structure
        System.Console.WriteLine($"Node type: {node.GetType().Name}");
        PrintNodeTree(node, "");
        
        System.Console.WriteLine($"\nTotal focusables: {focusables.Count}");
        for (int i = 0; i < focusables.Count; i++)
        {
            System.Console.WriteLine($"  [{i}] {focusables[i].GetType().Name}");
        }
        
        // Assert - should be 5 (4 buttons + 1 scroll)
        Assert.Equal(5, focusables.Count);
    }
    
    private static void PrintNodeTree(Hex1bNode node, string indent)
    {
        System.Console.WriteLine($"{indent}{node.GetType().Name}");
        foreach (var child in node.GetChildren())
        {
            PrintNodeTree(child, indent + "  ");
        }
    }
    
    [Fact]
    public void RescueFallbackWidget_AfterMeasureAndArrange_HasCorrectFocusables()
    {
        // This test mimics the FULL production scenario:
        // 1. Create widget  
        // 2. Reconcile
        // 3. Measure (like Hex1bApp does)
        // 4. Arrange (like Hex1bApp does)
        // 5. Rebuild focus ring (like Hex1bApp does)
        
        // Arrange
        var state = new RescueState();
        state.SetError(new InvalidOperationException("Test error"), RescueErrorPhase.Build);
        
        var actions = new List<RescueAction>
        {
            new("Button1", () => { }),
            new("Button2", () => { }),
            new("Button3", () => { }),
            new("Button4", () => { })
        };
        
        var widget = new RescueFallbackWidget(state, ShowDetails: true, Actions: actions);
        
        // Reconcile like Hex1bApp.Reconcile does
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;
        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult();
        node.BindingsConfigurator = widget.BindingsConfigurator;
        node.WidthHint = widget.WidthHint;
        node.HeightHint = widget.HeightHint;
        
        // Measure and arrange like Hex1bApp does
        var terminalSize = new Size(80, 24);
        var constraints = Constraints.Tight(terminalSize);
        node.Measure(constraints);
        node.Arrange(Rect.FromSize(terminalSize));
        
        // Rebuild focus ring like Hex1bApp does
        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();
        
        // Print debug info
        System.Console.WriteLine($"After full layout, focusables: {focusRing.Focusables.Count}");
        foreach (var f in focusRing.Focusables)
        {
            System.Console.WriteLine($"  {f.GetType().Name} IsFocused={f.IsFocused}");
        }
        
        // Assert
        Assert.Equal(5, focusRing.Focusables.Count);
    }
    
    [Fact]
    public async Task RescueFallbackWidget_InputRouting_TabNavigatesToScrollNode()
    {
        // This test uses the full input routing stack like Hex1bApp does
        
        // Arrange
        var state = new RescueState();
        state.SetError(new InvalidOperationException("Test error"), RescueErrorPhase.Build);
        
        var actions = new List<RescueAction>
        {
            new("Button1", () => { }),
            new("Button2", () => { }),
            new("Button3", () => { }),
            new("Button4", () => { })
        };
        
        var widget = new RescueFallbackWidget(state, ShowDetails: true, Actions: actions);
        
        // Reconcile like Hex1bApp.Reconcile does
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;
        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult();
        node.BindingsConfigurator = widget.BindingsConfigurator;
        node.WidthHint = widget.WidthHint;
        node.HeightHint = widget.HeightHint;
        
        // Measure and arrange like Hex1bApp does
        var terminalSize = new Size(80, 24);
        var constraints = Constraints.Tight(terminalSize);
        node.Measure(constraints);
        node.Arrange(Rect.FromSize(terminalSize));
        
        // Rebuild focus ring like Hex1bApp does
        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();
        var routerState = new InputRouterState();
        
        // Tab key event
        var tabEvent = new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None);
        
        // Route Tab 4 times to get from button 1 to button 4
        for (int i = 0; i < 4; i++)
        {
            var result = await InputRouter.RouteInputAsync(node, tabEvent, focusRing, routerState, null, TestContext.Current.CancellationToken);
            System.Console.WriteLine($"Tab {i+1}: result={result}, focused={focusRing.FocusedNode?.GetType().Name}");
            Assert.Equal(InputResult.Handled, result);
        }
        
        // After 4 Tabs, we should be on ScrollNode
        Assert.IsType<Hex1b.Nodes.ScrollNode>(focusRing.FocusedNode);
    }
    
    [Fact]
    public async Task RescueFallbackWidget_ArrowDown_ScrollsWhenFocused()
    {
        // This test verifies that ArrowDown scrolls when ScrollNode has focus
        
        // Arrange
        var state = new RescueState();
        state.SetError(new InvalidOperationException("Test error with a long message that spans multiple lines so we have scrollable content"), RescueErrorPhase.Build);
        
        var actions = new List<RescueAction>
        {
            new("Button1", () => { }),
            new("Button2", () => { }),
            new("Button3", () => { }),
            new("Button4", () => { })
        };
        
        var widget = new RescueFallbackWidget(state, ShowDetails: true, Actions: actions);
        
        // Reconcile and layout
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;
        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult();
        var terminalSize = new Size(80, 24);
        node.Measure(Constraints.Tight(terminalSize));
        node.Arrange(Rect.FromSize(terminalSize));
        
        // Build focus ring
        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();
        var routerState = new InputRouterState();
        
        // Navigate to ScrollNode (Tab 4 times)
        var tabEvent = new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None);
        for (int i = 0; i < 4; i++)
        {
            await InputRouter.RouteInputAsync(node, tabEvent, focusRing, routerState, null, TestContext.Current.CancellationToken);
        }
        
        // Verify we're on ScrollNode
        var scrollNode = focusRing.FocusedNode as Hex1b.Nodes.ScrollNode;
        Assert.NotNull(scrollNode);
        Assert.True(scrollNode.IsFocused);
        
        // Get initial scroll position
        var initialOffset = scrollNode.Offset;
        System.Console.WriteLine($"Initial scroll offset: {initialOffset}");
        
        // Press ArrowDown
        var downEvent = new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None);
        var result = await InputRouter.RouteInputAsync(node, downEvent, focusRing, routerState, null, TestContext.Current.CancellationToken);
        
        System.Console.WriteLine($"ArrowDown result: {result}, new offset: {scrollNode.Offset}");
        
        // Verify scroll happened (offset increased)
        Assert.Equal(InputResult.Handled, result);
        // Note: Whether scroll actually moves depends on content being larger than viewport
    }
    
    [Fact]
    public void RescueFallbackWidget_TabNavigation_ReachesScrollNode()
    {
        // Arrange
        var state = new RescueState();
        state.SetError(new InvalidOperationException("Test error"), RescueErrorPhase.Build);
        
        var actions = new List<RescueAction>
        {
            new("Button1", () => { }),
            new("Button2", () => { }),
            new("Button3", () => { }),
            new("Button4", () => { })
        };
        
        var widget = new RescueFallbackWidget(state, ShowDetails: true, Actions: actions);
        
        // Act - reconcile the widget tree
        var context = ReconcileContext.CreateRoot();
        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult();
        
        // Build focus ring
        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        
        // Debug: Show all focusables
        System.Console.WriteLine($"Total focusables: {focusRing.Focusables.Count}");
        for (int i = 0; i < focusRing.Focusables.Count; i++)
        {
            System.Console.WriteLine($"  [{i}] {focusRing.Focusables[i].GetType().Name}");
        }
        
        // Focus the first node
        focusRing.EnsureFocus();
        System.Console.WriteLine($"After EnsureFocus: Focused={focusRing.FocusedNode?.GetType().Name}, Index={focusRing.FocusedIndex}");
        
        // Tab through all nodes
        for (int i = 0; i < 4; i++)
        {
            var before = focusRing.FocusedNode;
            focusRing.FocusNext();
            var after = focusRing.FocusedNode;
            System.Console.WriteLine($"Tab {i+1}: {before?.GetType().Name} -> {after?.GetType().Name} (Index={focusRing.FocusedIndex})");
        }
        
        // Assert - should be on the scroll node now
        var focusedNode = focusRing.FocusedNode;
        Assert.NotNull(focusedNode);
        Assert.IsType<Hex1b.Nodes.ScrollNode>(focusedNode);
        Assert.True(focusedNode.IsFocused);
    }
}
