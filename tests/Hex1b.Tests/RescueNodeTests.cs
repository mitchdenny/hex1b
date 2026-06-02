using Hex1b.Events;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class RescueNodeTests
{
    #region RescueNode Basic Tests

    [TestMethod]
    public async Task RescueNode_NoError_MeasuresChild()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = new RescueNode { Child = child };

        var size = node.Measure(new Constraints(0, 100, 0, 50));

        Assert.AreEqual(5, size.Width); // "Hello" is 5 characters
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public async Task RescueNode_NoError_ArrangesChild()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = new RescueNode { Child = child };

        node.Measure(new Constraints(0, 100, 0, 50));
        node.Arrange(new Rect(10, 20, 100, 50));

        Assert.AreEqual(10, child.Bounds.X);
        Assert.AreEqual(20, child.Bounds.Y);
    }

    [TestMethod]
    public async Task RescueNode_NoError_RendersChild()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        var child = new TextBlockNode { Text = "Hello" };
        var node = new RescueNode { Child = child };

        node.Measure(new Constraints(0, 100, 0, 50));
        node.Arrange(new Rect(0, 0, 100, 50));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(5), "Hello text to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.IsTrue(snapshot.ContainsText("Hello"));
    }

    #endregion

    #region Exception Catching Tests

    [TestMethod]
    public async Task RescueNode_MeasureThrows_CapturesError()
    {
        var throwingChild = new ThrowingNode { ThrowOnMeasure = true };
        var node = new RescueNode
        {
            Child = throwingChild,
            ShowDetails = true
        };

        // This should not throw - the error should be captured
        node.Measure(new Constraints(0, 100, 0, 50));

        Assert.IsTrue(node.HasError);
        Assert.IsNotNull(node.Exception);
        Assert.AreEqual(RescueErrorPhase.Measure, node.ErrorPhase);
    }

    [TestMethod]
    public async Task RescueNode_ArrangeThrows_CapturesError()
    {
        var throwingChild = new ThrowingNode { ThrowOnArrange = true };
        var node = new RescueNode
        {
            Child = throwingChild,
            ShowDetails = true
        };

        node.Measure(new Constraints(0, 100, 0, 50));

        // This should not throw - the error should be captured
        node.Arrange(new Rect(0, 0, 100, 50));

        Assert.IsTrue(node.HasError);
        Assert.AreEqual(RescueErrorPhase.Arrange, node.ErrorPhase);
    }

    [TestMethod]
    public async Task RescueNode_RenderThrows_CapturesError()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        var throwingChild = new ThrowingNode { ThrowOnRender = true };
        var node = new RescueNode
        {
            Child = throwingChild,
            ShowDetails = true
        };

        node.Measure(new Constraints(0, 100, 0, 50));
        node.Arrange(new Rect(0, 0, 100, 50));

        // This should not throw - the error should be captured
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("UNHANDLED EXCEPTION"), TimeSpan.FromSeconds(5), "error fallback to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.IsTrue(node.HasError);
        Assert.AreEqual(RescueErrorPhase.Render, node.ErrorPhase);
    }

    #endregion

    #region Fallback Rendering Tests

    [TestMethod]
    public async Task RescueNode_AfterError_RendersFallback()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        var throwingChild = new ThrowingNode { ThrowOnMeasure = true };
        var node = new RescueNode
        {
            Child = throwingChild,
            ShowDetails = true
        };

        node.Measure(new Constraints(0, 100, 0, 50));
        node.Arrange(new Rect(0, 0, 100, 50));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("UNHANDLED EXCEPTION"), TimeSpan.FromSeconds(5), "UNHANDLED EXCEPTION fallback to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Should show error message
        Assert.IsTrue(snapshot.ContainsText("UNHANDLED EXCEPTION"));
    }

    [TestMethod]
    public async Task RescueNode_CustomFallback_UsesCustomBuilder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        var throwingChild = new ThrowingNode { ThrowOnMeasure = true };

        Hex1bWidget CustomFallback(RescueContext ctx) =>
            new TextBlockWidget($"Custom error: {ctx.Exception?.Message}");

        var node = new RescueNode
        {
            Child = throwingChild,
            FallbackBuilder = CustomFallback
        };

        node.Measure(new Constraints(0, 100, 0, 50));
        node.Arrange(new Rect(0, 0, 100, 50));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Custom error: Test exception"), TimeSpan.FromSeconds(5), "custom fallback message to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Should show custom error message
        Assert.IsTrue(snapshot.ContainsText("Custom error: Test exception"));
    }

    [TestMethod]
    public async Task RescueNode_ShowDetailsTrue_IncludesStackTrace()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        var throwingChild = new ThrowingNode { ThrowOnMeasure = true };
        var node = new RescueNode
        {
            Child = throwingChild,
            ShowDetails = true
        };

        node.Measure(new Constraints(0, 100, 0, 50));
        node.Arrange(new Rect(0, 0, 100, 50));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Stack Trace"), TimeSpan.FromSeconds(5), "Stack Trace details to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Should show stack trace in details mode
        Assert.IsTrue(snapshot.ContainsText("Stack Trace"));
    }

    [TestMethod]
    public async Task RescueNode_ShowDetailsFalse_ShowsFriendlyMessage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        var throwingChild = new ThrowingNode { ThrowOnMeasure = true };
        var node = new RescueNode
        {
            Child = throwingChild,
            ShowDetails = false
        };

        node.Measure(new Constraints(0, 100, 0, 50));
        node.Arrange(new Rect(0, 0, 100, 50));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Something went wrong"), TimeSpan.FromSeconds(5), "friendly error message to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Should show friendly message without details
        Assert.IsTrue(snapshot.ContainsText("Something went wrong"));
        Assert.IsFalse(snapshot.ContainsText("Stack Trace"));
    }

    #endregion

    #region Focus Navigation Tests

    [TestMethod]
    public async Task RescueNode_NoError_GetsFocusableNodesFromChild()
    {
        var button = new ButtonNode { Label = "Click me" };
        var node = new RescueNode { Child = button };

        var focusable = node.GetFocusableNodes().ToList();

        TestSeq.Single(focusable);
        Assert.AreSame(button, focusable[0]);
    }

    [TestMethod]
    public async Task RescueNode_AfterError_ReturnsEmptyFocusableNodes()
    {
        var button = new ButtonNode { Label = "Click me" };
        var node = new RescueNode
        {
            Child = button
        };

        // Trigger an error by directly capturing it
        node.CaptureErrorAsync(new Exception("Test"), RescueErrorPhase.Render).GetAwaiter().GetResult();

        // No fallback child is set, so no focusable nodes
        var focusable = node.GetFocusableNodes().ToList();

        Assert.IsEmpty(focusable);
    }

    #endregion

    #region RescueWidget Tests

    [TestMethod]
    public async Task RescueWidget_Reconcile_CreatesRescueNode()
    {
        var childWidget = new TextBlockWidget("Test");
        var widget = new RescueWidget(childWidget);

        var context = ReconcileContext.CreateRoot();
        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult();

        TestSeq.IsType<RescueNode>(node);
    }

    [TestMethod]
    public async Task RescueWidget_Reconcile_ReusesExistingNode()
    {
        var childWidget = new TextBlockWidget("Test");
        var widget = new RescueWidget(childWidget);

        var context = ReconcileContext.CreateRoot();
        var node1 = widget.ReconcileAsync(null, context).GetAwaiter().GetResult() as RescueNode;
        var node2 = widget.ReconcileAsync(node1, context).GetAwaiter().GetResult() as RescueNode;

        Assert.AreSame(node1, node2);
    }

    [TestMethod]
    public async Task RescueWidget_WhenInErrorState_ReconcilesFallbackInstead()
    {
        var childWidget = new TextBlockWidget("Test");
        var widget = new RescueWidget(childWidget);

        var context = ReconcileContext.CreateRoot();
        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult() as RescueNode;

        // Simulate error on node
        node!.CaptureErrorAsync(new Exception("Test"), RescueErrorPhase.Build).GetAwaiter().GetResult();

        // Reconcile again - should build fallback
        var node2 = widget.ReconcileAsync(node, context).GetAwaiter().GetResult() as RescueNode;

        // Should have fallback child, not main child
        Assert.IsNotNull(node2?.FallbackChild);
    }

    [TestMethod]
    public async Task RescueWidget_GetExpectedNodeType_ReturnsRescueNode()
    {
        var widget = new RescueWidget(new TextBlockWidget("Test"));

        Assert.AreEqual(typeof(RescueNode), widget.GetExpectedNodeType());
    }

    #endregion

    #region Event Handler Tests

    [TestMethod]
    public async Task RescueWidget_OnRescue_CalledWhenErrorCaptured()
    {
        var capturedArgs = (RescueEventArgs?)null;
        var childWidget = new TextBlockWidget("Test");
        var widget = new RescueWidget(childWidget)
            .OnRescue(args => capturedArgs = args);

        var context = ReconcileContext.CreateRoot();
        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult() as RescueNode;

        // Capture an error
        var exception = new InvalidOperationException("Test error");
        node!.CaptureErrorAsync(exception, RescueErrorPhase.Render).GetAwaiter().GetResult();

        Assert.IsNotNull(capturedArgs);
        Assert.AreSame(exception, capturedArgs.Exception);
        Assert.AreEqual(RescueErrorPhase.Render, capturedArgs.Phase);
    }

    [TestMethod]
    public async Task RescueWidget_OnReset_CalledAfterReset()
    {
        var capturedArgs = (RescueResetEventArgs?)null;
        var childWidget = new TextBlockWidget("Test");
        var widget = new RescueWidget(childWidget)
            .OnReset(args => capturedArgs = args);

        var context = ReconcileContext.CreateRoot();
        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult() as RescueNode;

        // Capture an error first
        var exception = new InvalidOperationException("Test error");
        await node!.CaptureErrorAsync(exception, RescueErrorPhase.Measure);

        // Reset
        await node.ResetAsync();

        Assert.IsNotNull(capturedArgs);
        Assert.AreSame(exception, capturedArgs.Exception);
        Assert.AreEqual(RescueErrorPhase.Measure, capturedArgs.Phase);
        Assert.IsFalse(node.HasError); // Should be cleared
    }

    [TestMethod]
    public async Task RescueWidget_WithFallback_UsesCustomFallback()
    {
        var widget = new RescueWidget(new TextBlockWidget("Test"))
            .Fallback(rescue => new TextBlockWidget($"Error: {rescue.Exception?.Message}"));

        Assert.IsNotNull(widget.FallbackBuilder);
    }

    #endregion

    #region Integration Tests

    [TestMethod]
    public async Task RescueNode_AfterReset_ResumesNormalRendering()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var context = new Hex1bRenderContext(workload);
        var throwOnce = new ThrowOnceNode();
        var node = new RescueNode
        {
            Child = throwOnce,
            ShowDetails = true
        };

        // First measure throws
        node.Measure(new Constraints(0, 100, 0, 50));
        Assert.IsTrue(node.HasError);

        // Reset and try again
        node.Reset();
        throwOnce.HasThrown = false; // Reset the throwing node too
        throwOnce.ShouldThrow = false; // Don't throw this time

        // Need to re-set the child because fallback replaced it
        node.Measure(new Constraints(0, 100, 0, 50));

        // State should still be clear after successful operation
        Assert.IsFalse(node.HasError);
    }

    [TestMethod]
    public async Task RescueWidget_ChainedWithOtherWidgets_WorksCorrectly()
    {
        var innerWidget = new TextBlockWidget("Content");

        var rescueWidget = new RescueWidget(innerWidget);
        var borderedWidget = new BorderWidget(rescueWidget);

        var context = ReconcileContext.CreateRoot();
        var node = borderedWidget.ReconcileAsync(null, context).GetAwaiter().GetResult() as BorderNode;

        Assert.IsNotNull(node);
        TestSeq.IsType<RescueNode>(node?.Child);
    }

    #endregion

    #region RescueContext Tests

    [TestMethod]
    public async Task RescueContext_ExposesExceptionInfo()
    {
        var exception = new ArgumentException("Test arg");

        var ctx = new RescueContext(exception, RescueErrorPhase.Arrange, () => { });

        Assert.AreSame(exception, ctx.Exception);
        Assert.AreEqual(RescueErrorPhase.Arrange, ctx.ErrorPhase);
    }

    [TestMethod]
    public async Task RescueContext_Reset_InvokesCallback()
    {
        var resetCalled = false;
        var ctx = new RescueContext(new Exception(), RescueErrorPhase.Build, () => resetCalled = true);

        ctx.Reset();

        Assert.IsTrue(resetCalled);
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

        protected override Size MeasureCore(Constraints constraints)
        {
            if (ThrowOnMeasure)
            {
                throw new InvalidOperationException("Test exception");
            }
            return new Size(10, 1);
        }

        protected override void ArrangeCore(Rect bounds)
        {
            base.ArrangeCore(bounds);
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

        protected override Size MeasureCore(Constraints constraints)
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
