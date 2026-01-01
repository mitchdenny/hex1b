using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Tests;

public class MenuNodeTests
{
    [Fact]
    public void MenuPopupNode_ConfiguresUpDownBindings()
    {
        // Arrange
        var ownerNode = new MenuNode
        {
            Label = "File",
            Children = [],
            ChildAccelerators = []
        };
        var popupNode = new MenuPopupNode
        {
            OwnerNode = ownerNode
        };
        
        // Act
        var builder = new InputBindingsBuilder();
        popupNode.ConfigureDefaultBindings(builder);
        var bindings = builder.Build();
        
        // Assert
        Assert.NotEmpty(bindings);
        
        // Check that UpArrow and DownArrow bindings exist
        var upArrowBinding = bindings.FirstOrDefault(b => 
            b.Steps.Count == 1 && 
            b.Steps[0].Key == Hex1bKey.UpArrow && 
            b.Steps[0].Modifiers == Hex1bModifiers.None);
        
        var downArrowBinding = bindings.FirstOrDefault(b => 
            b.Steps.Count == 1 && 
            b.Steps[0].Key == Hex1bKey.DownArrow && 
            b.Steps[0].Modifiers == Hex1bModifiers.None);
            
        Assert.NotNull(upArrowBinding);
        Assert.NotNull(downArrowBinding);
    }
    
    [Fact]
    public async Task InputRouter_FindsMenuPopupBindings_WhenChildIsFocused()
    {
        // Arrange - simulate the tree structure when a menu is open
        // ZStack → BackdropNode → AnchoredNode → MenuPopupNode → MenuItemNode
        
        var ownerNode = new MenuNode
        {
            Label = "File",
            Children = [],
            ChildAccelerators = []
        };
        
        var popupNode = new MenuPopupNode
        {
            OwnerNode = ownerNode
        };
        
        var menuItem = new MenuItemNode
        {
            Label = "Open",
            IsFocused = true
        };
        menuItem.Parent = popupNode;
        
        popupNode.ChildNodes = [menuItem];
        
        // Create a simple tree with popupNode → menuItem
        var zstack = new ZStackNode();
        zstack.Children = [popupNode];
        popupNode.Parent = zstack;
        
        // Act - route a down arrow key
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None);
        var focusRing = new FocusRing();
        focusRing.Rebuild(zstack);
        var state = new InputRouterState();
        
        var result = await InputRouter.RouteInputAsync(zstack, keyEvent, focusRing, state, cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert - the binding should be found and handled
        Assert.Equal(InputResult.Handled, result);
    }
    [Fact]
    public void MenuItemNode_Measure_ReturnsCorrectSize()
    {
        var node = new MenuItemNode { Label = "Open", RenderWidth = 20 };
        var constraints = new Constraints(0, 100, 0, 10);
        
        var size = node.Measure(constraints);
        
        Assert.Equal(20, size.Width);
        Assert.Equal(1, size.Height);
    }
    
    [Fact]
    public void MenuItemNode_Measure_UsesLabelLengthWhenNoRenderWidth()
    {
        var node = new MenuItemNode { Label = "Open", RenderWidth = 0 };
        var constraints = new Constraints(0, 100, 0, 10);
        
        var size = node.Measure(constraints);
        
        Assert.Equal(6, size.Width); // "Open" + 2 padding
        Assert.Equal(1, size.Height);
    }
    
    [Fact]
    public void MenuItemNode_IsFocusable_WhenNotDisabled()
    {
        var node = new MenuItemNode { Label = "Open", IsDisabled = false };
        
        Assert.True(node.IsFocusable);
    }
    
    [Fact]
    public void MenuItemNode_NotFocusable_WhenDisabled()
    {
        var node = new MenuItemNode { Label = "Open", IsDisabled = true };
        
        Assert.False(node.IsFocusable);
    }
    
    [Fact]
    public void MenuItemNode_IsFocused_MarksDirty()
    {
        var node = new MenuItemNode { Label = "Open" };
        node.ClearDirty();
        
        node.IsFocused = true;
        
        Assert.True(node.IsDirty);
    }
    
    [Fact]
    public void MenuSeparatorNode_Measure_ReturnsOneRowHeight()
    {
        var node = new MenuSeparatorNode { RenderWidth = 20 };
        var constraints = new Constraints(0, 100, 0, 10);
        
        var size = node.Measure(constraints);
        
        Assert.Equal(20, size.Width);
        Assert.Equal(1, size.Height);
    }
    
    [Fact]
    public void MenuSeparatorNode_NotFocusable()
    {
        var node = new MenuSeparatorNode();
        
        Assert.False(node.IsFocusable);
    }
    
    [Fact]
    public void MenuNode_Measure_ReturnsLabelWidth()
    {
        var node = new MenuNode { Label = "File" };
        var constraints = new Constraints(0, 100, 0, 10);
        
        var size = node.Measure(constraints);
        
        Assert.Equal(6, size.Width); // "File" + 2 padding
        Assert.Equal(1, size.Height);
    }
    
    [Fact]
    public void MenuNode_IsFocusable()
    {
        var node = new MenuNode { Label = "File" };
        
        Assert.True(node.IsFocusable);
    }
    
    [Fact]
    public void MenuNode_ManagesChildFocus()
    {
        var node = new MenuNode { Label = "File" };
        
        Assert.True(node.ManagesChildFocus);
    }
    
    [Fact]
    public void MenuBarNode_ManagesChildFocus()
    {
        var node = new MenuBarNode();
        
        Assert.True(node.ManagesChildFocus);
    }
    
    [Fact]
    public void MenuBarNode_NotFocusable()
    {
        var node = new MenuBarNode();
        
        Assert.False(node.IsFocusable);
    }
}
