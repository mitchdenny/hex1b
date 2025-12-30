using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal.Automation;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class RescueNodeTests
{
    #region RescueState Tests
    
    [Fact]
    public void RescueState_InitialState_HasNoError()
    {
        var state = new RescueState();
        
        Assert.False(state.HasError);
        Assert.Null(state.Exception);
        Assert.Equal(RescueErrorPhase.None, state.ErrorPhase);
    }
    
    [Fact]
    public void RescueState_SetError_CapturesAllDetails()
    {
        var state = new RescueState();
        var exception = new InvalidOperationException("Test exception");
        
        state.SetError(exception, RescueErrorPhase.Render);
        
        Assert.True(state.HasError);
        Assert.Same(exception, state.Exception);
        Assert.Equal(RescueErrorPhase.Render, state.ErrorPhase);
    }
    
    [Fact]
    public void RescueState_Reset_ClearsError()
    {
        var state = new RescueState();
        state.SetError(new Exception("Test"), RescueErrorPhase.Measure);
        
        state.Reset();
        
        Assert.False(state.HasError);
        Assert.Null(state.Exception);
        Assert.Equal(RescueErrorPhase.None, state.ErrorPhase);
    }
    
    #endregion
    
    #region RescueNode Basic Tests
    
    [Fact]
    public void RescueNode_NoError_MeasuresChild()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = new RescueNode { Child = child };
        
        var size = node.Measure(new Constraints(0, 100, 0, 50));
        
        Assert.Equal(5, size.Width); // "Hello" is 5 characters
        Assert.Equal(1, size.Height);
    }
    
    [Fact]
    public void RescueNode_NoError_ArrangesChild()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = new RescueNode { Child = child };
        
        node.Measure(new Constraints(0, 100, 0, 50));
        node.Arrange(new Rect(10, 20, 100, 50));
        
        Assert.Equal(10, child.Bounds.X);
        Assert.Equal(20, child.Bounds.Y);
    }
    
    [Fact]
    public void RescueNode_NoError_RendersChild()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        var child = new TextBlockNode { Text = "Hello" };
        var node = new RescueNode { Child = child };
        
        node.Measure(new Constraints(0, 100, 0, 50));
        node.Arrange(new Rect(0, 0, 100, 50));
        node.Render(context);
        
        Assert.True(terminal.CreateSnapshot().ContainsText("Hello"));
    }
    
    #endregion
    
    #region Exception Catching Tests
    
    [Fact]
    public void RescueNode_MeasureThrows_CapturesError()
    {
        var state = new RescueState();
        var throwingChild = new ThrowingNode { ThrowOnMeasure = true };
        var node = new RescueNode 
        { 
            Child = throwingChild,
            State = state,
            ShowDetails = true
        };
        
        // This should not throw - the error should be captured
        node.Measure(new Constraints(0, 100, 0, 50));
        
        Assert.True(state.HasError);
        Assert.NotNull(state.Exception);
        Assert.Equal(RescueErrorPhase.Measure, state.ErrorPhase);
    }
    
    [Fact]
    public void RescueNode_ArrangeThrows_CapturesError()
    {
        var state = new RescueState();
        var throwingChild = new ThrowingNode { ThrowOnArrange = true };
        var node = new RescueNode 
        { 
            Child = throwingChild,
            State = state,
            ShowDetails = true
        };
        
        node.Measure(new Constraints(0, 100, 0, 50));
        
        // This should not throw - the error should be captured
        node.Arrange(new Rect(0, 0, 100, 50));
        
        Assert.True(state.HasError);
        Assert.Equal(RescueErrorPhase.Arrange, state.ErrorPhase);
    }
    
    [Fact]
    public void RescueNode_RenderThrows_CapturesError()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        var state = new RescueState();
        var throwingChild = new ThrowingNode { ThrowOnRender = true };
        var node = new RescueNode 
        { 
            Child = throwingChild,
            State = state,
            ShowDetails = true
        };
        
        node.Measure(new Constraints(0, 100, 0, 50));
        node.Arrange(new Rect(0, 0, 100, 50));
        
        // This should not throw - the error should be captured
        node.Render(context);
        
        Assert.True(state.HasError);
        Assert.Equal(RescueErrorPhase.Render, state.ErrorPhase);
    }
    
    #endregion
    
    #region Fallback Rendering Tests
    
    [Fact]
    public void RescueNode_AfterError_RendersFallback()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        var state = new RescueState();
        var throwingChild = new ThrowingNode { ThrowOnMeasure = true };
        var node = new RescueNode 
        { 
            Child = throwingChild,
            State = state,
            ShowDetails = true
        };
        
        node.Measure(new Constraints(0, 100, 0, 50));
        node.Arrange(new Rect(0, 0, 100, 50));
        node.Render(context);
        
        // Should show error message (new RescueFallbackWidget uses "UNHANDLED EXCEPTION")
        Assert.True(terminal.CreateSnapshot().ContainsText("UNHANDLED EXCEPTION"));
    }
    
    [Fact]
    public void RescueNode_CustomFallback_UsesCustomBuilder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        var state = new RescueState();
        var throwingChild = new ThrowingNode { ThrowOnMeasure = true };
        
        Hex1bWidget CustomFallback(RescueState s) => 
            new TextBlockWidget($"Custom error: {s.Exception?.Message}");
        
        var node = new RescueNode 
        { 
            Child = throwingChild,
            State = state,
            FallbackBuilder = CustomFallback
        };
        
        node.Measure(new Constraints(0, 100, 0, 50));
        node.Arrange(new Rect(0, 0, 100, 50));
        node.Render(context);
        
        // Should show custom error message
        Assert.True(terminal.CreateSnapshot().ContainsText("Custom error: Test exception"));
    }
    
    [Fact]
    public void RescueNode_ShowDetailsTrue_IncludesStackTrace()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        var state = new RescueState();
        var throwingChild = new ThrowingNode { ThrowOnMeasure = true };
        var node = new RescueNode 
        { 
            Child = throwingChild,
            State = state,
            ShowDetails = true
        };
        
        node.Measure(new Constraints(0, 100, 0, 50));
        node.Arrange(new Rect(0, 0, 100, 50));
        node.Render(context);
        
        // Should show stack trace in details mode
        Assert.True(terminal.CreateSnapshot().ContainsText("Stack Trace"));
    }
    
    [Fact]
    public void RescueNode_ShowDetailsFalse_ShowsFriendlyMessage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        var state = new RescueState();
        var throwingChild = new ThrowingNode { ThrowOnMeasure = true };
        var node = new RescueNode 
        { 
            Child = throwingChild,
            State = state,
            ShowDetails = false
        };
        
        node.Measure(new Constraints(0, 100, 0, 50));
        node.Arrange(new Rect(0, 0, 100, 50));
        node.Render(context);
        
        // Should show friendly message without details
        Assert.True(terminal.CreateSnapshot().ContainsText("Something went wrong"));
        Assert.False(terminal.CreateSnapshot().ContainsText("Stack Trace"));
    }
    
    #endregion
    
    #region Focus Navigation Tests
    
    [Fact]
    public void RescueNode_NoError_GetsFocusableNodesFromChild()
    {
        var button = new ButtonNode { Label = "Click me" };
        var node = new RescueNode { Child = button };
        
        var focusable = node.GetFocusableNodes().ToList();
        
        Assert.Single(focusable);
        Assert.Same(button, focusable[0]);
    }
    
    [Fact]
    public void RescueNode_AfterError_ReturnsEmptyFocusableNodes()
    {
        var state = new RescueState();
        var button = new ButtonNode { Label = "Click me" };
        var node = new RescueNode 
        { 
            Child = button,
            State = state
        };
        
        // Simulate an error
        state.SetError(new Exception("Test"), RescueErrorPhase.Render);
        
        // No fallback child is set, so no focusable nodes
        var focusable = node.GetFocusableNodes().ToList();
        
        Assert.Empty(focusable);
    }
    
    #endregion
    
    #region RescueWidget Tests
    
    [Fact]
    public void RescueWidget_Reconcile_CreatesRescueNode()
    {
        var childWidget = new TextBlockWidget("Test");
        var widget = new RescueWidget(childWidget);
        
        var context = ReconcileContext.CreateRoot();
        var node = widget.Reconcile(null, context);
        
        Assert.IsType<RescueNode>(node);
    }
    
    [Fact]
    public void RescueWidget_Reconcile_ReusesExistingNode()
    {
        var childWidget = new TextBlockWidget("Test");
        var widget = new RescueWidget(childWidget);
        
        var context = ReconcileContext.CreateRoot();
        var node1 = widget.Reconcile(null, context) as RescueNode;
        var node2 = widget.Reconcile(node1, context) as RescueNode;
        
        Assert.Same(node1, node2);
    }
    
    [Fact]
    public void RescueWidget_Reconcile_SetsStateOnNode()
    {
        var state = new RescueState();
        var childWidget = new TextBlockWidget("Test");
        var widget = new RescueWidget(childWidget, state);
        
        var context = ReconcileContext.CreateRoot();
        var node = widget.Reconcile(null, context) as RescueNode;
        
        Assert.Same(state, node?.State);
    }
    
    [Fact]
    public void RescueWidget_WhenInErrorState_ReconcilesFallbackInstead()
    {
        var state = new RescueState();
        state.SetError(new Exception("Test"), RescueErrorPhase.Build);
        
        var childWidget = new TextBlockWidget("Test");
        var widget = new RescueWidget(childWidget, state);
        
        var context = ReconcileContext.CreateRoot();
        var node = widget.Reconcile(null, context) as RescueNode;
        
        // Should have fallback child, not main child
        Assert.NotNull(node?.FallbackChild);
    }
    
    [Fact]
    public void RescueWidget_GetExpectedNodeType_ReturnsRescueNode()
    {
        var widget = new RescueWidget(new TextBlockWidget("Test"));
        
        Assert.Equal(typeof(RescueNode), widget.GetExpectedNodeType());
    }
    
    #endregion
    
    #region Integration Tests
    
    [Fact]
    public void RescueNode_AfterReset_ResumesNormalRendering()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        var state = new RescueState();
        var throwOnce = new ThrowOnceNode();
        var node = new RescueNode 
        { 
            Child = throwOnce,
            State = state,
            ShowDetails = true
        };
        
        // First measure throws
        node.Measure(new Constraints(0, 100, 0, 50));
        Assert.True(state.HasError);
        
        // Reset and try again
        state.Reset();
        throwOnce.HasThrown = false; // Reset the throwing node too
        throwOnce.ShouldThrow = false; // Don't throw this time
        
        // Need to re-set the child because fallback replaced it
        node.Measure(new Constraints(0, 100, 0, 50));
        
        // State should still be clear after successful operation
        Assert.False(state.HasError);
    }
    
    [Fact]
    public void RescueWidget_ChainedWithOtherWidgets_WorksCorrectly()
    {
        var state = new RescueState();
        var innerWidget = new TextBlockWidget("Content");
        
        var rescueWidget = new RescueWidget(innerWidget, state);
        var borderedWidget = new BorderWidget(rescueWidget);
        
        var context = ReconcileContext.CreateRoot();
        var node = borderedWidget.Reconcile(null, context) as BorderNode;
        
        Assert.NotNull(node);
        Assert.IsType<RescueNode>(node?.Child);
    }
    
    #endregion
    
    #region Default Fallback Generation Tests
    
    [Fact]
    public void BuildDefaultFallback_WithDetails_IncludesExceptionInfo()
    {
        var state = new RescueState();
        state.SetError(new ArgumentException("Invalid argument"), RescueErrorPhase.Measure);
        
        var fallback = RescueNode.BuildDefaultFallback(state, showDetails: true);
        
        // Now returns RescueFallbackWidget with hardcoded styling
        Assert.IsType<RescueFallbackWidget>(fallback);
        var rescueFallback = (RescueFallbackWidget)fallback;
        Assert.Same(state, rescueFallback.State);
        Assert.True(rescueFallback.ShowDetails);
    }
    
    [Fact]
    public void BuildDefaultFallback_WithoutDetails_ShowsGenericMessage()
    {
        var state = new RescueState();
        state.SetError(new ArgumentException("Invalid argument"), RescueErrorPhase.Measure);
        
        var fallback = RescueNode.BuildDefaultFallback(state, showDetails: false);
        
        // Now returns RescueFallbackWidget with hardcoded styling
        Assert.IsType<RescueFallbackWidget>(fallback);
        var rescueFallback = (RescueFallbackWidget)fallback;
        Assert.Same(state, rescueFallback.State);
        Assert.False(rescueFallback.ShowDetails);
    }
    
    #endregion
    
    #region Helper Classes
    
    /// <summary>
    /// A node that can be configured to throw on specific lifecycle methods.
    /// </summary>
    private class ThrowingNode : Hex1bNode
    {
        public bool ThrowOnMeasure { get; set; }
        public bool ThrowOnArrange { get; set; }
        public bool ThrowOnRender { get; set; }
        
        public override Size Measure(Constraints constraints)
        {
            if (ThrowOnMeasure)
            {
                throw new InvalidOperationException("Test exception");
            }
            return new Size(10, 1);
        }
        
        public override void Arrange(Rect bounds)
        {
            base.Arrange(bounds);
            if (ThrowOnArrange)
            {
                throw new InvalidOperationException("Test exception");
            }
        }
        
        public override void Render(Hex1bRenderContext context)
        {
            if (ThrowOnRender)
            {
                throw new InvalidOperationException("Test exception");
            }
        }
    }
    
    /// <summary>
    /// A node that throws only once, then succeeds.
    /// </summary>
    private class ThrowOnceNode : Hex1bNode
    {
        public bool ShouldThrow { get; set; } = true;
        public bool HasThrown { get; set; }
        
        public override Size Measure(Constraints constraints)
        {
            if (ShouldThrow && !HasThrown)
            {
                HasThrown = true;
                throw new InvalidOperationException("First time exception");
            }
            return new Size(10, 1);
        }
        
        public override void Render(Hex1bRenderContext context)
        {
            context.Write("Success");
        }
    }
    
    #endregion
}
