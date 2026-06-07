using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Tokens;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class MenuNodeTests
{
    [TestMethod]
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
        Assert.IsNotEmpty(bindings);
        
        // Check that UpArrow and DownArrow bindings exist
        var upArrowBinding = bindings.FirstOrDefault(b => 
            b.Steps.Count == 1 && 
            b.Steps[0].Key == Hex1bKey.UpArrow && 
            b.Steps[0].Modifiers == Hex1bModifiers.None);
        
        var downArrowBinding = bindings.FirstOrDefault(b => 
            b.Steps.Count == 1 && 
            b.Steps[0].Key == Hex1bKey.DownArrow && 
            b.Steps[0].Modifiers == Hex1bModifiers.None);
            
        Assert.IsNotNull(upArrowBinding);
        Assert.IsNotNull(downArrowBinding);
    }
    
    [TestMethod]
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
        Assert.AreEqual(InputResult.Handled, result);
    }
    
    [TestMethod]
    public async Task MenuItemNode_DownArrow_MovesFocusToNextItem()
    {
        // Arrange - set up a popup with two menu items
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
        
        var menuItem1 = new MenuItemNode
        {
            Label = "New",
            IsFocused = true  // First item has focus
        };
        var menuItem2 = new MenuItemNode
        {
            Label = "Open",
            IsFocused = false
        };
        
        menuItem1.Parent = popupNode;
        menuItem2.Parent = popupNode;
        popupNode.ChildNodes = [menuItem1, menuItem2];
        
        var zstack = new ZStackNode();
        zstack.Children = [popupNode];
        popupNode.Parent = zstack;
        
        // Build focus ring with both items
        var focusRing = new FocusRing();
        focusRing.Rebuild(zstack);
        
        // Verify initial state
        Assert.AreEqual(2, focusRing.Focusables.Count);
        Assert.AreEqual(menuItem1, focusRing.FocusedNode);
        
        // Act - route a down arrow key
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None);
        var state = new InputRouterState();
        
        var result = await InputRouter.RouteInputAsync(zstack, keyEvent, focusRing, state, cancellationToken: TestContext.Current.CancellationToken);
        
        // Assert - focus should have moved to the second item
        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsFalse(menuItem1.IsFocused, "First item should no longer be focused");
        Assert.IsTrue(menuItem2.IsFocused, "Second item should now be focused");
        Assert.AreEqual(menuItem2, focusRing.FocusedNode);
    }
    
    [TestMethod]
    public void MenuItemNode_Measure_ReturnsCorrectSize()
    {
        var node = new MenuItemNode { Label = "Open", RenderWidth = 20 };
        var constraints = new Constraints(0, 100, 0, 10);
        
        var size = node.Measure(constraints);
        
        Assert.AreEqual(20, size.Width);
        Assert.AreEqual(1, size.Height);
    }
    
    [TestMethod]
    public void MenuItemNode_Measure_UsesLabelLengthWhenNoRenderWidth()
    {
        var node = new MenuItemNode { Label = "Open", RenderWidth = 0 };
        var constraints = new Constraints(0, 100, 0, 10);
        
        var size = node.Measure(constraints);
        
        Assert.AreEqual(6, size.Width); // "Open" + 2 padding
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public void MenuItemNode_Measure_WideCharacterLabel_UsesDisplayWidth()
    {
        var node = new MenuItemNode { Label = "播放", RenderWidth = 0 };
        var constraints = new Constraints(0, 100, 0, 10);

        var size = node.Measure(constraints);

        Assert.AreEqual(6, size.Width); // CJK label width 4 + 2 padding
        Assert.AreEqual(1, size.Height);
    }
    
    [TestMethod]
    public void MenuItemNode_IsFocusable_WhenNotDisabled()
    {
        var node = new MenuItemNode { Label = "Open", IsDisabled = false };
        
        Assert.IsTrue(node.IsFocusable);
    }
    
    [TestMethod]
    public void MenuItemNode_NotFocusable_WhenDisabled()
    {
        var node = new MenuItemNode { Label = "Open", IsDisabled = true };
        
        Assert.IsFalse(node.IsFocusable);
    }
    
    [TestMethod]
    public void MenuItemNode_IsFocused_MarksDirty()
    {
        var node = new MenuItemNode { Label = "Open" };
        node.ClearDirty();
        
        node.IsFocused = true;
        
        Assert.IsTrue(node.IsDirty);
    }
    
    [TestMethod]
    public void MenuSeparatorNode_Measure_ReturnsOneRowHeight()
    {
        var node = new MenuSeparatorNode { RenderWidth = 20 };
        var constraints = new Constraints(0, 100, 0, 10);
        
        var size = node.Measure(constraints);
        
        Assert.AreEqual(20, size.Width);
        Assert.AreEqual(1, size.Height);
    }
    
    [TestMethod]
    public void MenuSeparatorNode_NotFocusable()
    {
        var node = new MenuSeparatorNode();
        
        Assert.IsFalse(node.IsFocusable);
    }
    
    [TestMethod]
    public void MenuSeparatorNode_FallbackFocusable_WhenSet()
    {
        var node = new MenuSeparatorNode { IsFallbackFocusable = true };
        
        Assert.IsTrue(node.IsFocusable);
    }
    
    [TestMethod]
    public void MenuSeparatorNode_FallbackFocusable_DefaultsFalse()
    {
        var node = new MenuSeparatorNode();
        
        Assert.IsFalse(node.IsFallbackFocusable);
        Assert.IsFalse(node.IsFocusable);
    }
    
    [TestMethod]
    public void MenuItemNode_Disabled_NotFocusable()
    {
        var node = new MenuItemNode { Label = "Test", IsDisabled = true };
        
        Assert.IsFalse(node.IsFocusable);
    }
    
    [TestMethod]
    public void MenuItemNode_Disabled_FallbackFocusable_WhenSet()
    {
        var node = new MenuItemNode { Label = "Test", IsDisabled = true, IsFallbackFocusable = true };
        
        Assert.IsTrue(node.IsFocusable);
    }
    
    [TestMethod]
    public void MenuItemNode_Enabled_AlwaysFocusable()
    {
        var node = new MenuItemNode { Label = "Test", IsDisabled = false };
        
        Assert.IsTrue(node.IsFocusable);
        
        // IsFallbackFocusable shouldn't affect enabled items
        node.IsFallbackFocusable = true;
        Assert.IsTrue(node.IsFocusable);
    }
    
    [TestMethod]
    public void MenuNode_Measure_ReturnsLabelWidth()
    {
        var node = new MenuNode { Label = "File" };
        var constraints = new Constraints(0, 100, 0, 10);
        
        var size = node.Measure(constraints);
        
        Assert.AreEqual(6, size.Width); // "File" + 2 padding
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public void MenuNode_Measure_WideCharacterLabel_UsesDisplayWidth()
    {
        var node = new MenuNode { Label = "媒体" };
        var constraints = new Constraints(0, 100, 0, 10);

        var size = node.Measure(constraints);

        Assert.AreEqual(6, size.Width); // CJK label width 4 + 2 padding
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public void MenuPopupNode_Measure_WideCharacterItems_UsesDisplayWidth()
    {
        var item = new MenuItemWidget("播放");
        var submenu = new MenuWidget("搜索", []);
        var ownerNode = new MenuNode
        {
            Label = "媒体",
            Children = [item, submenu],
            ChildAccelerators = [(item, null, -1), (submenu, null, -1)]
        };
        var popupNode = new MenuPopupNode { OwnerNode = ownerNode };
        var constraints = new Constraints(0, 100, 0, 10);

        var size = popupNode.Measure(constraints);

        Assert.AreEqual(10, size.Width); // submenu label width 4 + indicator width 2 + padding 2 + border 2
        Assert.AreEqual(4, size.Height);
    }
    
    [TestMethod]
    public void MenuNode_IsFocusable()
    {
        var node = new MenuNode { Label = "File" };
        
        Assert.IsTrue(node.IsFocusable);
    }
    
    [TestMethod]
    public void MenuNode_ManagesChildFocus()
    {
        var node = new MenuNode { Label = "File" };
        
        Assert.IsTrue(node.ManagesChildFocus);
    }
    
    [TestMethod]
    public void MenuBarNode_ManagesChildFocus()
    {
        var node = new MenuBarNode();
        
        Assert.IsTrue(node.ManagesChildFocus);
    }
    
    [TestMethod]
    public void MenuBarNode_NotFocusable()
    {
        var node = new MenuBarNode();
        
        Assert.IsFalse(node.IsFocusable);
    }
    
    [TestMethod]
    public void BackdropNode_FocusDelegation_SetsChildFocus()
    {
        // Arrange - simulate the tree structure when a menu is open
        // BackdropNode → AnchoredNode → MenuPopupNode → MenuItemNode
        
        var menuItem1 = new MenuItemNode { Label = "New" };
        var menuItem2 = new MenuItemNode { Label = "Open" };
        
        var ownerNode = new MenuNode
        {
            Label = "File",
            Children = [],
            ChildAccelerators = []
        };
        
        var popupNode = new MenuPopupNode { OwnerNode = ownerNode };
        popupNode.ChildNodes = [menuItem1, menuItem2];
        menuItem1.Parent = popupNode;
        menuItem2.Parent = popupNode;
        
        // Simulate AnchoredNode wrapping MenuPopupNode
        var anchoredNode = new AnchoredNode { Child = popupNode };
        popupNode.Parent = anchoredNode;
        
        // Simulate BackdropNode wrapping AnchoredNode
        var backdropNode = new BackdropNode { Child = anchoredNode };
        anchoredNode.Parent = backdropNode;
        
        // Initially, nothing is focused
        Assert.IsFalse(menuItem1.IsFocused);
        Assert.IsFalse(menuItem2.IsFocused);
        
        // Act - Set focus on BackdropNode (what ZStack does)
        backdropNode.IsFocused = true;
        
        // Assert - Focus should delegate to first focusable child (menuItem1)
        Assert.IsTrue(menuItem1.IsFocused);
        Assert.IsFalse(menuItem2.IsFocused);
    }
    
    [TestMethod]
    public async Task InputRouter_EnterKey_ActivatesMenuItem_WhenMenuItemIsFocused()
    {
        // Arrange - simulate full tree with ZStack → BackdropNode → AnchoredNode → MenuPopupNode → MenuItemNode
        var activated = false;
        
        var menuItem1 = new MenuItemNode 
        { 
            Label = "New",
            ActivatedAction = ctx => { activated = true; return Task.CompletedTask; }
        };
        menuItem1.IsFocused = true;  // Directly set focus on the item
        
        var ownerNode = new MenuNode
        {
            Label = "File",
            Children = [],
            ChildAccelerators = []
        };
        
        var popupNode = new MenuPopupNode { OwnerNode = ownerNode };
        popupNode.ChildNodes = [menuItem1];
        menuItem1.Parent = popupNode;
        
        var anchoredNode = new AnchoredNode { Child = popupNode };
        popupNode.Parent = anchoredNode;
        
        var backdropNode = new BackdropNode { Child = anchoredNode };
        anchoredNode.Parent = backdropNode;
        
        var zstack = new ZStackNode();
        zstack.Children = [backdropNode];
        backdropNode.Parent = zstack;
        
        // Build focus ring
        var focusRing = new FocusRing();
        focusRing.Rebuild(zstack);
        
        // Verify menuItem1 is in focus ring and is focused
        Assert.Contains(menuItem1, focusRing.Focusables);
        Assert.AreEqual(menuItem1, focusRing.FocusedNode);
        
        // Act - Route Enter key
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None);
        var state = new InputRouterState();
        
        var result = await InputRouter.RouteInputAsync(zstack, keyEvent, focusRing, state);
        
        // Assert
        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsTrue(activated, "ActivatedAction should have been called");
    }
    
    [TestMethod]
    public void MenuNode_InMenuBar_ConfiguresLeftRightBindings()
    {
        // Arrange
        var menuBar = new MenuBarNode();
        var menuNode = new MenuNode
        {
            Label = "File",
            Children = [],
            ChildAccelerators = [],
            Parent = menuBar
        };
        menuBar.MenuNodes = [menuNode];
        
        // Act
        var builder = new InputBindingsBuilder();
        menuNode.ConfigureDefaultBindings(builder);
        var bindings = builder.Build();
        
        // Assert - should have Left and Right arrow bindings
        var leftBinding = bindings.FirstOrDefault(b => 
            b.Steps.Count == 1 && 
            b.Steps[0].Key == Hex1bKey.LeftArrow && 
            b.Steps[0].Modifiers == Hex1bModifiers.None);
        
        var rightBinding = bindings.FirstOrDefault(b => 
            b.Steps.Count == 1 && 
            b.Steps[0].Key == Hex1bKey.RightArrow && 
            b.Steps[0].Modifiers == Hex1bModifiers.None);
        
        var downBinding = bindings.FirstOrDefault(b => 
            b.Steps.Count == 1 && 
            b.Steps[0].Key == Hex1bKey.DownArrow && 
            b.Steps[0].Modifiers == Hex1bModifiers.None);
            
        Assert.IsNotNull(leftBinding);
        Assert.IsNotNull(rightBinding);
        Assert.IsNotNull(downBinding);
    }
    
    [TestMethod]
    public async Task MenuNode_InMenuBar_RightArrow_FocusesNextMenu()
    {
        // Arrange - simulate the full tree with VStack -> MenuBar -> MenuNodes
        var vstack = new VStackNode();
        var menuBar = new MenuBarNode
        {
            Menus = [
                new MenuWidget("File", []),
                new MenuWidget("Edit", [])
            ],
            MenuAccelerators = [
                (new MenuWidget("File", []), null, -1),
                (new MenuWidget("Edit", []), null, -1)
            ]
        };
        menuBar.Parent = vstack;
        vstack.Children = [menuBar];
        
        // Measure to populate MenuNodes
        menuBar.Measure(Constraints.Unbounded);
        
        // Verify MenuNodes are populated
        Assert.AreEqual(2, menuBar.MenuNodes.Count);
        
        var menu1 = menuBar.MenuNodes[0];
        var menu2 = menuBar.MenuNodes[1];
        
        // Verify Parent is set
        Assert.AreSame(menuBar, menu1.Parent);
        Assert.AreSame(menuBar, menu2.Parent);
        
        // Build focus ring from the vstack
        var focusRing = new FocusRing();
        focusRing.Rebuild(vstack);
        
        // Verify both menus are focusable
        Assert.Contains(menu1, focusRing.Focusables);
        Assert.Contains(menu2, focusRing.Focusables);
        
        // Verify menu1 gets initial focus
        focusRing.EnsureFocus();
        Assert.IsTrue(menu1.IsFocused, "menu1 should be initially focused");
        
        // Verify bindings are configured correctly
        var builder = new InputBindingsBuilder();
        menu1.ConfigureDefaultBindings(builder);
        var bindings = builder.Build();
        
        var rightBinding = bindings.FirstOrDefault(b => 
            b.Steps.Count == 1 && 
            b.Steps[0].Key == Hex1bKey.RightArrow);
        Assert.IsNotNull(rightBinding);
        
        // Act - press right arrow via InputRouter
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None);
        var state = new InputRouterState();
        var result = await InputRouter.RouteInputAsync(vstack, keyEvent, focusRing, state);
        
        // Assert
        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsTrue(menu2.IsFocused, $"menu2 should be focused. Focused node: {focusRing.FocusedNode?.GetType().Name}");
        Assert.IsFalse(menu1.IsFocused, "menu1 should not be focused");
    }
    
    [TestMethod]
    public async Task MenuNode_InMenuBar_LeftArrow_FocusesPreviousMenu()
    {
        // Arrange - simulate the full tree with VStack -> MenuBar -> MenuNodes
        var vstack = new VStackNode();
        var menuBar = new MenuBarNode
        {
            Menus = [
                new MenuWidget("File", []),
                new MenuWidget("Edit", [])
            ],
            MenuAccelerators = [
                (new MenuWidget("File", []), null, -1),
                (new MenuWidget("Edit", []), null, -1)
            ]
        };
        menuBar.Parent = vstack;
        vstack.Children = [menuBar];
        
        // Measure to populate MenuNodes
        menuBar.Measure(Constraints.Unbounded);
        
        var menu1 = menuBar.MenuNodes[0];
        var menu2 = menuBar.MenuNodes[1];
        
        // Build focus ring from the vstack
        var focusRing = new FocusRing();
        focusRing.Rebuild(vstack);
        
        // Start with menu2 focused
        focusRing.Focus(menu2);
        Assert.IsTrue(menu2.IsFocused, "menu2 should be initially focused");
        
        // Act - press left arrow via InputRouter
        var keyEvent = new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None);
        var state = new InputRouterState();
        var result = await InputRouter.RouteInputAsync(vstack, keyEvent, focusRing, state);
        
        // Assert
        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsTrue(menu1.IsFocused, "menu1 should be focused");
        Assert.IsFalse(menu2.IsFocused, "menu2 should not be focused");
    }
    
    #region Accelerator Underline Rendering Tests
    
    /// <summary>
    /// Simple synchronous test terminal that directly calls ApplyTokens.
    /// </summary>
    private sealed class TestTerminal : IDisposable
    {
        private readonly StreamWorkloadAdapter _workload;

        public Hex1bTerminal Terminal { get; }

        public TestTerminal(int width, int height)
        {
            _workload = StreamWorkloadAdapter.CreateHeadless(width, height);
            Terminal = Hex1bTerminal.CreateBuilder().WithWorkload(_workload).WithHeadless().WithDimensions(width, height).Build();
        }

        public void Dispose()
        {
            Terminal.Dispose();
            _workload.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
    
    /// <summary>
    /// Finds the first cell matching the specified character that has the underline attribute.
    /// </summary>
    private static bool HasCellWithUnderline(Hex1bTerminalSnapshot snapshot, char c)
    {
        var target = c.ToString();
        for (int y = 0; y < snapshot.Height; y++)
        {
            for (int x = 0; x < snapshot.Width; x++)
            {
                var cell = snapshot.GetCell(x, y);
                if (cell.Character == target && (cell.Attributes & CellAttributes.Underline) != 0)
                    return true;
            }
        }
        return false;
    }
    
    [TestMethod]
    public async Task MenuNode_InMenuBar_Normal_ShowsAcceleratorUnderline()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        
        var menuBar = new MenuBarNode();
        var menuNode = new MenuNode
        {
            Label = "File",
            Children = [],
            ChildAccelerators = [],
            Accelerator = 'F',
            AcceleratorIndex = 0,
            Parent = menuBar,
            IsFocused = false,
            IsSelected = false,
            IsOpen = false
        };
        menuBar.MenuNodes = [menuNode];
        menuNode.Arrange(new Rect(0, 0, 6, 1));
        
        // Act
        menuNode.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5), "File label")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Assert - 'F' should be underlined
        Assert.IsTrue(HasCellWithUnderline(snapshot, 'F'), "Accelerator 'F' should be underlined in normal state");
    }
    
    [TestMethod]
    public async Task MenuNode_InMenuBar_Focused_ShowsAcceleratorUnderline()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        
        var menuBar = new MenuBarNode();
        var menuNode = new MenuNode
        {
            Label = "File",
            Children = [],
            ChildAccelerators = [],
            Accelerator = 'F',
            AcceleratorIndex = 0,
            Parent = menuBar,
            IsFocused = true,
            IsSelected = false,
            IsOpen = false
        };
        menuBar.MenuNodes = [menuNode];
        menuNode.Arrange(new Rect(0, 0, 6, 1));
        
        // Act
        menuNode.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5), "File label")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Assert - 'F' should still be underlined when focused
        Assert.IsTrue(HasCellWithUnderline(snapshot, 'F'), "Accelerator 'F' should be underlined when focused");
    }
    
    [TestMethod]
    public async Task MenuNode_InMenuBar_Selected_ShowsAcceleratorUnderline()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        
        var menuBar = new MenuBarNode();
        var menuNode = new MenuNode
        {
            Label = "File",
            Children = [],
            ChildAccelerators = [],
            Accelerator = 'F',
            AcceleratorIndex = 0,
            Parent = menuBar,
            IsFocused = false,
            IsSelected = true,
            IsOpen = false
        };
        menuBar.MenuNodes = [menuNode];
        menuNode.Arrange(new Rect(0, 0, 6, 1));
        
        // Act
        menuNode.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5), "File label")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Assert - 'F' should still be underlined when selected
        Assert.IsTrue(HasCellWithUnderline(snapshot, 'F'), "Accelerator 'F' should be underlined when selected");
    }
    
    [TestMethod]
    public async Task MenuNode_InMenuBar_Hovered_ShowsAcceleratorUnderline()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        
        var menuBar = new MenuBarNode();
        var menuNode = new MenuNode
        {
            Label = "File",
            Children = [],
            ChildAccelerators = [],
            Accelerator = 'F',
            AcceleratorIndex = 0,
            Parent = menuBar,
            IsFocused = false,
            IsSelected = false,
            IsOpen = false,
            IsHovered = true
        };
        menuBar.MenuNodes = [menuNode];
        menuNode.Arrange(new Rect(0, 0, 6, 1));
        
        // Act
        menuNode.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5), "File label")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Assert - 'F' should still be underlined when hovered
        Assert.IsTrue(HasCellWithUnderline(snapshot, 'F'), "Accelerator 'F' should be underlined when hovered");
    }
    
    [TestMethod]
    public async Task MenuNode_InMenuBar_Open_ShowsAcceleratorUnderline()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        
        var menuBar = new MenuBarNode();
        var menuNode = new MenuNode
        {
            Label = "File",
            Children = [],
            ChildAccelerators = [],
            Accelerator = 'F',
            AcceleratorIndex = 0,
            Parent = menuBar,
            IsFocused = false,
            IsSelected = false,
            IsOpen = true
        };
        menuBar.MenuNodes = [menuNode];
        menuNode.Arrange(new Rect(0, 0, 6, 1));
        
        // Act
        menuNode.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5), "File label")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Assert - 'F' should still be underlined when open
        Assert.IsTrue(HasCellWithUnderline(snapshot, 'F'), "Accelerator 'F' should be underlined when open");
    }
    
    [TestMethod]
    public async Task MenuItemNode_Normal_ShowsAcceleratorUnderline()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        
        var menuItem = new MenuItemNode
        {
            Label = "Open",
            Accelerator = 'O',
            AcceleratorIndex = 0,
            RenderWidth = 10,
            IsFocused = false,
            IsHovered = false,
            IsDisabled = false
        };
        menuItem.Arrange(new Rect(0, 0, 10, 1));
        
        // Act
        menuItem.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Open"), TimeSpan.FromSeconds(5), "Open label")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Assert - 'O' should be underlined
        Assert.IsTrue(HasCellWithUnderline(snapshot, 'O'), "Accelerator 'O' should be underlined in normal state");
    }
    
    [TestMethod]
    public async Task MenuItemNode_Focused_ShowsAcceleratorUnderline()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        
        var menuItem = new MenuItemNode
        {
            Label = "Open",
            Accelerator = 'O',
            AcceleratorIndex = 0,
            RenderWidth = 10,
            IsFocused = true,
            IsHovered = false,
            IsDisabled = false
        };
        menuItem.Arrange(new Rect(0, 0, 10, 1));
        
        // Act
        menuItem.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Open"), TimeSpan.FromSeconds(5), "Open label")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Assert - 'O' should still be underlined when focused
        Assert.IsTrue(HasCellWithUnderline(snapshot, 'O'), "Accelerator 'O' should be underlined when focused");
    }
    
    [TestMethod]
    public async Task MenuItemNode_Hovered_ShowsAcceleratorUnderline()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        
        var menuItem = new MenuItemNode
        {
            Label = "Open",
            Accelerator = 'O',
            AcceleratorIndex = 0,
            RenderWidth = 10,
            IsFocused = false,
            IsHovered = true,
            IsDisabled = false
        };
        menuItem.Arrange(new Rect(0, 0, 10, 1));
        
        // Act
        menuItem.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Open"), TimeSpan.FromSeconds(5), "Open label")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Assert - 'O' should still be underlined when hovered
        Assert.IsTrue(HasCellWithUnderline(snapshot, 'O'), "Accelerator 'O' should be underlined when hovered");
    }
    
    [TestMethod]
    public async Task MenuItemNode_Disabled_DoesNotShowAcceleratorUnderline()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        
        var menuItem = new MenuItemNode
        {
            Label = "Open",
            Accelerator = 'O',
            AcceleratorIndex = 0,
            RenderWidth = 10,
            IsFocused = false,
            IsHovered = false,
            IsDisabled = true
        };
        menuItem.Arrange(new Rect(0, 0, 10, 1));
        
        // Act
        menuItem.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Open"), TimeSpan.FromSeconds(5), "Open label")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Assert - 'O' should NOT be underlined when disabled (accelerators are not available for disabled items)
        Assert.IsFalse(HasCellWithUnderline(snapshot, 'O'), "Accelerator 'O' should NOT be underlined when disabled");
    }
    
    #endregion
}
