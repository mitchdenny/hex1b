using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class FloatWidgetTests
{
    [Fact]
    public void VStack_FloatAbsolute_PositionsChildAtCoordinates()
    {
        // Arrange
        var child1 = new IconNode { Icon = "A" };
        var child2 = new IconNode { Icon = "B" };
        var node = new VStackNode();
        // Manually set up as if reconciled: one flow child + one float
        node.Children = [child1];
        node.Floats =
        [
            new FloatEntry { Node = child2, AbsoluteX = 10, AbsoluteY = 5 }
        ];

        // Act
        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));

        // Assert — flow child at top, float at absolute position
        Assert.Equal(0, child1.Bounds.X);
        Assert.Equal(0, child1.Bounds.Y);
        Assert.Equal(10, child2.Bounds.X);
        Assert.Equal(5, child2.Bounds.Y);
    }

    [Fact]
    public void VStack_FloatAbsolute_OffsetsFromContainerOrigin()
    {
        // Arrange — container at (5, 3)
        var child = new IconNode { Icon = "X" };
        var node = new VStackNode
        {
            Floats = [new FloatEntry { Node = child, AbsoluteX = 10, AbsoluteY = 5 }]
        };

        // Act
        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(5, 3, 80, 24));

        // Assert — float position = container origin + absolute offset
        Assert.Equal(15, child.Bounds.X);
        Assert.Equal(8, child.Bounds.Y);
    }

    [Fact]
    public void VStack_FloatAlignRight_AlignsRightEdges()
    {
        // Arrange
        var anchor = new TextBlockNode { Text = "Header" };
        var floatNode = new TextBlockNode { Text = "Hi" };
        var node = new VStackNode
        {
            Children = [anchor],
            Floats =
            [
                new FloatEntry
                {
                    Node = floatNode,
                    HorizontalAnchor = anchor,
                    HorizontalAlignment = FloatHorizontalAlignment.AlignRight,
                    VerticalAnchor = anchor,
                    VerticalAlignment = FloatVerticalAlignment.ExtendBottom,
                }
            ]
        };

        // Act
        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));

        // Assert — float's right edge = anchor's right edge
        var anchorRight = anchor.Bounds.X + anchor.Bounds.Width;
        var floatRight = floatNode.Bounds.X + floatNode.Bounds.Width;
        Assert.Equal(anchorRight, floatRight);
        // Float should be below anchor
        Assert.Equal(anchor.Bounds.Y + anchor.Bounds.Height, floatNode.Bounds.Y);
    }

    [Fact]
    public void VStack_FloatExtendRight_PlacesBesideAnchor()
    {
        // Arrange
        var anchor = new TextBlockNode { Text = "Left" };
        var floatNode = new TextBlockNode { Text = "Right" };
        var node = new VStackNode
        {
            Children = [anchor],
            Floats =
            [
                new FloatEntry
                {
                    Node = floatNode,
                    HorizontalAnchor = anchor,
                    HorizontalAlignment = FloatHorizontalAlignment.ExtendRight,
                    VerticalAnchor = anchor,
                    VerticalAlignment = FloatVerticalAlignment.AlignTop,
                }
            ]
        };

        // Act
        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));

        // Assert — float's left edge = anchor's right edge
        Assert.Equal(anchor.Bounds.X + anchor.Bounds.Width, floatNode.Bounds.X);
        Assert.Equal(anchor.Bounds.Y, floatNode.Bounds.Y);
    }

    [Fact]
    public void GetChildren_ReturnsAllFlowAndFloatNodes()
    {
        // Arrange
        var flow1 = new IconNode { Icon = "A" };
        var flow2 = new IconNode { Icon = "B" };
        var floatNode = new IconNode { Icon = "C" };
        var node = new VStackNode
        {
            Children = [flow1, flow2],
            Floats = [new FloatEntry { Node = floatNode, AbsoluteX = 0, AbsoluteY = 0 }],
            AllChildrenInOrder = [flow1, floatNode, flow2],
        };

        // Act
        var children = node.GetChildren().ToList();

        // Assert — declaration order preserved
        Assert.Equal(3, children.Count);
        Assert.Same(flow1, children[0]);
        Assert.Same(floatNode, children[1]);
        Assert.Same(flow2, children[2]);
    }

    [Fact]
    public void Reconcile_FloatWidget_SeparatesFromFlowChildren()
    {
        // Arrange
        var widget = new VStackWidget([
            new IconWidget("A"),
            new FloatWidget(new IconWidget("B")).Absolute(10, 5),
            new IconWidget("C"),
        ]);
        var context = ReconcileContext.CreateRoot(new FocusRing());

        // Act
        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult();
        var vstackNode = (VStackNode)node;

        // Assert — 2 flow children, 1 float
        Assert.Equal(2, vstackNode.Children.Count);
        Assert.Single(vstackNode.Floats);
        Assert.Equal(10, vstackNode.Floats[0].AbsoluteX);
        Assert.Equal(5, vstackNode.Floats[0].AbsoluteY);
    }

    [Fact]
    public void Reconcile_PreservesNodeOnSameType()
    {
        // Arrange
        var widget1 = new VStackWidget([
            new FloatWidget(new IconWidget("A")).Absolute(0, 0),
        ]);
        var widget2 = new VStackWidget([
            new FloatWidget(new IconWidget("B")).Absolute(5, 5),
        ]);
        var context = ReconcileContext.CreateRoot(new FocusRing());

        // Act
        var node1 = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult();
        var node2 = widget2.ReconcileAsync(node1, context).GetAwaiter().GetResult();

        // Assert — same VStack node reused
        Assert.Same(node1, node2);
        var vstackNode = (VStackNode)node2;
        Assert.Single(vstackNode.Floats);
        Assert.Equal(5, vstackNode.Floats[0].AbsoluteX);
    }

    [Fact]
    public void EmptyVStack_WithNoFloats_ArrangesWithoutError()
    {
        // Arrange
        var node = new VStackNode();

        // Act & Assert — should not throw
        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));
        Assert.Empty(node.Children);
        Assert.Empty(node.Floats);
    }

    [Fact]
    public void HStack_FloatAbsolute_Works()
    {
        // Arrange
        var floatNode = new IconNode { Icon = "F" };
        var node = new HStackNode
        {
            Floats = [new FloatEntry { Node = floatNode, AbsoluteX = 20, AbsoluteY = 3 }]
        };

        // Act
        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));

        // Assert
        Assert.Equal(20, floatNode.Bounds.X);
        Assert.Equal(3, floatNode.Bounds.Y);
    }

    [Fact]
    public void FloatWidget_AlignmentComposition_WorksWithSeparateAnchors()
    {
        // Test that horizontal and vertical alignment can be set independently
        var anchor = new IconWidget("📍");
        var fw = new FloatWidget(new IconWidget("T"))
            .AlignRight(anchor, 2)
            .ExtendBottom(anchor, 1);

        Assert.Equal(FloatHorizontalAlignment.AlignRight, fw.HorizontalAlignment);
        Assert.Equal(2, fw.HorizontalOffset);
        Assert.Equal(FloatVerticalAlignment.ExtendBottom, fw.VerticalAlignment);
        Assert.Equal(1, fw.VerticalOffset);
        Assert.Same(anchor, fw.HorizontalAnchor);
        Assert.Same(anchor, fw.VerticalAnchor);
    }

    [Fact]
    public void FloatWidget_Absolute_SetsCoordinates()
    {
        var fw = new FloatWidget(new IconWidget("X")).Absolute(42, 13);
        Assert.Equal(42, fw.AbsoluteX);
        Assert.Equal(13, fw.AbsoluteY);
    }
}

public class FloatWidgetIntegrationTests
{
    [Fact]
    public async Task Integration_FloatButton_Enter_TriggersAction()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Content"),
                    v.Float(v.Button("Submit").OnClick(_ => { clicked = true; return Task.CompletedTask; })).Absolute(2, 8),
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Submit"), TimeSpan.FromSeconds(5), "Submit button to appear")
            .Enter()
            .WaitUntil(s => true, TimeSpan.FromMilliseconds(200), "frame to process")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(clicked, "Floated button click handler was not invoked");
    }
}

public class FloatWidgetZStackIntegrationTests
{
    [Fact]
    public async Task Integration_ZStack_FloatButton_Enter_TriggersAction()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.ZStack(z => [
                    z.VStack(v => [
                        v.Text("Background"),
                    ]),
                    z.Float(z.Button("Submit").OnClick(_ => { clicked = true; return Task.CompletedTask; })).Absolute(2, 8),
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Submit"), TimeSpan.FromSeconds(5), "Submit button to appear")
            .Enter()
            .WaitUntil(s => true, TimeSpan.FromMilliseconds(200), "frame to process")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(clicked, "ZStack floated button click handler was not invoked");
    }
}

public class FloatWidgetPickerIntegrationTests
{
    [Fact]
    public async Task Integration_FlowButtonInVStackWithFloats_ReceivesFocus()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var flowClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Button("Flow Button").OnClick(_ => { flowClicked = true; return Task.CompletedTask; }),
                    v.Float(v.Border(b => [b.Text("Float")]).Title("F")).Absolute(30, 5),
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Flow Button"), TimeSpan.FromSeconds(5), "flow button to appear")
            .Enter()
            .WaitUntil(s => true, TimeSpan.FromMilliseconds(200), "frame to process")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(flowClicked, "Flow button in VStack with floats should receive focus and be clickable");
    }

    [Fact]
    public async Task Integration_AlignmentExplorerLayout_PickerOpensAndSelects()
    {
        // Exact reproduction of the FloatAlignmentExplorer sample layout
        var horizontal = "(none)";
        string[] hOptions = ["(none)", "AlignLeft", "AlignRight", "ExtendLeft", "ExtendRight"];
        string[] vOptions = ["(none)", "AlignTop", "AlignBottom", "ExtendTop", "ExtendBottom"];
        string[] offsetOptions = ["0", "-2", "-1", "1", "2", "3", "4"];

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        // Use the synchronous Hex1bApp constructor - same as the sample
        using var app = new Hex1bApp(ctx => ctx.VStack(v =>
        {
            var anchor = v.Border(b => [b.Text("  Anchor Widget  ")]).Title("Anchor");
            var floated = v.Float(v.Border(b => [b.Text("Float")]).Title("Float"));
            floated = horizontal switch
            {
                "AlignLeft" => floated.AlignLeft(anchor, 0),
                _ => floated.Absolute(25, 6),
            };
            return [
                v.Text(""),
                v.HStack(h => [
                    h.Text(" Horizontal: "),
                    h.Picker(hOptions).OnSelectionChanged(e => horizontal = e.SelectedText),
                    h.Text("  Vertical: "),
                    h.Picker(vOptions),
                    h.Text("  Offset: "),
                    h.Picker(offsetOptions),
                ]),
                v.Text(""),
                v.Text($" H: {horizontal}"),
                v.Text(""),
                anchor,
                floated,
            ];
        }), new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("(none)"), TimeSpan.FromSeconds(5), "picker to appear")
            .Enter()
            .WaitUntil(s => s.ContainsText("AlignLeft") && s.ContainsText("AlignRight"), TimeSpan.FromSeconds(5), "dropdown to open")
            .Down()
            .WaitUntil(s => true, TimeSpan.FromMilliseconds(200), "frame to process")
            .Enter()
            .WaitUntil(s => s.ContainsText("H: AlignLeft"), TimeSpan.FromSeconds(5), "selection to apply")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("AlignLeft", horizontal);
    }
}
