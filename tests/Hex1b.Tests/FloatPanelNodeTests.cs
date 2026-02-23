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

    [Fact]
    public async Task Integration_FloatAlignRight_PositionsRelativeToAnchor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        using var app = new Hex1bApp(ctx => ctx.VStack(v =>
        {
            var anchor = v.Border(b => [b.Text("  Anchor  ")]).Title("Anchor");
            var floated = v.Float(v.Border(b => [b.Text("F")]).Title("Float"))
                .AlignRight(anchor)
                .ExtendBottom(anchor);
            return [anchor, floated];
        }), new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snap = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Anchor") && s.ContainsText("F"), TimeSpan.FromSeconds(5), "both widgets to render")
            .Capture("result")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var text = snap.GetScreenText();
        // The float should appear BELOW the anchor (ExtendBottom), not at row 0
        var lines = text.Split('\n');
        int anchorLine = -1, floatLine = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("Anchor") && anchorLine == -1) anchorLine = i;
            if (lines[i].Contains("│F│")) floatLine = i;
        }

        Assert.True(floatLine > anchorLine, $"Float (line {floatLine}) should be below Anchor (line {anchorLine}). Screen:\n{text}");
    }

    [Fact]
    public async Task Integration_FloatExtendLeft_AlignTop_NestedAnchor_PositionsCorrectly()
    {
        // Anchor border is nested inside Center(Padding(...)) — tests recursive anchor resolution
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        using var app = new Hex1bApp(ctx => ctx.VStack(v =>
        {
            var anchorBorder = v.Border(b => [b.Text("  Anchor  ")]).Title("Anchor");
            var wrapped = v.Center(v.Padding(8, 8, 3, 3, anchorBorder));
            var floated = v.Float(v.Text("<<"))
                .ExtendLeft(anchorBorder)
                .AlignTop(anchorBorder);
            return [wrapped, floated];
        }), new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snap = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Anchor") && s.ContainsText("<<"), TimeSpan.FromSeconds(5), "both widgets to render")
            .Capture("result")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var text = snap.GetScreenText();
        var lines = text.Split('\n');

        // Find the line with "<<" and the line with "Anchor" title
        int floatLine = -1, anchorTitleLine = -1;
        int floatCol = -1, anchorCol = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("<<")) { floatLine = i; floatCol = lines[i].IndexOf("<<"); }
            if (lines[i].Contains("Anchor")) { anchorTitleLine = i; anchorCol = lines[i].IndexOf("Anchor"); }
        }

        // Float should be to the LEFT of the anchor border (ExtendLeft)
        Assert.True(floatCol < anchorCol, $"Float col {floatCol} should be left of Anchor col {anchorCol}. Screen:\n{text}");
        // Float should be top-aligned with the anchor (AlignTop), which means same row or close
        Assert.True(Math.Abs(floatLine - anchorTitleLine) <= 1, $"Float line {floatLine} should be near Anchor line {anchorTitleLine}. Screen:\n{text}");
    }
}

/// <summary>
/// Exhaustive cell-level placement tests for all 8 relative alignment combinations.
/// Uses a fixed-size container (40x20), a 10x3 anchor at a known position, and a 4x1 float.
/// Verifies exact cell coordinates by checking rendered screen text.
/// </summary>
public class FloatWidgetPlacementTests
{
    // Direct unit test approach: manually set up nodes, measure, arrange, check Bounds.
    private static (Rect Anchor, Rect Float) ArrangeWithManualNodes(
        FloatHorizontalAlignment hAlign, FloatVerticalAlignment vAlign,
        int hOffset = 0, int vOffset = 0)
    {
        // Anchor: 10 wide, 1 tall, placed at (5, 4) within a 40x20 container
        var anchor = new TextBlockNode { Text = "AAAAAAAAAA" };
        var floatNode = new TextBlockNode { Text = "FFFF" };

        var node = new VStackNode
        {
            Children = [
                new TextBlockNode { Text = "" },
                new TextBlockNode { Text = "" },
                new TextBlockNode { Text = "" },
                new TextBlockNode { Text = "" },
                anchor,
            ],
            Floats = [
                new FloatEntry
                {
                    Node = floatNode,
                    HorizontalAnchor = hAlign != FloatHorizontalAlignment.None ? anchor : null,
                    HorizontalAlignment = hAlign,
                    HorizontalOffset = hOffset,
                    VerticalAnchor = vAlign != FloatVerticalAlignment.None ? anchor : null,
                    VerticalAlignment = vAlign,
                    VerticalOffset = vOffset,
                }
            ]
        };

        node.Measure(new Constraints(0, 40, 0, 20));
        node.Arrange(new Rect(0, 0, 40, 20));

        return (anchor.Bounds, floatNode.Bounds);
    }

    [Fact]
    public void AlignLeft_AlignTop_FloatLeftEdgeMatchesAnchorLeftEdge_SameRow()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.AlignLeft, FloatVerticalAlignment.AlignTop);

        Assert.Equal(anchor.X, floatR.X);       // left edges aligned
        Assert.Equal(anchor.Y, floatR.Y);       // same row
    }

    [Fact]
    public void AlignRight_AlignTop_FloatRightEdgeMatchesAnchorRightEdge_SameRow()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.AlignRight, FloatVerticalAlignment.AlignTop);

        // right edges: anchor.X + 10 == float.X + 4
        Assert.Equal(anchor.X + anchor.Width, floatR.X + floatR.Width);
        Assert.Equal(anchor.Y, floatR.Y);
    }

    [Fact]
    public void ExtendRight_AlignTop_FloatLeftEdgeTouchesAnchorRightEdge()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.ExtendRight, FloatVerticalAlignment.AlignTop);

        Assert.Equal(anchor.X + anchor.Width, floatR.X); // float starts where anchor ends
        Assert.Equal(anchor.Y, floatR.Y);
    }

    [Fact]
    public void ExtendLeft_AlignTop_FloatRightEdgeTouchesAnchorLeftEdge()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.ExtendLeft, FloatVerticalAlignment.AlignTop);

        Assert.Equal(anchor.X, floatR.X + floatR.Width); // float ends where anchor starts
        Assert.Equal(anchor.Y, floatR.Y);
    }

    [Fact]
    public void AlignLeft_AlignBottom_FloatBottomEdgeMatchesAnchorBottomEdge()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.AlignLeft, FloatVerticalAlignment.AlignBottom);

        Assert.Equal(anchor.X, floatR.X);
        // Both are 1 row tall, so AlignBottom = same row
        Assert.Equal(anchor.Y + anchor.Height - floatR.Height, floatR.Y);
    }

    [Fact]
    public void AlignLeft_ExtendBottom_FloatTopEdgeTouchesAnchorBottomEdge()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.AlignLeft, FloatVerticalAlignment.ExtendBottom);

        Assert.Equal(anchor.X, floatR.X);
        Assert.Equal(anchor.Y + anchor.Height, floatR.Y); // float below anchor
    }

    [Fact]
    public void AlignLeft_ExtendTop_FloatBottomEdgeTouchesAnchorTopEdge()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.AlignLeft, FloatVerticalAlignment.ExtendTop);

        Assert.Equal(anchor.X, floatR.X);
        Assert.Equal(anchor.Y, floatR.Y + floatR.Height); // float above anchor
    }

    [Fact]
    public void ExtendRight_ExtendBottom_FloatCornerTouchesAnchorBottomRightCorner()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.ExtendRight, FloatVerticalAlignment.ExtendBottom);

        Assert.Equal(anchor.X + anchor.Width, floatR.X);  // right of anchor
        Assert.Equal(anchor.Y + anchor.Height, floatR.Y); // below anchor
    }

    [Fact]
    public void ExtendLeft_ExtendTop_FloatCornerTouchesAnchorTopLeftCorner()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.ExtendLeft, FloatVerticalAlignment.ExtendTop);

        Assert.Equal(anchor.X, floatR.X + floatR.Width);  // left of anchor
        Assert.Equal(anchor.Y, floatR.Y + floatR.Height); // above anchor
    }

    [Fact]
    public void AlignRight_ExtendBottom_FloatRightAlignedBelow()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.AlignRight, FloatVerticalAlignment.ExtendBottom);

        Assert.Equal(anchor.X + anchor.Width, floatR.X + floatR.Width); // right edges aligned
        Assert.Equal(anchor.Y + anchor.Height, floatR.Y);              // below
    }

    [Fact]
    public void AlignRight_AlignBottom_FloatRightAlignedBottomAligned()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.AlignRight, FloatVerticalAlignment.AlignBottom);

        Assert.Equal(anchor.X + anchor.Width, floatR.X + floatR.Width);
        Assert.Equal(anchor.Y + anchor.Height - floatR.Height, floatR.Y);
    }

    [Fact]
    public void ExtendRight_ExtendTop_FloatAboveAndRight()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.ExtendRight, FloatVerticalAlignment.ExtendTop);

        Assert.Equal(anchor.X + anchor.Width, floatR.X);  // right of anchor
        Assert.Equal(anchor.Y, floatR.Y + floatR.Height); // above anchor
    }

    [Fact]
    public void ExtendLeft_ExtendBottom_FloatBelowAndLeft()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.ExtendLeft, FloatVerticalAlignment.ExtendBottom);

        Assert.Equal(anchor.X, floatR.X + floatR.Width);  // left of anchor
        Assert.Equal(anchor.Y + anchor.Height, floatR.Y); // below anchor
    }

    [Fact]
    public void ExtendLeft_AlignBottom_FloatLeftAndBottomAligned()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.ExtendLeft, FloatVerticalAlignment.AlignBottom);

        Assert.Equal(anchor.X, floatR.X + floatR.Width);
        Assert.Equal(anchor.Y + anchor.Height - floatR.Height, floatR.Y);
    }

    [Fact]
    public void AlignLeft_AlignTop_WithPositiveOffset_ShiftsBoth()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.AlignLeft, FloatVerticalAlignment.AlignTop,
            hOffset: 3, vOffset: 2);

        Assert.Equal(anchor.X + 3, floatR.X);
        Assert.Equal(anchor.Y + 2, floatR.Y);
    }

    [Fact]
    public void ExtendRight_AlignTop_NegativeOffset_OverlapsAnchor()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.ExtendRight, FloatVerticalAlignment.AlignTop,
            hOffset: -2);

        // ExtendRight places at anchor.X + 10, offset -2 = anchor.X + 8
        Assert.Equal(anchor.X + anchor.Width - 2, floatR.X);
        Assert.Equal(anchor.Y, floatR.Y);
    }

    [Fact]
    public void ExtendBottom_NegativeVerticalOffset_OverlapsAnchor()
    {
        var (anchor, floatR) = ArrangeWithManualNodes(
            FloatHorizontalAlignment.AlignLeft, FloatVerticalAlignment.ExtendBottom,
            vOffset: -1);

        Assert.Equal(anchor.X, floatR.X);
        // ExtendBottom places at anchor.Y + 1, offset -1 = anchor.Y
        Assert.Equal(anchor.Y + anchor.Height - 1, floatR.Y);
    }
}

/// <summary>
/// Tests for float-anchored-to-float: what happens when a float references another floating widget as its anchor.
/// </summary>
public class FloatAnchoredToFloatTests
{
    [Fact]
    public async Task FloatAnchoredToFloat_ExtendRight_ChainsHorizontally()
    {
        // Float B anchors to Float A (ExtendRight) — B should appear to the right of A
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 20).Build();

        using var app = new Hex1bApp(ctx => ctx.VStack(v =>
        {
            var floatA = v.Text("AAAA");
            var fwA = v.Float(floatA).Absolute(5, 3);
            var fwB = v.Float(v.Text("BBBB")).ExtendRight(floatA).AlignTop(floatA);
            return [v.Text(""), fwA, fwB];
        }), new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snap = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("AAAA") && s.ContainsText("BBBB"), TimeSpan.FromSeconds(5), "both floats to render")
            .Capture("result")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var text = snap.GetScreenText();
        var lines = text.Split('\n');

        int aRow = -1, aCol = -1, bRow = -1, bCol = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            var ai = lines[i].IndexOf("AAAA");
            if (ai >= 0) { aRow = i; aCol = ai; }
            var bi = lines[i].IndexOf("BBBB");
            if (bi >= 0) { bRow = i; bCol = bi; }
        }

        Assert.True(aCol >= 0 && bCol >= 0, $"Floats not found. Screen:\n{text}");
        // B should be immediately to the right of A
        Assert.Equal(aCol + 4, bCol);
        // Same row
        Assert.Equal(aRow, bRow);
    }

    [Fact]
    public async Task FloatAnchoredToFloat_ExtendBottom_ChainsVertically()
    {
        // Float B anchors to Float A (ExtendBottom) — B should appear below A
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 20).Build();

        using var app = new Hex1bApp(ctx => ctx.VStack(v =>
        {
            var floatA = v.Text("AAAA");
            var fwA = v.Float(floatA).Absolute(5, 3);
            var fwB = v.Float(v.Text("BBBB")).AlignLeft(floatA).ExtendBottom(floatA);
            return [v.Text(""), fwA, fwB];
        }), new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snap = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("AAAA") && s.ContainsText("BBBB"), TimeSpan.FromSeconds(5), "both floats to render")
            .Capture("result")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var text = snap.GetScreenText();
        var lines = text.Split('\n');

        int aRow = -1, aCol = -1, bRow = -1, bCol = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            var ai = lines[i].IndexOf("AAAA");
            if (ai >= 0) { aRow = i; aCol = ai; }
            var bi = lines[i].IndexOf("BBBB");
            if (bi >= 0) { bRow = i; bCol = bi; }
        }

        Assert.True(aCol >= 0 && bCol >= 0, $"Floats not found. Screen:\n{text}");
        // Same column
        Assert.Equal(aCol, bCol);
        // B should be immediately below A (A is 1 row)
        Assert.Equal(aRow + 1, bRow);
    }

    [Fact]
    public async Task ThreeFloatsChained_PositionCorrectly()
    {
        // A at (2,2), B ExtendRight of A, C ExtendBottom of B — L-shaped chain
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 20).Build();

        using var app = new Hex1bApp(ctx => ctx.VStack(v =>
        {
            var txtA = v.Text("AA");
            var txtB = v.Text("BB");
            var fwA = v.Float(txtA).Absolute(2, 2);
            var fwB = v.Float(txtB).ExtendRight(txtA).AlignTop(txtA);
            var fwC = v.Float(v.Text("CC")).AlignLeft(txtB).ExtendBottom(txtB);
            return [v.Text(""), fwA, fwB, fwC];
        }), new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snap = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("AA") && s.ContainsText("BB") && s.ContainsText("CC"),
                TimeSpan.FromSeconds(5), "all three floats to render")
            .Capture("result")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var text = snap.GetScreenText();
        var lines = text.Split('\n');

        // Find positions — need to be careful with substring matching
        int aRow = -1, aCol = -1, bRow = -1, bCol = -1, cRow = -1, cCol = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            // Look for "AABB" pattern on the same line (A at col 2, B at col 4)
            var line = lines[i];
            if (line.Contains("AA") && aRow == -1)
            {
                aCol = line.IndexOf("AA");
                aRow = i;
            }
            if (line.Contains("BB") && bRow == -1)
            {
                bCol = line.IndexOf("BB");
                bRow = i;
            }
            if (line.Contains("CC") && cRow == -1)
            {
                cCol = line.IndexOf("CC");
                cRow = i;
            }
        }

        Assert.True(aCol >= 0 && bCol >= 0 && cCol >= 0, $"Not all floats found. Screen:\n{text}");

        // A at (2, 2)
        Assert.Equal(2, aCol);
        Assert.Equal(2, aRow);

        // B = ExtendRight(A) + AlignTop(A) → col = 2+2=4, row = 2
        Assert.Equal(aCol + 2, bCol);
        Assert.Equal(aRow, bRow);

        // C = AlignLeft(B) + ExtendBottom(B) → col = 4, row = 2+1=3
        Assert.Equal(bCol, cCol);
        Assert.Equal(bRow + 1, cRow);
    }
}
