using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for SelectionPanelNode covering pass-through behaviour outside
/// copy mode, cursor / anchor state inside copy mode, and the rendered
/// inversion overlay.
/// </summary>
public class SelectionPanelNodeTests
{
    // ------------------------------------------------------------------
    // Pass-through tests (outside copy mode)
    // ------------------------------------------------------------------

    [Fact]
    public void Measure_ReturnsChildSize()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = new SelectionPanelNode { Child = child };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(5, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_WithNoChild_ReturnsZero()
    {
        var node = new SelectionPanelNode { Child = null };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void Arrange_ForwardsRectToChild()
    {
        var child = new TextBlockNode { Text = "Hi" };
        var node = new SelectionPanelNode { Child = child };
        var rect = new Rect(3, 4, 10, 2);

        node.Measure(Constraints.Unbounded);
        node.Arrange(rect);

        Assert.Equal(rect, child.Bounds);
        Assert.Equal(rect, node.Bounds);
    }

    [Fact]
    public void IsFocusable_IsFalse()
    {
        var node = new SelectionPanelNode();

        Assert.False(node.IsFocusable);
    }

    [Fact]
    public void GetFocusableNodes_ReturnsChildFocusables()
    {
        var child = new TextBoxNode { Text = "x" };
        var node = new SelectionPanelNode { Child = child };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Same(child, focusables[0]);
    }

    [Fact]
    public void GetChildren_ReturnsChild()
    {
        var child = new TextBlockNode { Text = "x" };
        var node = new SelectionPanelNode { Child = child };

        var children = node.GetChildren().ToList();

        Assert.Single(children);
        Assert.Same(child, children[0]);
    }

    [Fact]
    public void IsFocused_SetterForwardsToChild()
    {
        var child = new TextBoxNode { Text = "" };
        var node = new SelectionPanelNode { Child = child };

        node.IsFocused = true;

        Assert.True(child.IsFocused);
    }

    // ------------------------------------------------------------------
    // SnapshotText (selection-driven)
    // ------------------------------------------------------------------

    [Fact]
    public void SnapshotText_NoSelection_ReturnsEmpty()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "Hello" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 5, 1));

        // No anchor set → empty.
        Assert.Equal(string.Empty, node.SnapshotText());
    }

    [Fact]
    public void SnapshotText_NoChild_ReturnsEmpty()
    {
        var node = new SelectionPanelNode { Child = null };
        Assert.Equal(string.Empty, node.SnapshotText());
    }

    [Fact]
    public void SnapshotText_CharacterSelection_OnSingleRow_ReturnsRangeText()
    {
        var painter = new PaintingTestNode();
        var node = new SelectionPanelNode { Child = painter };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 12, 8));

        // Place cursor at row 2 col 8, anchor at row 2 col 3 → "CCCCCC" (cols 3..8).
        node.EnterCopyMode();
        node.SetCursor(2, 3);
        node.StartOrToggleSelection(SelectionMode.Character);
        node.SetCursor(2, 8);

        Assert.Equal("CCCCCC", node.SnapshotText());
    }

    [Fact]
    public void SnapshotText_LineSelection_ReturnsFullRows()
    {
        var painter = new PaintingTestNode();
        var node = new SelectionPanelNode { Child = painter };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 12, 8));

        node.EnterCopyMode();
        node.SetCursor(2, 5);
        node.StartOrToggleSelection(SelectionMode.Line);
        node.SetCursor(5, 0);

        Assert.Equal(
            "CCCCCCCCCCCC\nDDDDDDDDDDDD\nEEEEEEEEEEEE\nFFFFFFFFFFFF",
            node.SnapshotText().Replace("\r\n", "\n"));
    }

    [Fact]
    public void SnapshotText_BlockSelection_ReturnsRectangle()
    {
        var painter = new PaintingTestNode();
        var node = new SelectionPanelNode { Child = painter };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 12, 8));

        node.EnterCopyMode();
        node.SetCursor(2, 3);
        node.StartOrToggleSelection(SelectionMode.Block);
        node.SetCursor(5, 8);

        Assert.Equal(
            "CCCCCC\nDDDDDD\nEEEEEE\nFFFFFF",
            node.SnapshotText().Replace("\r\n", "\n"));
    }

    [Fact]
    public void SnapshotText_CharacterSelection_OverManyRows_StreamsLikeTerminal()
    {
        var painter = new PaintingTestNode();
        var node = new SelectionPanelNode { Child = painter };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 12, 8));

        // Anchor at (2, 3), cursor at (5, 8).
        // Top row: cols 3..end (9 chars 'C')
        // Middle rows: full width (12 chars each)
        // Bottom row: cols 0..8 (9 chars 'F')
        node.EnterCopyMode();
        node.SetCursor(2, 3);
        node.StartOrToggleSelection(SelectionMode.Character);
        node.SetCursor(5, 8);

        Assert.Equal(
            "CCCCCCCCC\nDDDDDDDDDDDD\nEEEEEEEEEEEE\nFFFFFFFFF",
            node.SnapshotText().Replace("\r\n", "\n"));
    }

    // ------------------------------------------------------------------
    // EnterCopyMode / ExitCopyMode / cursor + anchor state
    // ------------------------------------------------------------------

    [Fact]
    public void EnterCopyMode_StartsAtBottomLeft_AndClearsAnchor()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "x" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 12, 8));

        node.EnterCopyMode();

        Assert.True(node.IsInCopyMode);
        Assert.Equal(7, node.CursorRow);
        Assert.Equal(0, node.CursorCol);
        Assert.False(node.HasSelection);
        Assert.Equal(SelectionMode.Character, node.CursorSelectionMode);
    }

    [Fact]
    public void ExitCopyMode_ClearsAllState()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "x" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 12, 8));
        node.EnterCopyMode();
        node.StartOrToggleSelection(SelectionMode.Block);

        node.ExitCopyMode();

        Assert.False(node.IsInCopyMode);
        Assert.False(node.HasSelection);
    }

    [Fact]
    public void MoveCursor_ClampsToBounds()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "x" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 10, 5));

        node.EnterCopyMode();
        node.SetCursor(0, 0);

        node.MoveCursor(-100, -100);
        Assert.Equal(0, node.CursorRow);
        Assert.Equal(0, node.CursorCol);

        node.MoveCursor(100, 100);
        Assert.Equal(4, node.CursorRow);
        Assert.Equal(9, node.CursorCol);
    }

    [Fact]
    public void StartOrToggleSelection_FirstCall_SetsAnchorAtCursor()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "x" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 10, 5));
        node.EnterCopyMode();
        node.SetCursor(2, 3);

        node.StartOrToggleSelection(SelectionMode.Character);

        Assert.True(node.HasSelection);
        Assert.Equal(2, node.AnchorRow);
        Assert.Equal(3, node.AnchorCol);
        Assert.Equal(SelectionMode.Character, node.CursorSelectionMode);
    }

    [Fact]
    public void StartOrToggleSelection_SameMode_ClearsSelection()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "x" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 10, 5));
        node.EnterCopyMode();
        node.SetCursor(2, 3);
        node.StartOrToggleSelection(SelectionMode.Character);

        node.StartOrToggleSelection(SelectionMode.Character);

        Assert.False(node.HasSelection);
    }

    [Fact]
    public void StartOrToggleSelection_DifferentMode_KeepsAnchorChangesMode()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "x" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 10, 5));
        node.EnterCopyMode();
        node.SetCursor(2, 3);
        node.StartOrToggleSelection(SelectionMode.Character);

        node.StartOrToggleSelection(SelectionMode.Block);

        Assert.True(node.HasSelection);
        Assert.Equal(2, node.AnchorRow);
        Assert.Equal(3, node.AnchorCol);
        Assert.Equal(SelectionMode.Block, node.CursorSelectionMode);
    }

    // ------------------------------------------------------------------
    // ConfigureDefaultBindings
    // ------------------------------------------------------------------

    [Fact]
    public void ConfigureDefaultBindings_NoHandler_RegistersNoBinding()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "x" } };
        var bindings = new InputBindingsBuilder();

        node.ConfigureDefaultBindings(bindings);

        Assert.Empty(bindings.Bindings);
    }

    [Fact]
    public void ConfigureDefaultBindings_WithHandler_RegistersGlobalEntry_AndCaptureOverrideActions()
    {
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "x" },
            CopyHandler = _ => Task.CompletedTask,
        };
        var bindings = new InputBindingsBuilder();

        node.ConfigureDefaultBindings(bindings);

        // Exactly one global binding — the F12 entry.
        var globals = bindings.Bindings.Where(b => b.IsGlobal).ToList();
        Assert.Single(globals);
        var entry = globals[0];
        Assert.Equal(SelectionPanelWidget.EnterCopyMode, entry.ActionId);
        Assert.Equal(Hex1bKey.F12, entry.Steps[0].Key);

        // Every other binding is a capture-override.
        var captureOverrides = bindings.Bindings.Where(b => !b.IsGlobal).ToList();
        Assert.NotEmpty(captureOverrides);
        Assert.All(captureOverrides, b => Assert.True(b.OverridesCapture,
            $"Non-global binding {b.ActionId} should be a capture-override binding."));

        // Spot-check key bindings exist.
        Assert.Contains(captureOverrides, b => b.Steps[0].Key == Hex1bKey.UpArrow && b.ActionId == SelectionPanelWidget.CopyModeUp);
        Assert.Contains(captureOverrides, b => b.Steps[0].Key == Hex1bKey.DownArrow && b.ActionId == SelectionPanelWidget.CopyModeDown);
        Assert.Contains(captureOverrides, b => b.Steps[0].Key == Hex1bKey.Y && b.ActionId == SelectionPanelWidget.CopyModeCopy);
        Assert.Contains(captureOverrides, b => b.Steps[0].Key == Hex1bKey.Enter && b.ActionId == SelectionPanelWidget.CopyModeCopy);
        Assert.Contains(captureOverrides, b => b.Steps[0].Key == Hex1bKey.Escape && b.ActionId == SelectionPanelWidget.CopyModeCancel);
        Assert.Contains(captureOverrides, b => b.Steps[0].Key == Hex1bKey.V && b.Steps[0].Modifiers == Hex1bModifiers.None && b.ActionId == SelectionPanelWidget.CopyModeStartSelection);
    }

    [Fact]
    public async Task EnterCopyMode_BindingHandler_SetsCopyMode_AndCapturesInput()
    {
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "x" },
            CopyHandler = _ => Task.CompletedTask,
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 10, 5));

        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);
        var entry = bindings.Bindings.Single(b => b.ActionId == SelectionPanelWidget.EnterCopyMode);

        var focusRing = new FocusRing();
        await entry.ExecuteAsync(new InputBindingActionContext(focusRing));

        Assert.True(node.IsInCopyMode);
        Assert.Same(node, focusRing.CapturedNode);
    }

    [Fact]
    public async Task CopyAction_WithSelection_InvokesHandler_ExitsCopyMode_ReleasesCapture()
    {
        string? captured = null;
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "Hello" },
            CopyHandler = text => { captured = text; return Task.CompletedTask; },
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 5, 1));

        node.EnterCopyMode();
        node.SetCursor(0, 0);
        node.StartOrToggleSelection(SelectionMode.Character);
        node.SetCursor(0, 4);

        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);
        var copy = bindings.Bindings.First(b => b.ActionId == SelectionPanelWidget.CopyModeCopy);

        var focusRing = new FocusRing();
        focusRing.CaptureInput(node);
        await copy.ExecuteAsync(new InputBindingActionContext(focusRing));

        Assert.Equal("Hello", captured);
        Assert.False(node.IsInCopyMode);
        Assert.Null(focusRing.CapturedNode);
    }

    [Fact]
    public async Task CopyAction_WithoutSelection_DoesNothing()
    {
        bool handlerCalled = false;
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "Hello" },
            CopyHandler = _ => { handlerCalled = true; return Task.CompletedTask; },
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 5, 1));
        node.EnterCopyMode();

        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);
        var copy = bindings.Bindings.First(b => b.ActionId == SelectionPanelWidget.CopyModeCopy);

        var focusRing = new FocusRing();
        focusRing.CaptureInput(node);
        await copy.ExecuteAsync(new InputBindingActionContext(focusRing));

        Assert.False(handlerCalled);
        // Still in copy mode (Y/Enter without selection is a no-op).
        Assert.True(node.IsInCopyMode);
        Assert.Same(node, focusRing.CapturedNode);
    }

    [Fact]
    public async Task CancelAction_ExitsCopyMode_WithoutInvokingHandler()
    {
        bool handlerCalled = false;
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "Hello" },
            CopyHandler = _ => { handlerCalled = true; return Task.CompletedTask; },
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 5, 1));
        node.EnterCopyMode();
        node.StartOrToggleSelection(SelectionMode.Character);

        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);
        var cancel = bindings.Bindings.First(b => b.ActionId == SelectionPanelWidget.CopyModeCancel);

        var focusRing = new FocusRing();
        focusRing.CaptureInput(node);
        await cancel.ExecuteAsync(new InputBindingActionContext(focusRing));

        Assert.False(handlerCalled);
        Assert.False(node.IsInCopyMode);
        Assert.Null(focusRing.CapturedNode);
    }

    [Fact]
    public async Task EnterCopyMode_WhileAlreadyInCopyMode_IsNoOp()
    {
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "x" },
            CopyHandler = _ => Task.CompletedTask,
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 10, 5));
        node.EnterCopyMode();
        node.SetCursor(2, 3);
        node.StartOrToggleSelection(SelectionMode.Block);

        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);
        var entry = bindings.Bindings.Single(b => b.ActionId == SelectionPanelWidget.EnterCopyMode);

        await entry.ExecuteAsync(new InputBindingActionContext(new FocusRing()));

        // Re-entry guarded — selection state preserved.
        Assert.True(node.IsInCopyMode);
        Assert.Equal(2, node.CursorRow);
        Assert.Equal(3, node.CursorCol);
        Assert.True(node.HasSelection);
    }

    // ------------------------------------------------------------------
    // Render: cursor + selection inversion overlay
    // ------------------------------------------------------------------

    [Fact]
    public void Render_NotInCopyMode_PassesThroughChild_NoInversion()
    {
        var painter = new PaintingTestNode();
        var node = new SelectionPanelNode { Child = painter };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 12, 8));

        var surface = new Surface(12, 8);
        var ctx = new SurfaceRenderContext(surface);
        node.Render(ctx);

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                Assert.True(surface.TryGetCell(x, y, out var cell));
                Assert.Equal(((char)('A' + y)).ToString(), cell.Character);
                Assert.False(cell.Attributes.HasFlag(CellAttributes.Reverse),
                    $"Cell ({x},{y}) should not be inverted outside copy mode.");
            }
        }
    }

    [Fact]
    public void Render_InCopyMode_NoSelection_InvertsCursorCellOnly()
    {
        var painter = new PaintingTestNode();
        var node = new SelectionPanelNode { Child = painter };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 12, 8));
        node.EnterCopyMode();
        node.SetCursor(3, 5);

        var surface = new Surface(12, 8);
        var ctx = new SurfaceRenderContext(surface);
        node.Render(ctx);

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                Assert.True(surface.TryGetCell(x, y, out var cell));
                bool expectInverted = (x == 5 && y == 3);
                Assert.Equal(expectInverted, cell.Attributes.HasFlag(CellAttributes.Reverse));
            }
        }
    }

    [Fact]
    public void Render_BlockSelection_InvertsRectangleRegion()
    {
        var painter = new PaintingTestNode();
        var node = new SelectionPanelNode { Child = painter };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 12, 8));
        node.EnterCopyMode();
        node.SetCursor(2, 3);
        node.StartOrToggleSelection(SelectionMode.Block);
        node.SetCursor(5, 8);

        var surface = new Surface(12, 8);
        var ctx = new SurfaceRenderContext(surface);
        node.Render(ctx);

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                Assert.True(surface.TryGetCell(x, y, out var cell));
                bool inside = y >= 2 && y <= 5 && x >= 3 && x <= 8;
                Assert.Equal(inside, cell.Attributes.HasFlag(CellAttributes.Reverse));
            }
        }
    }

    [Fact]
    public void Render_LineSelection_InvertsFullRows()
    {
        var painter = new PaintingTestNode();
        var node = new SelectionPanelNode { Child = painter };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 12, 8));
        node.EnterCopyMode();
        node.SetCursor(2, 5);
        node.StartOrToggleSelection(SelectionMode.Line);
        node.SetCursor(4, 0);

        var surface = new Surface(12, 8);
        var ctx = new SurfaceRenderContext(surface);
        node.Render(ctx);

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                Assert.True(surface.TryGetCell(x, y, out var cell));
                bool inside = y >= 2 && y <= 4;
                Assert.Equal(inside, cell.Attributes.HasFlag(CellAttributes.Reverse));
            }
        }
    }

    [Fact]
    public void Render_CharacterSelection_InvertsStreamRegion()
    {
        var painter = new PaintingTestNode();
        var node = new SelectionPanelNode { Child = painter };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 12, 8));
        node.EnterCopyMode();
        node.SetCursor(2, 3);
        node.StartOrToggleSelection(SelectionMode.Character);
        node.SetCursor(5, 8);

        var surface = new Surface(12, 8);
        var ctx = new SurfaceRenderContext(surface);
        node.Render(ctx);

        // Top row (y=2): cols 3..end
        // Middle rows (y=3,4): full width
        // Bottom row (y=5): cols 0..8
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                Assert.True(surface.TryGetCell(x, y, out var cell));
                bool inside =
                    (y == 2 && x >= 3) ||
                    (y > 2 && y < 5) ||
                    (y == 5 && x <= 8);
                Assert.Equal(inside, cell.Attributes.HasFlag(CellAttributes.Reverse));
            }
        }
    }

    /// <summary>
    /// Test render node that paints every cell in its arranged bounds with a
    /// per-row deterministic character so cell-extraction tests can assert
    /// exactly which cells were picked.
    /// </summary>
    private sealed class PaintingTestNode : Hex1bNode
    {
        protected override Size MeasureCore(Constraints constraints)
            => constraints.Constrain(new Size(constraints.MaxWidth, constraints.MaxHeight));

        public override void Render(Hex1bRenderContext context)
        {
            var bounds = Bounds;
            for (int y = 0; y < bounds.Height; y++)
            {
                var ch = (char)('A' + (y % 26));
                var line = new string(ch, bounds.Width);
                context.SetCursorPosition(bounds.X, bounds.Y + y);
                context.Write(line);
            }
        }
    }

    [Fact]
    public void ConfigureDefaultBindings_WithHandler_RegistersThreeDragBindings()
    {
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "x" },
            CopyHandler = _ => Task.CompletedTask,
        };
        var bindings = new InputBindingsBuilder();

        node.ConfigureDefaultBindings(bindings);

        Assert.Equal(3, bindings.DragBindings.Count);
        Assert.All(bindings.DragBindings, b => Assert.Equal(MouseButton.Left, b.Button));

        var modifierSets = bindings.DragBindings.Select(b => b.Modifiers).ToHashSet();
        Assert.Contains(Hex1bModifiers.None, modifierSets);
        Assert.Contains(Hex1bModifiers.Shift, modifierSets);
        Assert.Contains(Hex1bModifiers.Alt, modifierSets);
    }

    [Fact]
    public void ConfigureDefaultBindings_WithoutHandler_RegistersNoDragBindings()
    {
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "x" },
        };
        var bindings = new InputBindingsBuilder();

        node.ConfigureDefaultBindings(bindings);

        Assert.Empty(bindings.DragBindings);
    }

    [Fact]
    public void DragBinding_StartDrag_EntersCopyMode_AnchorsCursor_StartsCharacterSelection()
    {
        var node = SetupArrangedNode(width: 20, height: 5);
        var dragBinding = node.BuildBindings().DragBindings.Single(b => b.Modifiers == Hex1bModifiers.None);

        // Drag begins at local (4, 2) within the node's surface.
        var handler = dragBinding.StartDrag(4, 2);

        Assert.True(node.IsInCopyMode);
        Assert.Equal(2, node.CursorRow);
        Assert.Equal(4, node.CursorCol);
        Assert.True(node.HasSelection);
        Assert.Equal(2, node.AnchorRow);
        Assert.Equal(4, node.AnchorCol);
        Assert.Equal(SelectionMode.Character, node.CursorSelectionMode);
        Assert.False(handler.IsEmpty);
    }

    [Fact]
    public void DragBinding_OnMove_UpdatesCursorRelativeToAnchor()
    {
        var node = SetupArrangedNode(width: 20, height: 10);
        var dragBinding = node.BuildBindings().DragBindings.Single(b => b.Modifiers == Hex1bModifiers.None);

        var handler = dragBinding.StartDrag(3, 1);
        var ctx = new InputBindingActionContext(new FocusRing());

        handler.OnMove?.Invoke(ctx, 5, 2);

        Assert.True(node.IsInCopyMode);
        Assert.Equal(1, node.AnchorRow);
        Assert.Equal(3, node.AnchorCol);
        Assert.Equal(3, node.CursorRow);
        Assert.Equal(8, node.CursorCol);
    }

    [Fact]
    public void DragBinding_OnMove_ClampsToNodeBounds()
    {
        var node = SetupArrangedNode(width: 10, height: 5);
        var dragBinding = node.BuildBindings().DragBindings.Single(b => b.Modifiers == Hex1bModifiers.None);

        var handler = dragBinding.StartDrag(5, 2);
        var ctx = new InputBindingActionContext(new FocusRing());

        handler.OnMove?.Invoke(ctx, 100, 100);

        Assert.Equal(4, node.CursorRow);
        Assert.Equal(9, node.CursorCol);
    }

    [Fact]
    public void DragBinding_OnEnd_InstallsKeyboardCapture()
    {
        var node = SetupArrangedNode(width: 20, height: 5);
        var dragBinding = node.BuildBindings().DragBindings.Single(b => b.Modifiers == Hex1bModifiers.None);
        var focusRing = new FocusRing();

        var handler = dragBinding.StartDrag(2, 1);
        handler.OnEnd?.Invoke(new InputBindingActionContext(focusRing));

        Assert.True(node.IsInCopyMode);
        Assert.Same(node, focusRing.CapturedNode);
    }

    [Fact]
    public void ShiftDrag_StartsLineSelection()
    {
        var node = SetupArrangedNode(width: 20, height: 5);
        var dragBinding = node.BuildBindings().DragBindings.Single(b => b.Modifiers == Hex1bModifiers.Shift);

        dragBinding.StartDrag(4, 2);

        Assert.True(node.IsInCopyMode);
        Assert.Equal(SelectionMode.Line, node.CursorSelectionMode);
        Assert.True(node.HasSelection);
    }

    [Fact]
    public void AltDrag_StartsBlockSelection()
    {
        var node = SetupArrangedNode(width: 20, height: 5);
        var dragBinding = node.BuildBindings().DragBindings.Single(b => b.Modifiers == Hex1bModifiers.Alt);

        dragBinding.StartDrag(4, 2);

        Assert.True(node.IsInCopyMode);
        Assert.Equal(SelectionMode.Block, node.CursorSelectionMode);
        Assert.True(node.HasSelection);
    }

    [Fact]
    public void DragBinding_NegativeLocalCoordinates_ClampToZero()
    {
        // When SelectionPanel is inside a scrolled ScrollPanel, Bounds.Y can be
        // negative. The drag binding must not anchor at a negative position.
        var node = SetupArrangedNode(width: 20, height: 5);
        var dragBinding = node.BuildBindings().DragBindings.Single(b => b.Modifiers == Hex1bModifiers.None);

        dragBinding.StartDrag(-3, -2);

        Assert.True(node.IsInCopyMode);
        Assert.Equal(0, node.CursorRow);
        Assert.Equal(0, node.CursorCol);
        Assert.Equal(0, node.AnchorRow);
        Assert.Equal(0, node.AnchorCol);
    }

    private static SelectionPanelNode SetupArrangedNode(int width, int height)
    {
        var child = new TextBlockNode { Text = string.Empty };
        var node = new SelectionPanelNode
        {
            Child = child,
            CopyHandler = _ => Task.CompletedTask,
        };
        node.Measure(new Constraints(0, width, 0, height));
        node.Arrange(new Rect(0, 0, width, height));
        return node;
    }
}
