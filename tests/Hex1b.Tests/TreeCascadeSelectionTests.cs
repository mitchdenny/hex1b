using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for TreeWidget cascade selection functionality.
/// </summary>
public class TreeCascadeSelectionTests
{
    #region ComputeSelectionState Tests

    [Fact]
    public void ComputeSelectionState_LeafNode_ReturnsSelectedWhenSelected()
    {
        var node = new TreeItemNode { Label = "Leaf", IsSelected = true };
        
        Assert.Equal(TreeSelectionState.Selected, node.ComputeSelectionState());
    }

    [Fact]
    public void ComputeSelectionState_LeafNode_ReturnsNoneWhenNotSelected()
    {
        var node = new TreeItemNode { Label = "Leaf", IsSelected = false };
        
        Assert.Equal(TreeSelectionState.None, node.ComputeSelectionState());
    }

    [Fact]
    public void ComputeSelectionState_ParentWithAllChildrenSelected_ReturnsSelected()
    {
        var parent = new TreeItemNode
        {
            Label = "Parent",
            Children = [
                new TreeItemNode { Label = "Child 1", IsSelected = true },
                new TreeItemNode { Label = "Child 2", IsSelected = true }
            ]
        };
        
        Assert.Equal(TreeSelectionState.Selected, parent.ComputeSelectionState());
    }

    [Fact]
    public void ComputeSelectionState_ParentWithNoChildrenSelected_ReturnsNone()
    {
        var parent = new TreeItemNode
        {
            Label = "Parent",
            Children = [
                new TreeItemNode { Label = "Child 1", IsSelected = false },
                new TreeItemNode { Label = "Child 2", IsSelected = false }
            ]
        };
        
        Assert.Equal(TreeSelectionState.None, parent.ComputeSelectionState());
    }

    [Fact]
    public void ComputeSelectionState_ParentWithSomeChildrenSelected_ReturnsIndeterminate()
    {
        var parent = new TreeItemNode
        {
            Label = "Parent",
            Children = [
                new TreeItemNode { Label = "Child 1", IsSelected = true },
                new TreeItemNode { Label = "Child 2", IsSelected = false }
            ]
        };
        
        Assert.Equal(TreeSelectionState.Indeterminate, parent.ComputeSelectionState());
    }

    [Fact]
    public void ComputeSelectionState_GrandparentWithIndeterminateChild_ReturnsIndeterminate()
    {
        var grandparent = new TreeItemNode
        {
            Label = "Grandparent",
            Children = [
                new TreeItemNode
                {
                    Label = "Parent",
                    Children = [
                        new TreeItemNode { Label = "Child 1", IsSelected = true },
                        new TreeItemNode { Label = "Child 2", IsSelected = false }
                    ]
                }
            ]
        };
        
        Assert.Equal(TreeSelectionState.Indeterminate, grandparent.ComputeSelectionState());
    }

    [Fact]
    public void ComputeSelectionState_DeepTree_AllSelected_ReturnsSelected()
    {
        var root = new TreeItemNode
        {
            Label = "Root",
            Children = [
                new TreeItemNode
                {
                    Label = "Level 1",
                    Children = [
                        new TreeItemNode
                        {
                            Label = "Level 2",
                            Children = [
                                new TreeItemNode { Label = "Leaf", IsSelected = true }
                            ]
                        }
                    ]
                }
            ]
        };
        
        Assert.Equal(TreeSelectionState.Selected, root.ComputeSelectionState());
    }

    #endregion

    #region SetSelectionCascade Tests

    [Fact]
    public void SetSelectionCascade_SelectsAllDescendants()
    {
        var parent = new TreeItemNode
        {
            Label = "Parent",
            IsSelected = false,
            Children = [
                new TreeItemNode
                {
                    Label = "Child 1",
                    IsSelected = false,
                    Children = [
                        new TreeItemNode { Label = "Grandchild", IsSelected = false }
                    ]
                },
                new TreeItemNode { Label = "Child 2", IsSelected = false }
            ]
        };
        
        parent.SetSelectionCascade(true);
        
        Assert.True(parent.IsSelected);
        Assert.True(parent.Children[0].IsSelected);
        Assert.True(parent.Children[0].Children[0].IsSelected);
        Assert.True(parent.Children[1].IsSelected);
    }

    [Fact]
    public void SetSelectionCascade_DeselectsAllDescendants()
    {
        var parent = new TreeItemNode
        {
            Label = "Parent",
            IsSelected = true,
            Children = [
                new TreeItemNode
                {
                    Label = "Child 1",
                    IsSelected = true,
                    Children = [
                        new TreeItemNode { Label = "Grandchild", IsSelected = true }
                    ]
                },
                new TreeItemNode { Label = "Child 2", IsSelected = true }
            ]
        };
        
        parent.SetSelectionCascade(false);
        
        Assert.False(parent.IsSelected);
        Assert.False(parent.Children[0].IsSelected);
        Assert.False(parent.Children[0].Children[0].IsSelected);
        Assert.False(parent.Children[1].IsSelected);
    }

    #endregion

    #region ToggleSelectionAsync Tests

    [Fact]
    public async Task ToggleSelectionAsync_WithCascade_SelectsAllChildren()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Parent").WithChildren(
                new TreeItemWidget("Child 1"),
                new TreeItemWidget("Child 2")
            )
        ]).WithCascadeSelection();
        
        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as TreeNode;
        
        // Initially nothing selected
        Assert.Equal(TreeSelectionState.None, node!.Items[0].ComputeSelectionState());
        
        // Toggle selection on parent
        var focusRing = new FocusRing();
        var ctx = new InputBindingActionContext(focusRing, null, default);
        await node.ToggleSelectionAsync(node.Items[0], ctx);
        
        // All should be selected
        Assert.True(node.Items[0].IsSelected);
        Assert.True(node.Items[0].Children[0].IsSelected);
        Assert.True(node.Items[0].Children[1].IsSelected);
        Assert.Equal(TreeSelectionState.Selected, node.Items[0].ComputeSelectionState());
    }

    [Fact]
    public async Task ToggleSelectionAsync_WithCascade_DeselectingChildMakesParentIndeterminate()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Parent").WithChildren(
                new TreeItemWidget("Child 1"),
                new TreeItemWidget("Child 2")
            )
        ]).WithCascadeSelection();
        
        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as TreeNode;
        
        // First select parent (cascades to children)
        var focusRing = new FocusRing();
        var ctx = new InputBindingActionContext(focusRing, null, default);
        await node!.ToggleSelectionAsync(node.Items[0], ctx);
        
        Assert.Equal(TreeSelectionState.Selected, node.Items[0].ComputeSelectionState());
        
        // Now deselect one child
        await node.ToggleSelectionAsync(node.Items[0].Children[0], ctx);
        
        // Child 1 should be deselected
        Assert.False(node.Items[0].Children[0].IsSelected);
        // Child 2 should still be selected
        Assert.True(node.Items[0].Children[1].IsSelected);
        // Parent should show indeterminate
        Assert.Equal(TreeSelectionState.Indeterminate, node.Items[0].ComputeSelectionState());
    }

    [Fact]
    public async Task ToggleSelectionAsync_WithoutCascade_OnlyTogglesClickedItem()
    {
        var widget = new TreeWidget([
            new TreeItemWidget("Parent").WithChildren(
                new TreeItemWidget("Child 1"),
                new TreeItemWidget("Child 2")
            )
        ]).WithMultiSelect(); // Multi-select but NOT cascade
        
        var context = ReconcileContext.CreateRoot();
        var node = await widget.ReconcileAsync(null, context) as TreeNode;
        
        // Toggle parent
        var focusRing = new FocusRing();
        var ctx = new InputBindingActionContext(focusRing, null, default);
        await node!.ToggleSelectionAsync(node.Items[0], ctx);
        
        // Only parent should be selected, not children
        Assert.True(node.Items[0].IsSelected);
        Assert.False(node.Items[0].Children[0].IsSelected);
        Assert.False(node.Items[0].Children[1].IsSelected);
    }

    #endregion

    #region Widget API Tests

    [Fact]
    public void WithCascadeSelection_EnablesMultiSelect()
    {
        var widget = new TreeWidget([]).WithCascadeSelection();
        
        Assert.True(widget.MultiSelect);
        Assert.True(widget.CascadeSelection);
    }

    [Fact]
    public void WithCascadeSelection_False_PreservesMultiSelectIfAlreadyEnabled()
    {
        var widget = new TreeWidget([]).WithMultiSelect().WithCascadeSelection(false);
        
        Assert.True(widget.MultiSelect);
        Assert.False(widget.CascadeSelection);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Tree_CascadeSelection_RendersIndeterminateCheckbox()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TreeWidget([
                new TreeItemWidget("Parent").Expanded().WithChildren(
                    new TreeItemWidget("Child 1").Selected(true), // Pre-select only one child
                    new TreeItemWidget("Child 2").Selected(false)
                )
            ]).WithCascadeSelection()),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Parent") && s.ContainsText("Child 2"), 
                TimeSpan.FromSeconds(2), "tree to render")
            .Capture("cascade")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Parent should show indeterminate checkbox [-]
        Assert.True(snapshot.ContainsText("[-]"), 
            $"Should show indeterminate checkbox. Output:\n{snapshot.GetDisplayText()}");
        // Child 1 should show checked [x]
        Assert.True(snapshot.ContainsText("[x]"), 
            "Should show checked checkbox for selected child");
        // Child 2 should show unchecked [ ]
        Assert.True(snapshot.ContainsText("[ ]"), 
            "Should show unchecked checkbox for unselected child");
    }

    [Fact]
    public async Task Tree_CascadeSelection_SpaceSelectsAllChildren()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 20)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TreeWidget([
                new TreeItemWidget("Parent").Expanded().WithChildren(
                    new TreeItemWidget("Child 1"),
                    new TreeItemWidget("Child 2")
                )
            ]).WithCascadeSelection()),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Parent"), TimeSpan.FromSeconds(2), "tree to render")
            .Space()  // Select parent (should cascade to children)
            .Wait(50)
            .Capture("after_space")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // All items should show checked [x] - count occurrences
        var text = snapshot.GetDisplayText();
        var checkedCount = text.Split("[x]").Length - 1;
        Assert.Equal(3, checkedCount); // Parent + 2 children
    }

    [Fact]
    public async Task Tree_MouseClick_OnCheckbox_TogglesSelection()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 20)
            .WithMouse()
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TreeWidget([
                new TreeItemWidget("Item 1"),
                new TreeItemWidget("Item 2")
            ]).WithMultiSelect()),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        // For root items (depth 0): guide=0, indicator=2, so checkbox is at x=2-5
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2), "tree to render")
            .ClickAt(3, 1)  // Click on checkbox area of Item 2 (second row, x=3 is in [2,6) range)
            .Wait(50)
            .Capture("after_click")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Item 2 should now be selected
        var text = snapshot.GetDisplayText();
        Assert.Contains("[x]", text);
    }

    [Fact]
    public async Task Tree_MouseClick_OutsideCheckbox_DoesNotToggleSelection()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 20)
            .WithMouse()
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TreeWidget([
                new TreeItemWidget("Item 1"),
                new TreeItemWidget("Item 2")
            ]).WithMultiSelect()),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        // For root items (depth 0): checkbox is at x=2-5, so click at x=10 is on the label
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2), "tree to render")
            .ClickAt(10, 1)  // Click on label area of Item 2 (outside checkbox)
            .Wait(50)
            .Capture("after_click")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // No items should be selected (checkboxes still all unchecked)
        var text = snapshot.GetDisplayText();
        var checkedCount = text.Split("[x]").Length - 1;
        Assert.Equal(0, checkedCount);
    }

    [Fact]
    public async Task Tree_MouseClick_OnExpandIndicator_TogglesExpand()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(60, 20)
            .WithMouse()
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TreeWidget([
                new TreeItemWidget("Parent").Expanded().WithChildren(
                    new TreeItemWidget("Child 1"),
                    new TreeItemWidget("Child 2")
                )
            ])),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Verify children are visible initially (expanded)
        var beforeSnapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Parent") && s.ContainsText("Child 1"), 
                TimeSpan.FromSeconds(2), "tree to render expanded")
            .Capture("before_click")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.True(beforeSnapshot.ContainsText("Child 1"), "Children should be visible initially");
        
        // Click on the expand indicator (x=0-1 for root item, where ▼ is)
        var afterSnapshot = await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(0, 0)  // Click on expand indicator of Parent (first row, x=0)
            .Wait(100)
            .Capture("after_click")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Children should now be hidden (collapsed)
        Assert.True(afterSnapshot.ContainsText("Parent"), "Parent should still be visible");
        Assert.False(afterSnapshot.ContainsText("Child 1"), "Children should be hidden after clicking expand indicator");
    }

    #endregion
}
