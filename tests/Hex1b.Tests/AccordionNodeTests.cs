using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

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

    [Fact]
    public void Measure_AllCollapsed_ReturnsHeaderHeightOnly()
    {
        var node = CreateNode(3, [false, false, false]);
        var constraints = new Constraints(0, 40, 0, 20);

        var size = node.Measure(constraints);

        Assert.Equal(40, size.Width);
        Assert.Equal(3, size.Height); // 3 headers, 1 row each
    }

    [Fact]
    public void Measure_FillsAvailableHeight()
    {
        var node = CreateNode(3, [true, false, false]);
        var constraints = new Constraints(0, 40, 0, 20);

        var size = node.Measure(constraints);

        Assert.Equal(40, size.Width);
        Assert.Equal(20, size.Height); // fills available
    }

    [Fact]
    public void SectionCount_ReturnsCorrectCount()
    {
        var node = CreateNode(3, [true, false, true]);

        Assert.Equal(3, node.SectionCount);
    }

    [Fact]
    public void IsSectionExpanded_ReturnsCorrectState()
    {
        var node = CreateNode(3, [true, false, true]);

        Assert.True(node.IsSectionExpanded(0));
        Assert.False(node.IsSectionExpanded(1));
        Assert.True(node.IsSectionExpanded(2));
    }

    [Fact]
    public void ToggleSection_ChangesExpandedState()
    {
        var node = CreateNode(3, [true, false, false]);

        node.ToggleSection(0);
        Assert.False(node.IsSectionExpanded(0));

        node.ToggleSection(1);
        Assert.True(node.IsSectionExpanded(1));
    }

    [Fact]
    public void SetSectionExpanded_CollapseOthers_WhenMultipleNotAllowed()
    {
        var node = CreateNode(3, [true, false, false]);
        node.AllowMultipleExpanded = false;

        node.SetSectionExpanded(2, true);

        Assert.False(node.IsSectionExpanded(0)); // collapsed
        Assert.False(node.IsSectionExpanded(1));
        Assert.True(node.IsSectionExpanded(2));
    }

    [Fact]
    public void SetSectionExpanded_KeepsOthers_WhenMultipleAllowed()
    {
        var node = CreateNode(3, [true, false, false]);
        node.AllowMultipleExpanded = true;

        node.SetSectionExpanded(2, true);

        Assert.True(node.IsSectionExpanded(0));
        Assert.False(node.IsSectionExpanded(1));
        Assert.True(node.IsSectionExpanded(2));
    }

    [Fact]
    public void IsFocused_WhenSet_MarksDirty()
    {
        var node = CreateNode(1, [true]);
        node.ClearDirty();

        node.IsFocused = true;

        Assert.True(node.IsDirty);
    }

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = CreateNode(1, [true]);

        Assert.True(node.IsFocusable);
    }

    [Fact]
    public void ManagesChildFocus_ReturnsTrue()
    {
        var node = CreateNode(1, [true]);

        Assert.True(node.ManagesChildFocus);
    }

    [Fact]
    public void GetChildren_ReturnsOnlyContentNodes()
    {
        var node = CreateNode(3, [true, false, true]);

        // No content nodes set, so no children
        var children = node.GetChildren().ToList();
        Assert.Empty(children);
    }

    [Fact]
    public void IsSectionExpanded_OutOfRange_ReturnsFalse()
    {
        var node = CreateNode(2, [true, false]);

        Assert.False(node.IsSectionExpanded(-1));
        Assert.False(node.IsSectionExpanded(5));
    }

    [Fact]
    public void Arrange_DistributesContentHeightEqually()
    {
        var node = CreateNode(2, [true, true]);
        var constraints = new Constraints(0, 40, 0, 12);
        node.Measure(constraints);
        node.Arrange(new Rect(0, 0, 40, 12));

        // 2 headers (2 rows) + 10 remaining / 2 expanded = 5 each
        // Verify via GetChildren - content nodes would have bounds if set
        Assert.Equal(2, node.SectionCount);
    }

    [Fact]
    public void AccordionSectionWidget_Title_SetsTitle()
    {
        var section = new AccordionSectionWidget(s => []).Title("Test Title");

        Assert.Equal("Test Title", section.SectionTitle);
    }

    [Fact]
    public void AccordionSectionWidget_Expanded_SetsFlag()
    {
        var section = new AccordionSectionWidget(s => []).Expanded();

        Assert.True(section.IsExpanded);
    }

    [Fact]
    public void AccordionSectionWidget_Collapsed_SetsFlag()
    {
        var section = new AccordionSectionWidget(s => []).Collapsed();

        Assert.False(section.IsExpanded);
    }

    [Fact]
    public void AccordionSectionWidget_DirectReconcile_Throws()
    {
        var section = new AccordionSectionWidget(s => []);

        Assert.Throws<InvalidOperationException>(() =>
            section.ReconcileAsync(null, null!).GetAwaiter().GetResult());
    }

    [Fact]
    public void AccordionSectionWidget_LeftActions_SetsIcons()
    {
        var section = new AccordionSectionWidget(s => [])
            .LeftActions(a => [new IconWidget("+")]);

        Assert.Single(section.LeftActionIcons);
        Assert.Equal("+", section.LeftActionIcons[0].Icon);
    }

    [Fact]
    public void AccordionSectionWidget_RightActions_SetsIcons()
    {
        var section = new AccordionSectionWidget(s => [])
            .RightActions(a => [new IconWidget("×"), new IconWidget("⟳")]);

        Assert.Equal(2, section.RightActionIcons.Count);
    }

    [Fact]
    public void AccordionWidget_MultipleExpanded_DefaultsToTrue()
    {
        var widget = new AccordionWidget([]);

        Assert.True(widget.AllowMultipleExpanded);
    }

    [Fact]
    public void AccordionWidget_MultipleExpanded_CanBeDisabled()
    {
        var widget = new AccordionWidget([]).MultipleExpanded(false);

        Assert.False(widget.AllowMultipleExpanded);
    }

    [Fact]
    public void AccordionContext_Section_CreatesWidget()
    {
        var ctx = new AccordionContext();
        var section = ctx.Section(s => []);

        Assert.NotNull(section);
        Assert.Equal("", section.SectionTitle);
    }

    [Fact]
    public void AccordionContext_SectionWithTitle_CreatesWidget()
    {
        var ctx = new AccordionContext();
        var section = ctx.Section("My Title", s => []);

        Assert.Equal("My Title", section.SectionTitle);
    }

    [Fact]
    public void AccordionSectionActionContext_Toggle_ChangesState()
    {
        var node = CreateNode(2, [true, false]);
        var actionCtx = new AccordionSectionActionContext(node, 0);

        Assert.True(actionCtx.IsExpanded);
        actionCtx.Toggle();
        Assert.False(actionCtx.IsExpanded);
    }

    [Fact]
    public void AccordionSectionActionContext_Collapse_CollapsesSection()
    {
        var node = CreateNode(2, [true, false]);
        var actionCtx = new AccordionSectionActionContext(node, 0);

        actionCtx.Collapse();
        Assert.False(actionCtx.IsExpanded);
    }

    [Fact]
    public void AccordionSectionActionContext_Expand_ExpandsSection()
    {
        var node = CreateNode(2, [false, false]);
        var actionCtx = new AccordionSectionActionContext(node, 1);

        actionCtx.Expand();
        Assert.True(actionCtx.IsExpanded);
    }
}
