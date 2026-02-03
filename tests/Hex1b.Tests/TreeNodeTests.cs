using Hex1b;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for TreeNode and TreeItemNode behavior.
/// </summary>
public class TreeNodeTests
{
    #region Helper Methods

    private static TreeWidget CreateSimpleTree()
    {
        return new TreeWidget([
            new TreeItemWidget("Root 1") { 
                Children = [
                    new TreeItemWidget("Child 1.1"),
                    new TreeItemWidget("Child 1.2") {
                        Children = [
                            new TreeItemWidget("Grandchild 1.2.1")
                        ],
                        HasChildren = true
                    }
                ],
                HasChildren = true,
                IsExpanded = true
            },
            new TreeItemWidget("Root 2")
        ]);
    }

    private static async Task<TreeNode> ReconcileTreeAsync(TreeWidget widget)
    {
        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as TreeNode;
        return node!;
    }

    #endregion

    #region Reconciliation Tests

    [Fact]
    public async Task Reconcile_CreatesTreeNode()
    {
        var widget = CreateSimpleTree();
        
        var node = await ReconcileTreeAsync(widget);
        
        Assert.NotNull(node);
        Assert.IsType<TreeNode>(node);
    }

    [Fact]
    public async Task Reconcile_CreatesCorrectItemCount()
    {
        var widget = CreateSimpleTree();
        
        var node = await ReconcileTreeAsync(widget);
        
        Assert.Equal(2, node.Items.Count); // 2 root items
        Assert.Equal(2, node.Items[0].Children.Count); // Root 1 has 2 children
        Assert.Single(node.Items[0].Children[1].Children); // Child 1.2 has 1 grandchild
    }

    [Fact]
    public async Task Reconcile_SetsLabelsCorrectly()
    {
        var widget = CreateSimpleTree();
        
        var node = await ReconcileTreeAsync(widget);
        
        Assert.Equal("Root 1", node.Items[0].Label);
        Assert.Equal("Root 2", node.Items[1].Label);
        Assert.Equal("Child 1.1", node.Items[0].Children[0].Label);
    }

    [Fact]
    public async Task Reconcile_SetsIsExpandedCorrectly()
    {
        var widget = CreateSimpleTree();
        
        var node = await ReconcileTreeAsync(widget);
        
        Assert.True(node.Items[0].IsExpanded); // Root 1 is expanded
        Assert.False(node.Items[1].IsExpanded); // Root 2 is not expanded
    }

    [Fact]
    public async Task Reconcile_SetsIsLastChildCorrectly()
    {
        var widget = CreateSimpleTree();
        
        var node = await ReconcileTreeAsync(widget);
        
        Assert.False(node.Items[0].IsLastChild); // Root 1 is not last
        Assert.True(node.Items[1].IsLastChild); // Root 2 is last
    }

    #endregion

    #region Flattened View Tests

    [Fact]
    public async Task RebuildFlattenedView_IncludesExpandedItems()
    {
        var widget = CreateSimpleTree();
        var node = await ReconcileTreeAsync(widget);
        
        // Root 1 is expanded, should show: Root 1, Child 1.1, Child 1.2, Root 2
        // (Grandchild not shown because Child 1.2 is not expanded)
        Assert.Equal(4, node.FlattenedItems.Count);
    }

    [Fact]
    public async Task RebuildFlattenedView_ExcludesCollapsedChildren()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Root") { 
                Children = [new TreeItemWidget("Child")],
                HasChildren = true,
                IsExpanded = false // Collapsed
            }
        ]);
        
        var node = await ReconcileTreeAsync(widget);
        
        Assert.Single(node.FlattenedItems); // Only Root visible
    }

    [Fact]
    public async Task RebuildFlattenedView_SetsDepthCorrectly()
    {
        var widget = CreateSimpleTree();
        var node = await ReconcileTreeAsync(widget);
        
        Assert.Equal(0, node.FlattenedItems[0].Depth); // Root 1
        Assert.Equal(1, node.FlattenedItems[1].Depth); // Child 1.1
        Assert.Equal(1, node.FlattenedItems[2].Depth); // Child 1.2
        Assert.Equal(0, node.FlattenedItems[3].Depth); // Root 2
    }

    #endregion

    #region Measure Tests

    [Fact]
    public async Task Measure_ReturnsCorrectHeight()
    {
        var widget = CreateSimpleTree();
        var node = await ReconcileTreeAsync(widget);
        
        var size = node.Measure(Constraints.Unbounded);
        
        // 4 visible items
        Assert.Equal(4, size.Height);
    }

    [Fact]
    public async Task Measure_ReturnsCorrectWidth()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Short"),
            new TreeItemWidget("Much Longer Label")
        ]);
        var node = await ReconcileTreeAsync(widget);
        
        var size = node.Measure(Constraints.Unbounded);
        
        // Width = indicator (2) + longest label
        Assert.True(size.Width >= "Much Longer Label".Length + 2);
    }

    [Fact]
    public async Task Measure_EmptyTree_ReturnsZeroHeight()
    {
        var widget = new TreeWidget([]);
        var node = await ReconcileTreeAsync(widget);
        
        var size = node.Measure(Constraints.Unbounded);
        
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public async Task Measure_IncludesGuideWidth()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Root") { 
                Children = [
                    new TreeItemWidget("Child") {
                        Children = [new TreeItemWidget("Grandchild")],
                        HasChildren = true,
                        IsExpanded = true
                    }
                ],
                HasChildren = true,
                IsExpanded = true
            }
        ]);
        var node = await ReconcileTreeAsync(widget);
        
        var size = node.Measure(Constraints.Unbounded);
        
        // Grandchild at depth 2 should add 6 chars for guides (2 levels * 3 chars each)
        Assert.True(size.Width >= "Grandchild".Length + 2 + 6);
    }

    #endregion

    #region Focus Navigation Tests

    [Fact]
    public async Task FirstItem_HasInitialFocus()
    {
        var widget = CreateSimpleTree();
        var node = await ReconcileTreeAsync(widget);
        
        Assert.True(node.FlattenedItems[0].Node.IsFocused);
        Assert.False(node.FlattenedItems[1].Node.IsFocused);
    }

    #endregion

    #region Multi-Select Tests

    [Fact]
    public async Task MultiSelect_SetsToggleSelectCallback()
    {
        var widget = CreateSimpleTree().WithMultiSelect(true);
        var node = await ReconcileTreeAsync(widget);
        
        // All items should have ToggleSelectCallback set
        Assert.NotNull(node.Items[0].ToggleSelectCallback);
    }

    [Fact]
    public async Task GetSelectedItems_ReturnsSelectedItems()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Selected") { IsSelected = true },
            new TreeItemWidget("Not Selected")
        ]).WithMultiSelect(true);
        var node = await ReconcileTreeAsync(widget);
        
        var selected = node.GetSelectedItems();
        
        Assert.Single(selected);
        Assert.Equal("Selected", selected[0].Label);
    }

    #endregion

    #region Icon Tests

    [Fact]
    public async Task TreeItem_WithIcon_SetsIconProperty()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Folder").Icon("📁")
        ]);
        var node = await ReconcileTreeAsync(widget);
        
        Assert.Equal("📁", node.Items[0].Icon);
    }

    [Fact]
    public async Task GetDisplayText_WithIcon_IncludesIcon()
    {
        var item = new TreeItemNode { Label = "Test", Icon = "📁" };
        
        var displayText = item.GetDisplayText();
        
        Assert.Equal("📁 Test", displayText);
    }

    [Fact]
    public async Task GetDisplayText_WithoutIcon_ReturnsLabel()
    {
        var item = new TreeItemNode { Label = "Test" };
        
        var displayText = item.GetDisplayText();
        
        Assert.Equal("Test", displayText);
    }

    #endregion

    #region GuideStyle Tests

    #endregion

    #region TreeItemWidget Fluent API Tests

    [Fact]
    public void TreeItemWidget_FluentApi_ChainsCorrectly()
    {
        var item = new TreeItemWidget("Test")
            .Icon("📁")
            .Expanded(true)
            .Selected(true)
            .WithTag("user-data");
        
        Assert.Equal("Test", item.Label);
        Assert.Equal("📁", item.IconValue);
        Assert.True(item.IsExpanded);
        Assert.True(item.IsSelected);
        Assert.Equal("user-data", item.Tag);
    }

    [Fact]
    public void TreeItemWidget_WithChildren_SetsHasChildren()
    {
        var child = new TreeItemWidget("Child");
        var parent = new TreeItemWidget("Parent").WithChildren(child);
        
        Assert.True(parent.HasChildren);
        Assert.Single(parent.Children);
    }

    [Fact]
    public void TreeItemWidget_OnExpanding_SetsHasChildren()
    {
        var item = new TreeItemWidget("Lazy")
            .OnExpanding(_ => [new TreeItemWidget("Loaded")]);
        
        Assert.True(item.HasChildren);
        Assert.NotNull(item.ExpandingHandler);
    }

    #endregion

    #region CanExpand Tests

    [Fact]
    public void TreeItemNode_CanExpand_TrueWhenHasChildren()
    {
        var node = new TreeItemNode { HasChildren = true };
        Assert.True(node.CanExpand);
    }

    [Fact]
    public void TreeItemNode_CanExpand_TrueWhenHasActualChildren()
    {
        var node = new TreeItemNode { Children = [new TreeItemNode { Label = "Child" }] };
        Assert.True(node.CanExpand);
    }

    [Fact]
    public void TreeItemNode_CanExpand_FalseWhenNoChildren()
    {
        var node = new TreeItemNode();
        Assert.False(node.CanExpand);
    }

    #endregion

    #region Input Binding Tests

    [Fact]
    public async Task TreeNode_HasInputBindings()
    {
        var widget = CreateSimpleTree();
        var node = await ReconcileTreeAsync(widget);
        
        var builder = node.BuildBindings();
        var bindings = builder.Build();
        
        // Should have Up, Down, Left, Right, Enter, Space bindings at minimum
        Assert.True(bindings.Count >= 6, $"Expected at least 6 bindings, got {bindings.Count}");
    }

    [Fact]
    public async Task TreeNode_Expand_ChangesIsExpanded()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Parent").WithChildren(
                new TreeItemWidget("Child")
            )
        ]);
        var node = await ReconcileTreeAsync(widget);
        
        // Initially collapsed
        Assert.False(node.Items[0].IsExpanded);
        Assert.Single(node.FlattenedItems); // Only Parent visible
        
        // Toggle expand
        var focusRing = new FocusRing();
        var ctx = new InputBindingActionContext(focusRing, null, default);
        await node.ToggleExpandAsync(node.Items[0], ctx);
        
        // Now expanded
        Assert.True(node.Items[0].IsExpanded);
        Assert.Equal(2, node.FlattenedItems.Count); // Parent and Child visible
    }

    [Fact]
    public async Task TreeNode_Collapse_ChangesIsExpanded()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Parent").Expanded().WithChildren(
                new TreeItemWidget("Child")
            )
        ]);
        var node = await ReconcileTreeAsync(widget);
        
        // Initially expanded
        Assert.True(node.Items[0].IsExpanded);
        Assert.Equal(2, node.FlattenedItems.Count); // Parent and Child visible
        
        // Toggle collapse
        var focusRing = new FocusRing();
        var ctx = new InputBindingActionContext(focusRing, null, default);
        await node.ToggleExpandAsync(node.Items[0], ctx);
        
        // Now collapsed
        Assert.False(node.Items[0].IsExpanded);
        Assert.Single(node.FlattenedItems); // Only Parent visible
    }

    #endregion
}
