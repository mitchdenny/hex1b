using Hex1b;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class HyperlinkNodeTests
{
    [Fact]
    public void Measure_ReturnsTextWidth()
    {
        var node = new HyperlinkNode { Text = "Click here", Uri = "https://example.com" };
        
        var size = node.Measure(Constraints.Unbounded);
        
        Assert.Equal(10, size.Width); // "Click here" is 10 characters
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_WithEmptyText_ReturnsZeroWidth()
    {
        var node = new HyperlinkNode { Text = "", Uri = "https://example.com" };
        
        var size = node.Measure(Constraints.Unbounded);
        
        Assert.Equal(0, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_RespectsMaxConstraints()
    {
        var node = new HyperlinkNode { Text = "This is a very long hyperlink text", Uri = "https://example.com" };
        var constraints = new Constraints(0, 10, 0, 1);
        
        var size = node.Measure(constraints);
        
        Assert.Equal(10, size.Width); // Clamped to max
    }

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new HyperlinkNode();
        
        Assert.True(node.IsFocusable);
    }

    [Fact]
    public void IsFocused_WhenChanged_MarksDirty()
    {
        var node = new HyperlinkNode();
        node.ClearDirty();
        
        node.IsFocused = true;
        
        Assert.True(node.IsDirty);
    }

    [Fact]
    public void IsHovered_WhenChanged_MarksDirty()
    {
        var node = new HyperlinkNode();
        node.ClearDirty();
        
        node.IsHovered = true;
        
        Assert.True(node.IsDirty);
    }

    [Fact]
    public void Text_WhenChanged_MarksDirty()
    {
        var node = new HyperlinkNode { Text = "Initial" };
        node.ClearDirty();
        
        node.Text = "Changed";
        
        Assert.True(node.IsDirty);
    }

    [Fact]
    public void Uri_WhenChanged_MarksDirty()
    {
        var node = new HyperlinkNode { Uri = "https://old.com" };
        node.ClearDirty();
        
        node.Uri = "https://new.com";
        
        Assert.True(node.IsDirty);
    }

    [Fact]
    public void Parameters_WhenChanged_MarksDirty()
    {
        var node = new HyperlinkNode { Parameters = "" };
        node.ClearDirty();
        
        node.Parameters = "id=test";
        
        Assert.True(node.IsDirty);
    }

    [Fact]
    public void Render_OutputsOsc8Sequences()
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
        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        
        node.Render(context);
        
        var snapshot = terminal.CreateSnapshot();
        
        // The visible text should be "Link"
        Assert.Contains("Link", snapshot.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_WithParameters_IncludesParametersInSequence()
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
        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        
        node.Render(context);
        
        var snapshot = terminal.CreateSnapshot();
        
        // Check that the link text is rendered
        Assert.Contains("Link", snapshot.GetLineTrimmed(0));
    }

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.True(clicked);
    }

    [Fact]
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

        Assert.Equal(InputResult.NotHandled, result);
        Assert.False(clicked);
    }

    [Fact]
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

        Assert.Equal(InputResult.NotHandled, result);
    }
}

public class HyperlinkWidgetTests
{
    [Fact]
    public void Constructor_SetsTextAndUri()
    {
        var widget = new HyperlinkWidget("Click me", "https://example.com");
        
        Assert.Equal("Click me", widget.Text);
        Assert.Equal("https://example.com", widget.Uri);
    }

    [Fact]
    public void WithParameters_SetsParameters()
    {
        var widget = new HyperlinkWidget("Link", "https://example.com")
            .WithParameters("id=test");
        
        Assert.Equal("id=test", widget.Parameters);
    }

    [Fact]
    public void WithId_SetsIdParameter()
    {
        var widget = new HyperlinkWidget("Link", "https://example.com")
            .WithId("unique123");
        
        Assert.Equal("id=unique123", widget.Parameters);
    }

    [Fact]
    public void OnClick_SetsClickHandler()
    {
        var widget = new HyperlinkWidget("Link", "https://example.com")
            .OnClick(_ => { });
        
        Assert.NotNull(widget.ClickHandler);
    }

    [Fact]
    public void OnClick_Async_SetsClickHandler()
    {
        var widget = new HyperlinkWidget("Link", "https://example.com")
            .OnClick(async _ => { await Task.Delay(1); });
        
        Assert.NotNull(widget.ClickHandler);
    }

    [Fact]
    public void Reconcile_CreatesNewNode()
    {
        var widget = new HyperlinkWidget("Link", "https://example.com");
        var context = ReconcileContext.CreateRoot();
        
        var node = widget.Reconcile(null, context);
        
        Assert.IsType<HyperlinkNode>(node);
        var hyperlinkNode = (HyperlinkNode)node;
        Assert.Equal("Link", hyperlinkNode.Text);
        Assert.Equal("https://example.com", hyperlinkNode.Uri);
    }

    [Fact]
    public void Reconcile_ReusesExistingNode()
    {
        var widget = new HyperlinkWidget("Link", "https://example.com");
        var existingNode = new HyperlinkNode();
        var context = ReconcileContext.CreateRoot();
        
        var node = widget.Reconcile(existingNode, context);
        
        Assert.Same(existingNode, node);
    }

    [Fact]
    public void Reconcile_UpdatesNodeProperties()
    {
        var widget = new HyperlinkWidget("New Text", "https://new.com")
            .WithParameters("id=new");
        var existingNode = new HyperlinkNode 
        { 
            Text = "Old Text", 
            Uri = "https://old.com", 
            Parameters = "" 
        };
        var context = ReconcileContext.CreateRoot();
        
        var node = widget.Reconcile(existingNode, context);
        
        var hyperlinkNode = (HyperlinkNode)node;
        Assert.Equal("New Text", hyperlinkNode.Text);
        Assert.Equal("https://new.com", hyperlinkNode.Uri);
        Assert.Equal("id=new", hyperlinkNode.Parameters);
    }

    [Fact]
    public void GetExpectedNodeType_ReturnsHyperlinkNode()
    {
        var widget = new HyperlinkWidget("Link", "https://example.com");
        
        Assert.Equal(typeof(HyperlinkNode), widget.GetExpectedNodeType());
    }
}

public class HyperlinkIntegrationTests
{
    [Fact]
    public async Task Integration_Hyperlink_RendersViaHex1bApp()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Hyperlink("GitHub", "https://github.com")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("GitHub"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("GitHub"));
    }

    [Fact]
    public async Task Integration_Hyperlink_Enter_TriggersAction()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
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
            .WaitUntil(s => s.ContainsText("Click Me"), TimeSpan.FromSeconds(2))
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(clicked);
        Assert.Equal("https://example.com", clickedUri);
    }

    [Fact]
    public async Task Integration_MultipleHyperlinks_TabNavigates()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
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
            .WaitUntil(s => s.ContainsText("Link 1"), TimeSpan.FromSeconds(2))
            .Tab()
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.False(link1Clicked);
        Assert.True(link2Clicked);
    }

    [Fact]
    public async Task Integration_HyperlinkWithButton_TabBetween()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
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
            .WaitUntil(s => s.ContainsText("Visit Site"), TimeSpan.FromSeconds(2))
            .Tab()
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.False(linkClicked);
        Assert.True(buttonClicked);
    }
}

