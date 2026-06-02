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
[TestClass]
public class TreeNodeTests
{
    #region Helper Methods

    private static TreeWidget CreateSimpleTree()
    {
        return new TreeWidget([
            new TreeItemWidget("Root 1") { 
                ChildItems = [
                    new TreeItemWidget("Child 1.1"),
                    new TreeItemWidget("Child 1.2") {
                        ChildItems = [
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

    [TestMethod]
    public async Task Reconcile_CreatesTreeNode()
    {
        var widget = CreateSimpleTree();
        
        var node = await ReconcileTreeAsync(widget);
        
        Assert.IsNotNull(node);
        TestSeq.IsType<TreeNode>(node);
    }

    [TestMethod]
    public async Task Reconcile_CreatesCorrectItemCount()
    {
        var widget = CreateSimpleTree();
        
        var node = await ReconcileTreeAsync(widget);
        
        Assert.AreEqual(2, node.Items.Count); // 2 root items
        Assert.AreEqual(2, node.Items[0].Children.Count); // Root 1 has 2 children
        TestSeq.Single(node.Items[0].Children[1].Children); // Child 1.2 has 1 grandchild
    }

    [TestMethod]
    public async Task Reconcile_SetsLabelsCorrectly()
    {
        var widget = CreateSimpleTree();
        
        var node = await ReconcileTreeAsync(widget);
        
        Assert.AreEqual("Root 1", node.Items[0].Label);
        Assert.AreEqual("Root 2", node.Items[1].Label);
        Assert.AreEqual("Child 1.1", node.Items[0].Children[0].Label);
    }

    [TestMethod]
    public async Task Reconcile_SetsIsExpandedCorrectly()
    {
        var widget = CreateSimpleTree();
        
        var node = await ReconcileTreeAsync(widget);
        
        Assert.IsTrue(node.Items[0].IsExpanded); // Root 1 is expanded
        Assert.IsFalse(node.Items[1].IsExpanded); // Root 2 is not expanded
    }

    [TestMethod]
    public async Task Reconcile_SetsIsLastChildCorrectly()
    {
        var widget = CreateSimpleTree();
        
        var node = await ReconcileTreeAsync(widget);
        
        Assert.IsFalse(node.Items[0].IsLastChild); // Root 1 is not last
        Assert.IsTrue(node.Items[1].IsLastChild); // Root 2 is last
    }

    #endregion

    #region Flattened View Tests

    [TestMethod]
    public async Task RebuildFlattenedView_IncludesExpandedItems()
    {
        var widget = CreateSimpleTree();
        var node = await ReconcileTreeAsync(widget);
        
        // Root 1 is expanded, should show: Root 1, Child 1.1, Child 1.2, Root 2
        // (Grandchild not shown because Child 1.2 is not expanded)
        Assert.AreEqual(4, node.FlattenedItems.Count);
    }

    [TestMethod]
    public async Task RebuildFlattenedView_ExcludesCollapsedChildren()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Root") { 
                ChildItems = [new TreeItemWidget("Child")],
                HasChildren = true,
                IsExpanded = false // Collapsed
            }
        ]);
        
        var node = await ReconcileTreeAsync(widget);
        
        TestSeq.Single(node.FlattenedItems); // Only Root visible
    }

    [TestMethod]
    public async Task RebuildFlattenedView_SetsDepthCorrectly()
    {
        var widget = CreateSimpleTree();
        var node = await ReconcileTreeAsync(widget);
        
        Assert.AreEqual(0, node.FlattenedItems[0].Depth); // Root 1
        Assert.AreEqual(1, node.FlattenedItems[1].Depth); // Child 1.1
        Assert.AreEqual(1, node.FlattenedItems[2].Depth); // Child 1.2
        Assert.AreEqual(0, node.FlattenedItems[3].Depth); // Root 2
    }

    #endregion

    #region Measure Tests

    [TestMethod]
    public async Task Measure_ReturnsCorrectHeight()
    {
        var widget = CreateSimpleTree();
        var node = await ReconcileTreeAsync(widget);
        
        var size = node.Measure(Constraints.Unbounded);
        
        // 4 visible items
        Assert.AreEqual(4, size.Height);
    }

    [TestMethod]
    public async Task Measure_ReturnsCorrectWidth()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Short"),
            new TreeItemWidget("Much Longer Label")
        ]);
        var node = await ReconcileTreeAsync(widget);
        
        var size = node.Measure(Constraints.Unbounded);
        
        // Width = indicator (2) + longest label
        Assert.IsTrue(size.Width >= "Much Longer Label".Length + 2);
    }

    [TestMethod]
    public async Task Measure_EmptyTree_ReturnsZeroHeight()
    {
        var widget = new TreeWidget([]);
        var node = await ReconcileTreeAsync(widget);
        
        var size = node.Measure(Constraints.Unbounded);
        
        Assert.AreEqual(0, size.Height);
    }

    [TestMethod]
    public async Task Measure_IncludesGuideWidth()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Root") { 
                ChildItems = [
                    new TreeItemWidget("Child") {
                        ChildItems = [new TreeItemWidget("Grandchild")],
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
        Assert.IsTrue(size.Width >= "Grandchild".Length + 2 + 6);
    }

    #endregion

    #region Focus Navigation Tests

    [TestMethod]
    public async Task FirstItem_HasInitialFocus()
    {
        var widget = CreateSimpleTree();
        var node = await ReconcileTreeAsync(widget);
        
        Assert.IsTrue(node.FlattenedItems[0].Node.IsFocused);
        Assert.IsFalse(node.FlattenedItems[1].Node.IsFocused);
    }

    #endregion

    #region Multi-Select Tests

    [TestMethod]
    public async Task MultiSelect_SetsToggleSelectCallback()
    {
        var widget = CreateSimpleTree().MultiSelect();
        var node = await ReconcileTreeAsync(widget);
        
        // All items should have ToggleSelectCallback set
        Assert.IsNotNull(node.Items[0].ToggleSelectCallback);
    }

    [TestMethod]
    public async Task GetSelectedItems_ReturnsSelectedItems()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Selected") { IsSelected = true },
            new TreeItemWidget("Not Selected")
        ]).MultiSelect();
        var node = await ReconcileTreeAsync(widget);
        
        var selected = node.GetSelectedItems();
        
        TestSeq.Single(selected);
        Assert.AreEqual("Selected", selected[0].Label);
    }

    #endregion

    #region Icon Tests

    [TestMethod]
    public async Task TreeItem_WithIcon_SetsIconProperty()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Folder").Icon("📁")
        ]);
        var node = await ReconcileTreeAsync(widget);
        
        Assert.AreEqual("📁", node.Items[0].Icon);
    }

    [TestMethod]
    public async Task GetDisplayText_WithIcon_IncludesIcon()
    {
        var item = new TreeItemNode { Label = "Test", Icon = "📁" };
        
        var displayText = item.GetDisplayText();
        
        Assert.AreEqual("📁 Test", displayText);
    }

    [TestMethod]
    public async Task GetDisplayText_WithoutIcon_ReturnsLabel()
    {
        var item = new TreeItemNode { Label = "Test" };
        
        var displayText = item.GetDisplayText();
        
        Assert.AreEqual("Test", displayText);
    }

    #endregion

    #region GuideStyle Tests

    #endregion

    #region TreeItemWidget Fluent API Tests

    [TestMethod]
    public void TreeItemWidget_FluentApi_ChainsCorrectly()
    {
        var item = new TreeItemWidget("Test")
            .Icon("📁")
            .Expanded(true)
            .Selected(true)
            .Data("user-data");
        
        Assert.AreEqual("Test", item.Label);
        Assert.AreEqual("📁", item.IconValue);
        Assert.IsTrue(item.IsExpanded);
        Assert.IsTrue(item.IsSelected);
        Assert.AreEqual("user-data", item.DataValue);
        Assert.AreEqual(typeof(string), item.DataType);
    }

    [TestMethod]
    public async Task TreeItemNode_GetData_ReturnsTypedData()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Test").Data("my-data")
        ]);
        
        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as TreeNode;
        
        var itemNode = node!.Items[0];
        var data = itemNode.GetData<string>();
        
        Assert.AreEqual("my-data", data);
    }

    [TestMethod]
    public async Task TreeItemNode_GetData_ThrowsOnTypeMismatch()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Test").Data("string-data")
        ]);
        
        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as TreeNode;
        
        var itemNode = node!.Items[0];
        
        Assert.ThrowsExactly<InvalidCastException>(() => itemNode.GetData<int>());
    }

    [TestMethod]
    public async Task TreeItemNode_TryGetData_ReturnsFalseOnTypeMismatch()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Test").Data("string-data")
        ]);
        
        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as TreeNode;
        
        var itemNode = node!.Items[0];
        
        Assert.IsFalse(itemNode.TryGetData<int>(out _));
        Assert.IsTrue(itemNode.TryGetData<string>(out var data));
        Assert.AreEqual("string-data", data);
    }

    [TestMethod]
    public void TreeItemWidget_WithChildren_SetsHasChildren()
    {
        var child = new TreeItemWidget("Child");
        var parent = new TreeItemWidget("Parent").Children(child);
        
        Assert.IsTrue(parent.HasChildren);
        TestSeq.Single(parent.ChildItems);
    }

    [TestMethod]
    public void TreeItemWidget_OnExpanding_SetsHasChildren()
    {
        var item = new TreeItemWidget("Lazy")
            .OnExpanding(_ => [new TreeItemWidget("Loaded")]);
        
        Assert.IsTrue(item.HasChildren);
        Assert.IsNotNull(item.ExpandingHandler);
    }

    #endregion

    #region CanExpand Tests

    [TestMethod]
    public void TreeItemNode_CanExpand_TrueWhenHasChildren()
    {
        var node = new TreeItemNode { HasChildren = true };
        Assert.IsTrue(node.CanExpand);
    }

    [TestMethod]
    public void TreeItemNode_CanExpand_TrueWhenHasActualChildren()
    {
        var node = new TreeItemNode { Children = [new TreeItemNode { Label = "Child" }] };
        Assert.IsTrue(node.CanExpand);
    }

    [TestMethod]
    public void TreeItemNode_CanExpand_FalseWhenNoChildren()
    {
        var node = new TreeItemNode();
        Assert.IsFalse(node.CanExpand);
    }

    #endregion

    #region Input Binding Tests

    [TestMethod]
    public async Task TreeNode_HasInputBindings()
    {
        var widget = CreateSimpleTree();
        var node = await ReconcileTreeAsync(widget);
        
        var builder = node.BuildBindings();
        var bindings = builder.Build();
        
        // Should have Up, Down, Left, Right, Enter, Space bindings at minimum
        Assert.IsTrue(bindings.Count >= 6, $"Expected at least 6 bindings, got {bindings.Count}");
    }

    [TestMethod]
    public async Task TreeNode_Expand_ChangesIsExpanded()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Parent").Children(
                new TreeItemWidget("Child")
            )
        ]);
        var node = await ReconcileTreeAsync(widget);
        
        // Initially collapsed
        Assert.IsFalse(node.Items[0].IsExpanded);
        TestSeq.Single(node.FlattenedItems); // Only Parent visible
        
        // Toggle expand
        var focusRing = new FocusRing();
        var ctx = new InputBindingActionContext(focusRing, null, default);
        await node.ToggleExpandAsync(node.Items[0], ctx);
        
        // Now expanded
        Assert.IsTrue(node.Items[0].IsExpanded);
        Assert.AreEqual(2, node.FlattenedItems.Count); // Parent and Child visible
    }

    [TestMethod]
    public async Task TreeNode_Collapse_ChangesIsExpanded()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Parent").Expanded().Children(
                new TreeItemWidget("Child")
            )
        ]);
        var node = await ReconcileTreeAsync(widget);
        
        // Initially expanded
        Assert.IsTrue(node.Items[0].IsExpanded);
        Assert.AreEqual(2, node.FlattenedItems.Count); // Parent and Child visible
        
        // Toggle collapse
        var focusRing = new FocusRing();
        var ctx = new InputBindingActionContext(focusRing, null, default);
        await node.ToggleExpandAsync(node.Items[0], ctx);
        
        // Now collapsed
        Assert.IsFalse(node.Items[0].IsExpanded);
        TestSeq.Single(node.FlattenedItems); // Only Parent visible
    }

    #endregion
}
