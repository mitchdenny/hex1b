using Hex1b;
using Hex1b.Events;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class PickerNodeTests
{
    [Fact]
    public void PickerNode_InitialState_HasCorrectDefaults()
    {
        // Arrange & Act
        var node = new PickerNode();
        
        // Assert
        Assert.Empty(node.Items);
        Assert.Equal(0, node.SelectedIndex);
        Assert.Equal("", node.SelectedText);
        Assert.Null(node.SelectionChangedAction);
    }
    
    [Fact]
    public void PickerNode_WithItems_ReturnsCorrectSelectedText()
    {
        // Arrange
        var node = new PickerNode
        {
            Items = ["Apple", "Banana", "Cherry"],
            SelectedIndex = 1
        };
        
        // Act & Assert
        Assert.Equal("Banana", node.SelectedText);
    }
    
    [Fact]
    public void PickerNode_SelectedIndex_ClampsToValidRange()
    {
        // Arrange
        var node = new PickerNode
        {
            Items = ["Apple", "Banana", "Cherry"],
            SelectedIndex = 10 // Out of range
        };
        
        // Assert - SelectedText should return empty since index is invalid
        Assert.Equal("", node.SelectedText);
    }
    
    [Fact]
    public void PickerNode_EmptyItems_ReturnsEmptySelectedText()
    {
        // Arrange
        var node = new PickerNode
        {
            Items = [],
            SelectedIndex = 0
        };
        
        // Assert
        Assert.Equal("", node.SelectedText);
    }
    
    [Fact]
    public async Task PickerWidget_Reconcile_CreatesPickerNode()
    {
        // Arrange
        var widget = new PickerWidget(["Apple", "Banana", "Cherry"]);
        var context = ReconcileContext.CreateRoot();
        
        // Act
        var node = await widget.ReconcileAsync(null, context) as PickerNode;
        
        // Assert
        Assert.NotNull(node);
        Assert.Equal(["Apple", "Banana", "Cherry"], node.Items);
        Assert.Equal(0, node.SelectedIndex);
        Assert.Equal("Apple", node.SelectedText);
    }
    
    [Fact]
    public async Task PickerWidget_Reconcile_PreservesExistingSelection()
    {
        // Arrange
        var widget1 = new PickerWidget(["Apple", "Banana", "Cherry"]);
        var context = ReconcileContext.CreateRoot();
        var node = await widget1.ReconcileAsync(null, context) as PickerNode;
        node!.SelectedIndex = 2; // User selected Cherry
        
        // Act - Reconcile again with same items
        var widget2 = new PickerWidget(["Apple", "Banana", "Cherry"]);
        var reconciledNode = await widget2.ReconcileAsync(node, context) as PickerNode;
        
        // Assert - Selection should be preserved
        Assert.Same(node, reconciledNode);
        Assert.Equal(2, reconciledNode!.SelectedIndex);
        Assert.Equal("Cherry", reconciledNode.SelectedText);
    }
    
    [Fact]
    public async Task PickerWidget_Reconcile_ClampsSelectionWhenItemsReduced()
    {
        // Arrange
        var widget1 = new PickerWidget(["Apple", "Banana", "Cherry", "Date", "Elderberry"]);
        var context = ReconcileContext.CreateRoot();
        var node = await widget1.ReconcileAsync(null, context) as PickerNode;
        node!.SelectedIndex = 4; // User selected Elderberry
        
        // Act - Reconcile with fewer items
        var widget2 = new PickerWidget(["Apple", "Banana", "Cherry"]);
        var reconciledNode = await widget2.ReconcileAsync(node, context) as PickerNode;
        
        // Assert - Selection should be clamped to last valid index
        Assert.Equal(2, reconciledNode!.SelectedIndex);
        Assert.Equal("Cherry", reconciledNode.SelectedText);
    }
    
    [Fact]
    public async Task PickerWidget_WithInitialSelection_SetsCorrectIndex()
    {
        // Arrange
        var widget = new PickerWidget(["Apple", "Banana", "Cherry"]) { InitialSelectedIndex = 1 };
        var context = ReconcileContext.CreateRoot();
        
        // Act
        var node = await widget.ReconcileAsync(null, context) as PickerNode;
        
        // Assert
        Assert.Equal(1, node!.SelectedIndex);
        Assert.Equal("Banana", node.SelectedText);
    }
    
    [Fact]
    public async Task PickerWidget_OnSelectionChanged_SetsHandler()
    {
        // Arrange
        var handlerCalled = false;
        var widget = new PickerWidget(["Apple", "Banana", "Cherry"])
            .OnSelectionChanged(e => handlerCalled = true);
        var context = ReconcileContext.CreateRoot();
        
        // Act
        var node = await widget.ReconcileAsync(null, context) as PickerNode;
        
        // Assert
        Assert.NotNull(node!.SelectionChangedAction);
    }
    
    [Fact]
    public void PickerNode_ContentChild_IsCreated()
    {
        // Arrange & Act (via reconciliation)
        var widget = new PickerWidget(["Apple", "Banana", "Cherry"]);
        var context = ReconcileContext.CreateRoot();
        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult() as PickerNode;
        
        // Assert - ContentChild should be a ButtonNode (from the button we build internally)
        Assert.NotNull(node!.ContentChild);
        Assert.IsType<ButtonNode>(node.ContentChild);
    }
    
    [Fact]
    public void PickerNode_IsFocusable_ReturnsFalse_ButContentChildIsFocusable()
    {
        // Arrange
        var widget = new PickerWidget(["Apple", "Banana", "Cherry"]);
        var context = ReconcileContext.CreateRoot();
        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult() as PickerNode;
        
        // Assert - PickerNode itself is NOT focusable (focus passes through to content child)
        // This ensures the input router continues into GetChildren() to find the ButtonNode
        Assert.False(node!.IsFocusable);
        
        // But the content child (ButtonNode) IS focusable
        Assert.True(node.ContentChild!.IsFocusable);
    }
}
