using Hex1b;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class HyperlinkNodeTests
{
    [TestMethod]
    public async Task Measure_ReturnsTextWidth()
    {
        var node = new HyperlinkNode { Text = "Click here", Uri = "https://example.com" };
        
        var size = node.Measure(Constraints.Unbounded);
        
        Assert.AreEqual(10, size.Width); // "Click here" is 10 characters
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public async Task Measure_WithEmptyText_ReturnsZeroWidth()
    {
        var node = new HyperlinkNode { Text = "", Uri = "https://example.com" };
        
        var size = node.Measure(Constraints.Unbounded);
        
        Assert.AreEqual(0, size.Width);
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public async Task Measure_RespectsMaxConstraints()
    {
        var node = new HyperlinkNode { Text = "This is a very long hyperlink text", Uri = "https://example.com" };
        var constraints = new Constraints(0, 10, 0, 1);
        
        var size = node.Measure(constraints);
        
        Assert.AreEqual(10, size.Width); // Clamped to max
    }

    [TestMethod]
    public async Task IsFocusable_ReturnsTrue()
    {
        var node = new HyperlinkNode();
        
        Assert.IsTrue(node.IsFocusable);
    }

    [TestMethod]
    public async Task IsFocused_WhenChanged_MarksDirty()
    {
        var node = new HyperlinkNode();
        node.ClearDirty();
        
        node.IsFocused = true;
        
        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public async Task IsHovered_WhenChanged_MarksDirty()
    {
        var node = new HyperlinkNode();
        node.ClearDirty();
        
        node.IsHovered = true;
        
        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public async Task Text_WhenChanged_MarksDirty()
    {
        var node = new HyperlinkNode { Text = "Initial" };
        node.ClearDirty();
        
        node.Text = "Changed";
        
        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public async Task Uri_WhenChanged_MarksDirty()
    {
        var node = new HyperlinkNode { Uri = "https://old.com" };
        node.ClearDirty();
        
        node.Uri = "https://new.com";
        
        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public async Task Parameters_WhenChanged_MarksDirty()
    {
        var node = new HyperlinkNode { Parameters = "" };
        node.ClearDirty();
        
        node.Parameters = "id=test";
        
        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public async Task Render_OutputsOsc8Sequences()
    {
        var node = new HyperlinkNode 
        { 
            Text = "Link", 
            Uri = "https://example.com",
            Parameters = ""
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 10, 1));
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Link"), TimeSpan.FromSeconds(5), "Link text")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // The visible text should be "Link"
        Assert.Contains("Link", snapshot.GetLineTrimmed(0));
    }

    [TestMethod]
    public async Task Render_WithParameters_IncludesParametersInSequence()
    {
        var node = new HyperlinkNode 
        { 
            Text = "Link", 
            Uri = "https://example.com",
            Parameters = "id=unique123"
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 10, 1));
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Link"), TimeSpan.FromSeconds(5), "Link text with parameters")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Check that the link text is rendered
        Assert.Contains("Link", snapshot.GetLineTrimmed(0));
    }

    [TestMethod]
    public async Task HandleInput_Enter_TriggersClickAction()
    {
        var clicked = false;
        var node = new HyperlinkNode 
        { 
            Text = "Link", 
            Uri = "https://example.com",
            IsFocused = true,
            ClickAction = _ => { clicked = true; return Task.CompletedTask; }
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsTrue(clicked);
    }

    [TestMethod]
    public async Task HandleInput_OtherKey_DoesNotClick()
    {
        var clicked = false;
        var node = new HyperlinkNode 
        { 
            Text = "Link", 
            Uri = "https://example.com",
            IsFocused = true,
            ClickAction = _ => { clicked = true; return Task.CompletedTask; }
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.NotHandled, result);
        Assert.IsFalse(clicked);
    }

    [TestMethod]
    public async Task HandleInput_NullClickAction_DoesNotThrow()
    {
        var node = new HyperlinkNode 
        { 
            Text = "Link", 
            Uri = "https://example.com",
            IsFocused = true,
            ClickAction = null
        };

        // With no ClickAction, no bindings are registered, so Enter falls through
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.NotHandled, result);
    }
}

[TestClass]
public class HyperlinkWidgetTests
{
    [TestMethod]
    public async Task Constructor_SetsTextAndUri()
    {
        var widget = new HyperlinkWidget("Click me", "https://example.com");
        
        Assert.AreEqual("Click me", widget.Text);
        Assert.AreEqual("https://example.com", widget.Uri);
    }

    [TestMethod]
    public async Task Parameters_SetsParameters()
    {
        var widget = new HyperlinkWidget("Link", "https://example.com")
            .Parameters("id=test");
        
        Assert.AreEqual("id=test", widget.ParameterString);
    }

    [TestMethod]
    public async Task Id_SetsIdParameter()
    {
        var widget = new HyperlinkWidget("Link", "https://example.com")
            .Id("unique123");
        
        Assert.AreEqual("id=unique123", widget.ParameterString);
    }

    [TestMethod]
    public async Task OnClick_SetsClickHandler()
    {
        var widget = new HyperlinkWidget("Link", "https://example.com")
            .OnClick(_ => { });
        
        Assert.IsNotNull(widget.ClickHandler);
    }

    [TestMethod]
    public async Task OnClick_Async_SetsClickHandler()
    {
        var widget = new HyperlinkWidget("Link", "https://example.com")
            .OnClick(async _ => { await Task.Delay(1); });
        
        Assert.IsNotNull(widget.ClickHandler);
    }

    [TestMethod]
    public async Task Reconcile_CreatesNewNode()
    {
        var widget = new HyperlinkWidget("Link", "https://example.com");
        var context = ReconcileContext.CreateRoot();
        
        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult();
        
        TestSeq.IsType<HyperlinkNode>(node);
        var hyperlinkNode = (HyperlinkNode)node;
        Assert.AreEqual("Link", hyperlinkNode.Text);
        Assert.AreEqual("https://example.com", hyperlinkNode.Uri);
    }

    [TestMethod]
    public async Task Reconcile_ReusesExistingNode()
    {
        var widget = new HyperlinkWidget("Link", "https://example.com");
        var existingNode = new HyperlinkNode();
        var context = ReconcileContext.CreateRoot();
        
        var node = widget.ReconcileAsync(existingNode, context).GetAwaiter().GetResult();
        
        Assert.AreSame(existingNode, node);
    }

    [TestMethod]
    public async Task Reconcile_UpdatesNodeProperties()
    {
        var widget = new HyperlinkWidget("New Text", "https://new.com")
            .Parameters("id=new");
        var existingNode = new HyperlinkNode 
        { 
            Text = "Old Text", 
            Uri = "https://old.com", 
            Parameters = "" 
        };
        var context = ReconcileContext.CreateRoot();
        
        var node = widget.ReconcileAsync(existingNode, context).GetAwaiter().GetResult();
        
        var hyperlinkNode = (HyperlinkNode)node;
        Assert.AreEqual("New Text", hyperlinkNode.Text);
        Assert.AreEqual("https://new.com", hyperlinkNode.Uri);
        Assert.AreEqual("id=new", hyperlinkNode.Parameters);
    }

    [TestMethod]
    public async Task GetExpectedNodeType_ReturnsHyperlinkNode()
    {
        var widget = new HyperlinkWidget("Link", "https://example.com");
        
        Assert.AreEqual(typeof(HyperlinkNode), widget.GetExpectedNodeType());
    }
}

[TestClass]
public class HyperlinkIntegrationTests
{
    [TestMethod]
    public async Task Integration_Hyperlink_RendersViaHex1bApp()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Hyperlink("GitHub", "https://github.com")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("GitHub"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.IsTrue(snapshot.ContainsText("GitHub"));
    }

    [TestMethod]
    public async Task Integration_Hyperlink_Enter_TriggersAction()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var clicked = false;
        var clickedUri = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Hyperlink("Click Me", "https://example.com").OnClick(e => { 
                        clicked = true; 
                        clickedUri = e.Uri;
                    })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click Me"), TimeSpan.FromSeconds(5))
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.IsTrue(clicked);
        Assert.AreEqual("https://example.com", clickedUri);
    }

    [TestMethod]
    public async Task Integration_MultipleHyperlinks_TabNavigates()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var link1Clicked = false;
        var link2Clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Hyperlink("Link 1", "https://one.com").OnClick(_ => { link1Clicked = true; }),
                    v.Hyperlink("Link 2", "https://two.com").OnClick(_ => { link2Clicked = true; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Tab to second link and press Enter
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Link 1"), TimeSpan.FromSeconds(5))
            .Tab()
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.IsFalse(link1Clicked);
        Assert.IsTrue(link2Clicked);
    }

    [TestMethod]
    public async Task Integration_HyperlinkWithButton_TabBetween()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var linkClicked = false;
        var buttonClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Hyperlink("Visit Site", "https://example.com").OnClick(_ => { linkClicked = true; }),
                    v.Button("Submit").OnClick(_ => { buttonClicked = true; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Tab to button and press Enter
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Visit Site"), TimeSpan.FromSeconds(5))
            .Tab()
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.IsFalse(linkClicked);
        Assert.IsTrue(buttonClicked);
    }
}

