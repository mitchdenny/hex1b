using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal.Automation;
using Hex1b.Widgets;
using Hex1b.Terminal;

namespace Hex1b.Tests;

/// <summary>
/// Tests for ResponsiveNode layout, rendering, and condition evaluation.
/// </summary>
public class ResponsiveNodeTests
{
    private static Hex1bRenderContext CreateContext(IHex1bAppTerminalWorkloadAdapter workload)
    {
        return new Hex1bRenderContext(workload);
    }

    [Fact]
    public void Measure_FirstMatchingCondition_ReturnsChildSize()
    {
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Hidden")),
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Visible"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Hidden" },
                new TextBlockNode { Text = "Visible" }
            ]
        };

        var size = node.Measure(Constraints.Unbounded);

        // Should measure the "Visible" text (7 chars)
        Assert.Equal(7, size.Width);
        Assert.Equal(1, size.Height);
        Assert.Equal(1, node.ActiveBranchIndex);
    }

    [Fact]
    public void Measure_FirstConditionTrue_SelectsFirst()
    {
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => true, new TextBlockWidget("First")),
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Second"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "First" },
                new TextBlockNode { Text = "Second" }
            ]
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(5, size.Width); // "First"
        Assert.Equal(0, node.ActiveBranchIndex);
    }

    [Fact]
    public void Measure_NoMatchingCondition_ReturnsZero()
    {
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Hidden1")),
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Hidden2"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Hidden1" },
                new TextBlockNode { Text = "Hidden2" }
            ]
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
        Assert.Equal(-1, node.ActiveBranchIndex);
    }

    [Fact]
    public void Measure_EmptyBranches_ReturnsZero()
    {
        var node = new ResponsiveNode
        {
            Branches = [],
            ChildNodes = []
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
        Assert.Equal(-1, node.ActiveBranchIndex);
    }

    [Fact]
    public void Measure_RespectsConstraints()
    {
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => true, new TextBlockWidget("This is a long text"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "This is a long text" }
            ]
        };

        var size = node.Measure(new Constraints(0, 10, 0, 5));

        Assert.True(size.Width <= 10);
        Assert.True(size.Height <= 5);
    }

    [Fact]
    public void Arrange_ActiveChildGetsFullBounds()
    {
        var child1 = new TextBlockNode { Text = "Hidden" };
        var child2 = new TextBlockNode { Text = "Visible" };
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Hidden")),
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Visible"))
            ],
            ChildNodes = [child1, child2]
        };
        var bounds = new Rect(5, 3, 20, 10);

        node.Measure(Constraints.Tight(20, 10));
        node.Arrange(bounds);

        // Only the active child should have bounds set
        Assert.Equal(bounds, child2.Bounds);
        Assert.Equal(bounds, node.Bounds);
    }

    [Fact]
    public void Render_OnlyRendersActiveChild()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 5);
        var context = CreateContext(workload);
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Hidden")),
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Visible"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Hidden" },
                new TextBlockNode { Text = "Visible" }
            ]
        };

        node.Measure(Constraints.Tight(30, 5));
        node.Arrange(new Rect(0, 0, 30, 5));
        node.Render(context);

        var screenText = terminal.CreateSnapshot().GetScreenText();
        Assert.Contains("Visible", screenText);
        Assert.DoesNotContain("Hidden", screenText);
    }

    [Fact]
    public void Render_NoMatchingCondition_RendersNothing()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 5);
        var context = CreateContext(workload);
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Hidden"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Hidden" }
            ]
        };

        node.Measure(Constraints.Tight(30, 5));
        node.Arrange(new Rect(0, 0, 30, 5));
        node.Render(context);

        var screenText = terminal.CreateSnapshot().GetScreenText();
        Assert.DoesNotContain("Hidden", screenText);
    }

    [Fact]
    public void Measure_ConditionReceivesAvailableSize()
    {
        int receivedWidth = 0;
        int receivedHeight = 0;
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => { receivedWidth = w; receivedHeight = h; return true; }, new TextBlockWidget("Test"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Test" }
            ]
        };

        node.Measure(new Constraints(0, 100, 0, 50));

        Assert.Equal(100, receivedWidth);
        Assert.Equal(50, receivedHeight);
    }

    [Fact]
    public void Measure_WidthBasedCondition_SelectsCorrectBranch()
    {
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => w >= 100, new TextBlockWidget("Wide")),
                new ConditionalWidget((w, h) => w >= 50, new TextBlockWidget("Medium")),
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Narrow"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Wide" },
                new TextBlockNode { Text = "Medium" },
                new TextBlockNode { Text = "Narrow" }
            ]
        };

        // Wide layout
        node.Measure(new Constraints(0, 120, 0, 30));
        Assert.Equal(0, node.ActiveBranchIndex);

        // Medium layout
        node.Measure(new Constraints(0, 80, 0, 30));
        Assert.Equal(1, node.ActiveBranchIndex);

        // Narrow layout
        node.Measure(new Constraints(0, 40, 0, 30));
        Assert.Equal(2, node.ActiveBranchIndex);
    }

    [Fact]
    public void GetFocusableNodes_ReturnsOnlyActiveChildFocusables()
    {
        var button1 = new ButtonNode { Label = "Hidden" };
        var button2 = new ButtonNode { Label = "Visible" };
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new ButtonWidget("Hidden").OnClick(_ => Task.CompletedTask)),
                new ConditionalWidget((w, h) => true, new ButtonWidget("Visible").OnClick(_ => Task.CompletedTask))
            ],
            ChildNodes = [button1, button2]
        };

        // Evaluate conditions
        node.Measure(Constraints.Unbounded);

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Contains(button2, focusables);
        Assert.DoesNotContain(button1, focusables);
    }

    [Fact]
    public void GetFocusableNodes_NoMatchingCondition_ReturnsEmpty()
    {
        var button = new ButtonNode { Label = "Hidden" };
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new ButtonWidget("Hidden").OnClick(_ => Task.CompletedTask))
            ],
            ChildNodes = [button]
        };

        node.Measure(Constraints.Unbounded);
        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    [Fact]
    public async Task HandleInput_PassesToActiveChild()
    {
        var clicked = false;
        var button = new ButtonNode
        {
            Label = "Click",
            IsFocused = true,
            ClickAction = _ => { clicked = true; return Task.CompletedTask; }
        };
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => true, new ButtonWidget("Click").OnClick(_ => { clicked = true; return Task.CompletedTask; }))
            ],
            ChildNodes = [button]
        };

        node.Measure(Constraints.Unbounded);
        
        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();
        var routerState = new InputRouterState();
        
        // Use InputRouter to route input to the focused child
        var result = await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None), focusRing, routerState, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.True(clicked);
    }

    [Fact]
    public void HandleInput_NoActiveChild_ReturnsFalse()
    {
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Hidden"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Hidden" }
            ]
        };

        node.Measure(Constraints.Unbounded);
        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.A, 'A', Hex1bModifiers.None));

        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public void IsFocusable_ReturnsFalse()
    {
        var node = new ResponsiveNode();

        Assert.False(node.IsFocusable);
    }

    [Fact]
    public void ActiveChild_ReturnsCorrectNode()
    {
        var child1 = new TextBlockNode { Text = "First" };
        var child2 = new TextBlockNode { Text = "Second" };
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("First")),
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Second"))
            ],
            ChildNodes = [child1, child2]
        };

        node.Measure(Constraints.Unbounded);

        Assert.Same(child2, node.ActiveChild);
    }

    [Fact]
    public void ActiveChild_NoMatch_ReturnsNull()
    {
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Hidden"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Hidden" }
            ]
        };

        node.Measure(Constraints.Unbounded);

        Assert.Null(node.ActiveChild);
    }

    [Fact]
    public void NestedResponsive_WorksCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 5);
        var context = CreateContext(workload);

        var innerNode = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Inner"))
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "Inner" }
            ]
        };

        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Outer"))
            ],
            ChildNodes = [innerNode]
        };

        // Override ChildNodes to use the inner responsive
        node.ChildNodes = [innerNode];

        node.Measure(Constraints.Tight(30, 5));
        node.Arrange(new Rect(0, 0, 30, 5));
        node.Render(context);

        Assert.Contains("Inner", terminal.CreateSnapshot().GetScreenText());
    }

    [Fact]
    public void Responsive_WithOtherwiseFallback_ShowsFallback()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 5);
        var context = CreateContext(workload);
        var node = new ResponsiveNode
        {
            Branches =
            [
                new ConditionalWidget((w, h) => false, new TextBlockWidget("First")),
                new ConditionalWidget((w, h) => false, new TextBlockWidget("Second")),
                new ConditionalWidget((w, h) => true, new TextBlockWidget("Fallback")) // Otherwise is (w,h) => true
            ],
            ChildNodes =
            [
                new TextBlockNode { Text = "First" },
                new TextBlockNode { Text = "Second" },
                new TextBlockNode { Text = "Fallback" }
            ]
        };

        node.Measure(Constraints.Tight(30, 5));
        node.Arrange(new Rect(0, 0, 30, 5));
        node.Render(context);

        Assert.Contains("Fallback", terminal.CreateSnapshot().GetScreenText());
    }

    #region Integration Tests with Fluent API

    [Fact]
    public async Task Integration_Responsive_WideLayout_ShowsWideContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 120, 20);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Responsive(r => [
                    r.WhenMinWidth(100, r => r.Text("Wide View: Full Details")),
                    r.Otherwise(r => r.Text("Compact"))
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Wide View"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Wide View: Full Details"));
        Assert.False(terminal.CreateSnapshot().ContainsText("Compact"));
    }

    [Fact]
    public async Task Integration_Responsive_NarrowLayout_ShowsNarrowContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 50, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Responsive(r => [
                    r.WhenMinWidth(100, r => r.Text("Wide View")),
                    r.Otherwise(r => r.Text("Compact View"))
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Compact View"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Compact View"));
        Assert.False(terminal.CreateSnapshot().ContainsText("Wide View"));
    }

    [Fact]
    public async Task Integration_Responsive_ThreeTiers_SelectsCorrectTier()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 75, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Responsive(r => [
                    r.WhenMinWidth(100, r => r.Text("Large")),
                    r.WhenMinWidth(60, r => r.Text("Medium")),
                    r.Otherwise(r => r.Text("Small"))
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Medium"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // 75 width should match Medium tier
        Assert.True(terminal.CreateSnapshot().ContainsText("Medium"));
        Assert.False(terminal.CreateSnapshot().ContainsText("Large"));
        Assert.False(terminal.CreateSnapshot().ContainsText("Small"));
    }

    [Fact]
    public async Task Integration_Responsive_WhenWidth_ConditionWorks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Responsive(r => [
                    r.WhenWidth(w => w > 100, r => r.Text("Very Wide")),
                    r.WhenWidth(w => w > 50, r => r.Text("Wide")),
                    r.Otherwise(r => r.Text("Narrow"))
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Wide"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Wide"));
        Assert.False(terminal.CreateSnapshot().ContainsText("Very Wide"));
    }

    [Fact]
    public async Task Integration_Responsive_WithFullCondition_UsesWidthAndHeight()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 60, 30);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Responsive(r => [
                    r.When((w, h) => w >= 50 && h >= 20, r => r.Text("Large Screen")),
                    r.Otherwise(r => r.Text("Small Screen"))
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Large Screen"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Large Screen"));
    }

    [Fact]
    public async Task Integration_Responsive_InsideBorder_ReceivesConstrainedSize()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 50, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(
                    ctx.Responsive(r => [
                        r.WhenMinWidth(100, r => r.Text("Wide")),
                        r.Otherwise(r => r.Text("Narrow"))
                    ]),
                    "Container"
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Narrow"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Border takes 2 columns, so inner space is 48, which is < 100
        Assert.True(terminal.CreateSnapshot().ContainsText("Narrow"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Container"));
    }

    [Fact]
    public async Task Integration_Responsive_WithFocusableChildren_FocusWorks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 10);
        var clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Responsive(r => [
                    r.WhenMinWidth(50, r => r.Button("Click Me").OnClick(_ => { clicked = true; return Task.CompletedTask; })),
                    r.Otherwise(r => r.Text("Too narrow"))
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
    }

    [Fact]
    public async Task Integration_Responsive_WithTextBox_InputWorks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 10);
        var text = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Responsive(r => [
                    r.WhenMinWidth(50, r => r.TextBox(text).OnTextChanged(args => text = args.NewText)),
                    r.Otherwise(r => r.Text("Too narrow"))
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Type("Responsive input")
            .WaitUntil(s => s.ContainsText("Responsive input"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("Responsive input", text);
    }

    [Fact]
    public async Task Integration_Responsive_InVStack_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Header"),
                    v.Responsive(r => [
                        r.WhenMinWidth(50, r => r.Text("Wide Content")),
                        r.Otherwise(r => r.Text("Narrow"))
                    ]),
                    v.Text("Footer")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Wide Content"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Header"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Wide Content"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Footer"));
    }

    [Fact]
    public async Task Integration_Responsive_NoMatchingConditions_RendersNothing()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Responsive(r => [
                    r.WhenMinWidth(100, r => r.Text("Very Wide")),
                    r.WhenMinWidth(80, r => r.Text("Wide"))
                    // No Otherwise fallback - neither condition matches
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.False(terminal.CreateSnapshot().ContainsText("Very Wide"));
        Assert.False(terminal.CreateSnapshot().ContainsText("Wide"));
    }

    [Fact]
    public async Task Integration_Responsive_WithList_NavigationWorks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 10);
        IReadOnlyList<string> items = ["Item 1", "Item 2"];

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Responsive(r => [
                    r.WhenMinWidth(50, r => r.List(items)),
                    r.Otherwise(r => r.Text("Too narrow for list"))
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2))
            .Down()
            .WaitUntil(s => s.ContainsText("> Item 2"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Verify second item is selected via rendered output
        Assert.True(terminal.CreateSnapshot().ContainsText("> Item 2"));
    }

    [Fact]
    public async Task Integration_Responsive_DifferentLayoutsForDifferentWidgets()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 100, 20);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Responsive(r => [
                    r.WhenMinWidth(80, r =>
                        r.HStack(h => [
                            h.Text("Left Panel"),
                            h.Text("Right Panel")
                        ])
                    ),
                    r.Otherwise(r =>
                        r.VStack(v => [
                            v.Text("Top Panel"),
                            v.Text("Bottom Panel")
                        ])
                    )
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Right Panel"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Wide layout uses HStack
        Assert.True(terminal.CreateSnapshot().ContainsText("Left Panel"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Right Panel"));
    }

    [Fact]
    public async Task Integration_Responsive_NarrowFallsBackToVStack()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 20);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Responsive(r => [
                    r.WhenMinWidth(80, r =>
                        r.HStack(h => [
                            h.Text("Left Panel"),
                            h.Text("Right Panel")
                        ])
                    ),
                    r.Otherwise(r =>
                        r.VStack(v => [
                            v.Text("Top Panel"),
                            v.Text("Bottom Panel")
                        ])
                    )
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Top Panel"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Narrow layout uses VStack
        Assert.True(terminal.CreateSnapshot().ContainsText("Top Panel"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Bottom Panel"));
    }

    [Fact]
    public async Task Integration_Responsive_InSplitter_WorksCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 100, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    ctx.Responsive(r => [
                        r.WhenMinWidth(30, r => r.Text("Wide Left")),
                        r.Otherwise(r => r.Text("Narrow Left"))
                    ]),
                    ctx.Text("Right Panel"),
                    leftWidth: 40
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Wide Left"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Wide Left"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Right Panel"));
    }

    [Fact]
    public async Task Integration_Responsive_WithState_AccessesStateCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 10);
        var state = new { Message = "Hello from state" };

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Responsive(r => [
                    r.Otherwise(r => r.Text(state.Message))
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello from state"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Hello from state"));
    }

    #endregion
}
