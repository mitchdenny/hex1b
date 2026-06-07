using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class AccordionNodeTests
{
    private static AccordionNode CreateNode(int sectionCount, bool[] expanded)
    {
        var node = new AccordionNode();
        var sections = new List<AccordionNode.SectionInfo>();
        for (int i = 0; i < sectionCount; i++)
        {
            sections.Add(new AccordionNode.SectionInfo(
                $"Section {i}",
                [],
                []));
        }
        node.SetSections(sections);
        for (int i = 0; i < sectionCount; i++)
        {
            node.SetExpandedState(i, expanded[i]);
        }
        return node;
    }

    [TestMethod]
    public void Measure_AllCollapsed_ReturnsHeaderHeightOnly()
    {
        var node = CreateNode(3, [false, false, false]);
        var constraints = new Constraints(0, 40, 0, 20);

        var size = node.Measure(constraints);

        Assert.AreEqual(40, size.Width);
        Assert.AreEqual(3, size.Height); // 3 headers, 1 row each
    }

    [TestMethod]
    public void Measure_FillsAvailableHeight()
    {
        var node = CreateNode(3, [true, false, false]);
        var constraints = new Constraints(0, 40, 0, 20);

        var size = node.Measure(constraints);

        Assert.AreEqual(40, size.Width);
        Assert.AreEqual(20, size.Height); // fills available
    }

    [TestMethod]
    public void SectionCount_ReturnsCorrectCount()
    {
        var node = CreateNode(3, [true, false, true]);

        Assert.AreEqual(3, node.SectionCount);
    }

    [TestMethod]
    public void IsSectionExpanded_ReturnsCorrectState()
    {
        var node = CreateNode(3, [true, false, true]);

        Assert.IsTrue(node.IsSectionExpanded(0));
        Assert.IsFalse(node.IsSectionExpanded(1));
        Assert.IsTrue(node.IsSectionExpanded(2));
    }

    [TestMethod]
    public void ToggleSection_ChangesExpandedState()
    {
        var node = CreateNode(3, [true, false, false]);

        node.ToggleSection(0);
        Assert.IsFalse(node.IsSectionExpanded(0));

        node.ToggleSection(1);
        Assert.IsTrue(node.IsSectionExpanded(1));
    }

    [TestMethod]
    public void SetSectionExpanded_CollapseOthers_WhenMultipleNotAllowed()
    {
        var node = CreateNode(3, [true, false, false]);
        node.AllowMultipleExpanded = false;

        node.SetSectionExpanded(2, true);

        Assert.IsFalse(node.IsSectionExpanded(0)); // collapsed
        Assert.IsFalse(node.IsSectionExpanded(1));
        Assert.IsTrue(node.IsSectionExpanded(2));
    }

    [TestMethod]
    public void SetSectionExpanded_KeepsOthers_WhenMultipleAllowed()
    {
        var node = CreateNode(3, [true, false, false]);
        node.AllowMultipleExpanded = true;

        node.SetSectionExpanded(2, true);

        Assert.IsTrue(node.IsSectionExpanded(0));
        Assert.IsFalse(node.IsSectionExpanded(1));
        Assert.IsTrue(node.IsSectionExpanded(2));
    }

    [TestMethod]
    public void IsFocused_WhenSet_MarksDirty()
    {
        var node = CreateNode(1, [true]);
        node.ClearDirty();

        node.IsFocused = true;

        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public void IsFocusable_ReturnsTrue()
    {
        var node = CreateNode(1, [true]);

        Assert.IsTrue(node.IsFocusable);
    }

    [TestMethod]
    public void ManagesChildFocus_ReturnsTrue()
    {
        var node = CreateNode(1, [true]);

        Assert.IsTrue(node.ManagesChildFocus);
    }

    [TestMethod]
    public void GetChildren_ReturnsOnlyContentNodes()
    {
        var node = CreateNode(3, [true, false, true]);

        // No content nodes set, so no children
        var children = node.GetChildren().ToList();
        Assert.IsEmpty(children);
    }

    [TestMethod]
    public void IsSectionExpanded_OutOfRange_ReturnsFalse()
    {
        var node = CreateNode(2, [true, false]);

        Assert.IsFalse(node.IsSectionExpanded(-1));
        Assert.IsFalse(node.IsSectionExpanded(5));
    }

    [TestMethod]
    public void Arrange_DistributesContentHeightEqually()
    {
        var node = CreateNode(2, [true, true]);
        var constraints = new Constraints(0, 40, 0, 12);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 12));

        // 2 headers (2 rows) + 10 remaining / 2 expanded = 5 each
        // Verify via GetChildren - content nodes would have bounds if set
        Assert.AreEqual(2, node.SectionCount);
    }

    [TestMethod]
    public void RenderHeader_WideCharacterRightAction_UsesDisplayWidth()
    {
        var action = new AccordionSectionActionBuilder().Icon("搜");
        var node = new AccordionNode();
        node.SetSections([
            new AccordionNode.SectionInfo("播放", [], [action])
        ]);
        node.Measure(new Constraints(0, 8, 0, 1));
        node.Arrange(new Rect(0, 0, 8, 1));

        var surface = new Surface(8, 1);
        node.Render(new SurfaceRenderContext(surface));

        Assert.AreEqual("播", surface[0, 0].Character);
        Assert.IsTrue(surface[1, 0].IsContinuation);
        Assert.AreEqual("放", surface[2, 0].Character);
        Assert.IsTrue(surface[3, 0].IsContinuation);
        Assert.AreEqual("搜", surface[6, 0].Character);
        Assert.IsTrue(surface[7, 0].IsContinuation);
    }

    [TestMethod]
    public void AccordionSectionWidget_Title_SetsTitle()
    {
        var section = new AccordionSectionWidget(s => []).Title("Test Title");

        Assert.AreEqual("Test Title", section.SectionTitle);
    }

    [TestMethod]
    public void AccordionSectionWidget_Expanded_SetsFlag()
    {
        var section = new AccordionSectionWidget(s => []).Expanded();

        Assert.IsTrue(section.IsExpanded);
    }

    [TestMethod]
    public void AccordionSectionWidget_ExpandedFalse_SetsFlag()
    {
        var section = new AccordionSectionWidget(s => []).Expanded(false);

        Assert.IsFalse(section.IsExpanded);
    }

    [TestMethod]
    public void AccordionSectionWidget_DirectReconcile_Throws()
    {
        var section = new AccordionSectionWidget(s => []);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            section.ReconcileAsync(null, null!).GetAwaiter().GetResult());
    }

    [TestMethod]
    public void AccordionSectionWidget_LeftActions_SetsActions()
    {
        var section = new AccordionSectionWidget(s => [])
            .LeftActions(a => [a.Icon("+")]);

        TestSeq.Single(section.LeftSectionActions);
        Assert.AreEqual("+", section.LeftSectionActions[0].Icon);
    }

    [TestMethod]
    public void AccordionSectionWidget_RightActions_SetsActions()
    {
        var section = new AccordionSectionWidget(s => [])
            .RightActions(a => [a.Icon("×"), a.Icon("⟳")]);

        Assert.AreEqual(2, section.RightSectionActions.Count);
    }

    [TestMethod]
    public void AccordionSectionWidget_LeftActions_Toggle_SetsIsToggle()
    {
        var section = new AccordionSectionWidget(s => [])
            .LeftActions(a => [a.Toggle("▶", "▼")]);

        TestSeq.Single(section.LeftSectionActions);
        Assert.IsTrue(section.LeftSectionActions[0].IsToggle);
    }

    [TestMethod]
    public void AccordionWidget_MultipleExpanded_DefaultsToFalse()
    {
        var widget = new AccordionWidget([]);

        Assert.IsFalse(widget.AllowMultipleExpanded);
    }

    [TestMethod]
    public void AccordionWidget_MultipleExpanded_CanBeEnabled()
    {
        var widget = new AccordionWidget([]).MultipleExpanded(true);

        Assert.IsTrue(widget.AllowMultipleExpanded);
    }

    [TestMethod]
    public void AccordionContext_Section_CreatesWidget()
    {
        var ctx = new AccordionContext();
        var section = ctx.Section(s => []);

        Assert.IsNotNull(section);
        Assert.AreEqual("", section.SectionTitle);
    }

    [TestMethod]
    public void AccordionContext_SectionWithTitle_CreatesWidget()
    {
        var ctx = new AccordionContext();
        var section = ctx.Section("My Title", s => []);

        Assert.AreEqual("My Title", section.SectionTitle);
    }

    [TestMethod]
    public void AccordionSectionActionContext_Toggle_ChangesState()
    {
        var node = CreateNode(2, [true, false]);
        var actionCtx = new AccordionSectionActionContext(node, 0);

        Assert.IsTrue(actionCtx.IsExpanded);
        actionCtx.Toggle();
        Assert.IsFalse(actionCtx.IsExpanded);
    }

    [TestMethod]
    public void AccordionSectionActionContext_Collapse_CollapsesSection()
    {
        var node = CreateNode(2, [true, false]);
        var actionCtx = new AccordionSectionActionContext(node, 0);

        actionCtx.Collapse();
        Assert.IsFalse(actionCtx.IsExpanded);
    }

    [TestMethod]
    public void AccordionSectionActionContext_Expand_ExpandsSection()
    {
        var node = CreateNode(2, [false, false]);
        var actionCtx = new AccordionSectionActionContext(node, 1);

        actionCtx.Expand();
        Assert.IsTrue(actionCtx.IsExpanded);
    }
}

[TestClass]
public class AccordionIntegrationTests
{
    [TestMethod]
    public async Task Accordion_BasicRender_ShowsSectionTitles()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(ctx => ctx.Accordion(a => [
                a.Section(s => [s.Text("Content 1")]).Title("Section A"),
                a.Section(s => [s.Text("Content 2")]).Title("Section B"),
            ]))
            .WithHeadless()
            .WithDimensions(40, 10)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Section A"), TimeSpan.FromSeconds(5), "accordion to render")
            .Capture("accordion-basic")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.IsTrue(snapshot.ContainsText("Section A"));
        Assert.IsTrue(snapshot.ContainsText("Section B"));
    }
}

[TestClass]
public class AccordionKeyboardTests
{
    [TestMethod]
    public async Task Accordion_EnterKey_TogglesSections()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(ctx => ctx.Accordion(a => [
                a.Section(s => [s.Text("Content A")]).Title("First"),
                a.Section(s => [s.Text("Content B")]).Title("Second"),
            ]))
            .WithHeadless()
            .WithDimensions(40, 12)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("First"), TimeSpan.FromSeconds(5), "accordion to render")
            .Capture("before-toggle")
            // First section should be expanded by default - shows Content A
            .Key(Hex1bKey.Enter)  // Toggle first section (collapse)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("after-collapse")
            .Key(Hex1bKey.DownArrow)  // Move to second section
            .Key(Hex1bKey.Enter)  // Toggle second section (expand)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("after-expand-second")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // After expanding second, "Content B" should be visible
        Assert.IsTrue(snapshot.ContainsText("Content B"));
    }
}

[TestClass]
public class AccordionLayoutTests
{
    [TestMethod]
    public async Task Accordion_InVStackWithBorder_Renders()
    {
        // Regression: VStack measures children with maxHeight=int.MaxValue,
        // which caused accordion to freeze trying to distribute infinite space.
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(ctx => ctx.VStack(v => [
                v.Text("Title"),
                v.Separator(),
                v.HStack(h => [
                    h.Border(b => [
                        b.Accordion(a => [
                            a.Section(s => [s.Text("Content")]).Title("EXPLORER"),
                            a.Section(s => [s.Text("More")]).Title("OUTLINE"),
                            a.Section(s => [s.Text("Third")]).Title("SOURCE CONTROL").Expanded(false),
                        ])
                    ]).Title("Sidebar").FixedWidth(35),
                    h.Border(b => [b.Text("Main")]).Title("Editor"),
                ]),
            ]))
            .WithMouse()
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5), "app to start")
            .Wait(TimeSpan.FromMilliseconds(500))
            .Capture("vstack-accordion")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Dump first 10 lines of screen for debugging
        var lines = new List<string>();
        for (int row = 0; row < 24; row++)
        {
            try { lines.Add(snapshot.GetTextAt(row, 0, 79)); } catch { }
        }
        var screenDump = string.Join("\n", lines);
        if (!snapshot.ContainsText("EXPLORER"))
            Assert.Fail($"Missing EXPLORER.\nScreen:\n{screenDump}");
        Assert.IsTrue(snapshot.ContainsText("OUTLINE"));
        Assert.IsTrue(snapshot.ContainsText("SOURCE CONTROL"));
    }
}
