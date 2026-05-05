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
            CopyHandler = args => { captured = args.Text; return Task.CompletedTask; },
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
    public void ConfigureDefaultBindings_WithHandler_RegistersFourDragBindings()
    {
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "x" },
            CopyHandler = _ => Task.CompletedTask,
        };
        var bindings = new InputBindingsBuilder();

        node.ConfigureDefaultBindings(bindings);

        Assert.Equal(4, bindings.DragBindings.Count);
        Assert.All(bindings.DragBindings, b => Assert.Equal(MouseButton.Left, b.Button));

        var modifierSets = bindings.DragBindings.Select(b => b.Modifiers).ToHashSet();
        Assert.Contains(Hex1bModifiers.None, modifierSets);
        Assert.Contains(Hex1bModifiers.Control, modifierSets);
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
        // Cursor is computed from absolute mouse coords minus current Bounds,
        // so the test must construct a context with explicit mouse coords.
        // Node is arranged at (0, 0) → mouse (8, 3) ≡ panel-local (8, 3).
        var ctx = new InputBindingActionContext(new FocusRing(), mouseX: 8, mouseY: 3);

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
        // Mouse far outside panel bounds → cursor clamps to last valid cell.
        var ctx = new InputBindingActionContext(new FocusRing(), mouseX: 100, mouseY: 100);

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

    [Fact]
    public void DragBinding_OnMove_TracksMouseAfterBoundsShift()
    {
        // When an enclosing ScrollPanel scrolls mid-drag, this node's Bounds.Y
        // changes (the panel slides in terminal coords). The drag handler must
        // compute the cursor from absolute mouse coords against CURRENT Bounds
        // — not from the original anchor + a delta — so the highlight tracks
        // whatever cell is now under the (stationary) mouse pointer.
        var child = new TextBlockNode { Text = string.Empty };
        var node = new SelectionPanelNode
        {
            Child = child,
            CopyHandler = _ => Task.CompletedTask,
        };
        node.Measure(new Constraints(0, 20, 0, 50));
        node.Arrange(new Rect(0, 0, 20, 50));

        var dragBinding = node.BuildBindings().DragBindings.Single(b => b.Modifiers == Hex1bModifiers.None);
        var handler = dragBinding.StartDrag(5, 10);

        // Initial drag-move at terminal (5, 10): cursor at panel-local (5, 10).
        var ctxBefore = new InputBindingActionContext(new FocusRing(), mouseX: 5, mouseY: 10);
        handler.OnMove?.Invoke(ctxBefore, 0, 0);
        Assert.Equal(10, node.CursorRow);
        Assert.Equal(5, node.CursorCol);

        // Simulate scroll: panel slides up by 3 rows (Bounds.Y goes from 0 to -3).
        node.Arrange(new Rect(0, -3, 20, 50));

        // Mouse stationary at terminal (5, 10) — but with shifted bounds, the
        // cell under the mouse is now panel-local row 13 (10 - (-3)).
        var ctxAfter = new InputBindingActionContext(new FocusRing(), mouseX: 5, mouseY: 10);
        handler.OnMove?.Invoke(ctxAfter, 0, 0);
        Assert.Equal(13, node.CursorRow);
        Assert.Equal(5, node.CursorCol);
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

    // ------------------------------------------------------------------
    // BuildCopyEventArgs: rich payload with bounds + per-node breakdown.
    // ------------------------------------------------------------------

    [Fact]
    public void BuildCopyEventArgs_NoSelection_ReturnsEmptyPayload()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "Hello" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 5, 1));
        node.EnterCopyMode();

        var args = node.BuildCopyEventArgs();

        Assert.Equal(string.Empty, args.Text);
        Assert.Equal(Rect.Zero, args.PanelBounds);
        Assert.Equal(Rect.Zero, args.TerminalBounds);
        Assert.Empty(args.Nodes);
    }

    [Fact]
    public void BuildCopyEventArgs_BlockSelection_ProducesExactBoundsAndIntersections()
    {
        // Layout: SelectionPanel at (10, 5) wraps a TextBlock that fills it.
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "ABCDEFGHIJ" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(10, 5, 10, 1));

        node.EnterCopyMode();
        node.SetCursor(0, 2);
        node.StartOrToggleSelection(SelectionMode.Block);
        node.SetCursor(0, 5);

        var args = node.BuildCopyEventArgs();

        Assert.Equal(SelectionMode.Block, args.Mode);
        Assert.Equal(new Rect(2, 0, 4, 1), args.PanelBounds);
        Assert.Equal(new Rect(12, 5, 4, 1), args.TerminalBounds);

        // The TextBlock child fills the panel so its bounds intersect.
        var textEntry = Assert.Single(args.Nodes, n => n.Node is TextBlockNode);
        Assert.True(textEntry.IntersectionInTerminal == new Rect(12, 5, 4, 1));
        Assert.False(textEntry.IsFullySelected); // only 4 of 10 cols selected
    }

    [Fact]
    public void BuildCopyEventArgs_LineSelection_FullsRowsAreFullySelected()
    {
        // VStack with three single-row TextBlocks; line-select all three.
        var t1 = new TextBlockNode { Text = "AAA" };
        var t2 = new TextBlockNode { Text = "BBB" };
        var t3 = new TextBlockNode { Text = "CCC" };
        var stack = new VStackNode();
        stack.Children.Add(t1); stack.Children.Add(t2); stack.Children.Add(t3);

        var node = new SelectionPanelNode { Child = stack };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 3, 3));

        node.EnterCopyMode();
        node.SetCursor(0, 0);
        node.StartOrToggleSelection(SelectionMode.Line);
        node.SetCursor(2, 0);

        var args = node.BuildCopyEventArgs();

        Assert.Equal(SelectionMode.Line, args.Mode);
        Assert.Equal(new Rect(0, 0, 3, 3), args.PanelBounds);

        // All three TextBlocks should be fully selected (each is one full row).
        var leafEntries = args.Nodes.Where(n => n.Node is TextBlockNode).ToList();
        Assert.Equal(3, leafEntries.Count);
        Assert.All(leafEntries, e => Assert.True(e.IsFullySelected));
    }

    [Fact]
    public void BuildCopyEventArgs_LineSelection_PartialRowSpan_OnlyIntersectingNodesIncluded()
    {
        var t1 = new TextBlockNode { Text = "AAA" };
        var t2 = new TextBlockNode { Text = "BBB" };
        var t3 = new TextBlockNode { Text = "CCC" };
        var stack = new VStackNode();
        stack.Children.Add(t1); stack.Children.Add(t2); stack.Children.Add(t3);

        var node = new SelectionPanelNode { Child = stack };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 3, 3));

        node.EnterCopyMode();
        node.SetCursor(1, 0);
        node.StartOrToggleSelection(SelectionMode.Line);
        node.SetCursor(1, 0); // single row

        var args = node.BuildCopyEventArgs();

        Assert.Equal(new Rect(0, 1, 3, 1), args.PanelBounds);

        var leafEntries = args.Nodes.Where(n => n.Node is TextBlockNode).ToList();
        var single = Assert.Single(leafEntries);
        Assert.Same(t2, single.Node);
        Assert.True(single.IsFullySelected);
        Assert.Equal(new Rect(0, 1, 3, 1), single.IntersectionInTerminal);
        Assert.Equal(new Rect(0, 0, 3, 1), single.IntersectionInNode);
    }

    [Fact]
    public void BuildCopyEventArgs_TerminalBounds_TranslatedByPanelOrigin()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "Hello" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(20, 30, 5, 1));

        node.EnterCopyMode();
        node.SetCursor(0, 0);
        node.StartOrToggleSelection(SelectionMode.Block);
        node.SetCursor(0, 4);

        var args = node.BuildCopyEventArgs();

        Assert.Equal(new Rect(0, 0, 5, 1), args.PanelBounds);
        Assert.Equal(new Rect(20, 30, 5, 1), args.TerminalBounds);
    }

    [Fact]
    public void BuildCopyEventArgs_BlockSelection_PartialNodeReportsCorrectIntersectionInNode()
    {
        // TextBlock at (10, 10). Selection cuts off the last 2 columns.
        var text = new TextBlockNode { Text = "ABCDEFGHIJ" };
        var node = new SelectionPanelNode { Child = text };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(10, 10, 10, 1));

        node.EnterCopyMode();
        node.SetCursor(0, 0);
        node.StartOrToggleSelection(SelectionMode.Block);
        node.SetCursor(0, 7);

        var args = node.BuildCopyEventArgs();

        var entry = Assert.Single(args.Nodes, n => n.Node is TextBlockNode);
        Assert.False(entry.IsFullySelected);
        Assert.Equal(new Rect(0, 0, 8, 1), entry.IntersectionInNode);
        Assert.Equal(new Rect(10, 10, 8, 1), entry.IntersectionInTerminal);
    }

    [Fact]
    public void BuildCopyEventArgs_StreamSelection_NodeOnStartRowBeforeStartColumn_IsExcluded()
    {
        // Three single-row siblings on row 0: [LEFT][MIDDLE][RIGHT]
        // Stream selection from (row 0, col 6) → (row 2, col 0).
        // The bbox of that selection is the FULL panel width on row 0,
        // but cells (0, 0..5) on the start row are NOT actually selected.
        // The LEFT node sits at columns 0..2 — it must NOT appear in
        // args.Nodes.
        var left   = new TextBlockNode { Text = "LFT" };
        var middle = new TextBlockNode { Text = "MID" };
        var right  = new TextBlockNode { Text = "RGT" };
        var rowStack = new HStackNode();
        rowStack.Children.Add(left);
        rowStack.Children.Add(middle);
        rowStack.Children.Add(right);
        // Three rows of content so we can stream across rows.
        var t1 = new TextBlockNode { Text = "..." };
        var t2 = new TextBlockNode { Text = "..." };
        var col = new VStackNode();
        col.Children.Add(rowStack);
        col.Children.Add(t1);
        col.Children.Add(t2);

        var node = new SelectionPanelNode { Child = col };
        node.Measure(new Constraints(0, 9, 0, 3));
        node.Arrange(new Rect(0, 0, 9, 3));

        node.EnterCopyMode();
        node.SetCursor(0, 6);
        node.StartOrToggleSelection(SelectionMode.Character);
        node.SetCursor(2, 0);

        var args = node.BuildCopyEventArgs();

        // bbox spans full panel width across all 3 rows
        Assert.Equal(new Rect(0, 0, 9, 3), args.PanelBounds);

        // LEFT (cols 0..2 on row 0) must be filtered out — start-row
        // selected cells start at col 6.
        Assert.DoesNotContain(args.Nodes, n => ReferenceEquals(n.Node, left));

        // MIDDLE (cols 3..5 on row 0) is also entirely before col 6
        // and must be filtered out.
        Assert.DoesNotContain(args.Nodes, n => ReferenceEquals(n.Node, middle));

        // RIGHT (cols 6..8 on row 0) is at/after col 6 — included.
        Assert.Contains(args.Nodes, n => ReferenceEquals(n.Node, right));
    }

    [Fact]
    public void BuildCopyEventArgs_StreamSelection_StartRowNodeReportsClippedIntersection()
    {
        // Single TextBlock spanning the full width of row 0; stream
        // selection from (row 0, col 4) → (row 1, col 1). The node's
        // intersection must reflect cells [4..endOfRow), NOT the full
        // bbox row 0.
        var startRowText = new TextBlockNode { Text = "ABCDEFGH" };
        var nextRowText  = new TextBlockNode { Text = "IJKLMNOP" };
        var col = new VStackNode();
        col.Children.Add(startRowText);
        col.Children.Add(nextRowText);

        var node = new SelectionPanelNode { Child = col };
        node.Measure(new Constraints(0, 8, 0, 2));
        node.Arrange(new Rect(0, 0, 8, 2));

        node.EnterCopyMode();
        node.SetCursor(0, 4);
        node.StartOrToggleSelection(SelectionMode.Character);
        node.SetCursor(1, 1);

        var args = node.BuildCopyEventArgs();

        var startEntry = Assert.Single(args.Nodes, n => ReferenceEquals(n.Node, startRowText));
        // Start-row selection runs from col 4 to end-of-row (col 7), so
        // 4 cells wide starting at col 4. In node-local coords that is
        // (4, 0, 4, 1).
        Assert.Equal(new Rect(4, 0, 4, 1), startEntry.IntersectionInNode);
        Assert.Equal(new Rect(4, 0, 4, 1), startEntry.IntersectionInTerminal);
        Assert.False(startEntry.IsFullySelected);

        var endEntry = Assert.Single(args.Nodes, n => ReferenceEquals(n.Node, nextRowText));
        // End-row selection runs from col 0 to col 1 inclusive → 2 wide.
        Assert.Equal(new Rect(0, 0, 2, 1), endEntry.IntersectionInNode);
        Assert.Equal(new Rect(0, 1, 2, 1), endEntry.IntersectionInTerminal);
        Assert.False(endEntry.IsFullySelected);
    }

    [Fact]
    public void BuildCopyEventArgs_StreamSelection_MiddleRowNode_IsFullySelected()
    {
        // Three single-row TextBlocks. Stream from (row 0, col 1) to
        // (row 2, col 1) makes row 1 fully covered (full row width).
        // Verifies the per-row exact-coverage check supersedes the old
        // 4-corner approximation flagged by the rubber-duck review.
        var t0 = new TextBlockNode { Text = "AAA" };
        var t1 = new TextBlockNode { Text = "BBB" };
        var t2 = new TextBlockNode { Text = "CCC" };
        var col = new VStackNode();
        col.Children.Add(t0);
        col.Children.Add(t1);
        col.Children.Add(t2);

        var node = new SelectionPanelNode { Child = col };
        node.Measure(new Constraints(0, 3, 0, 3));
        node.Arrange(new Rect(0, 0, 3, 3));

        node.EnterCopyMode();
        node.SetCursor(0, 1);
        node.StartOrToggleSelection(SelectionMode.Character);
        node.SetCursor(2, 1);

        var args = node.BuildCopyEventArgs();

        var middleEntry = Assert.Single(args.Nodes, n => ReferenceEquals(n.Node, t1));
        Assert.True(middleEntry.IsFullySelected);
        Assert.Equal(new Rect(0, 0, 3, 1), middleEntry.IntersectionInNode);
        Assert.Equal(new Rect(0, 1, 3, 1), middleEntry.IntersectionInTerminal);

        // Start-row node only partially selected (cols 1..2).
        var startEntry = Assert.Single(args.Nodes, n => ReferenceEquals(n.Node, t0));
        Assert.False(startEntry.IsFullySelected);
        Assert.Equal(new Rect(1, 0, 2, 1), startEntry.IntersectionInNode);

        // End-row node only partially selected (cols 0..1).
        var endEntry = Assert.Single(args.Nodes, n => ReferenceEquals(n.Node, t2));
        Assert.False(endEntry.IsFullySelected);
        Assert.Equal(new Rect(0, 0, 2, 1), endEntry.IntersectionInNode);
    }

    // ------------------------------------------------------------------
    // Right-click commit binding.
    // ------------------------------------------------------------------

    [Fact]
    public void ConfigureDefaultBindings_WithHandler_RegistersRightClickMouseBinding()
    {
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "Hello" },
            CopyHandler = _ => Task.CompletedTask,
        };

        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);

        var mouseCommit = bindings.MouseBindings
            .Where(b => b.Button == MouseButton.Right && b.OverridesCapture)
            .ToList();
        Assert.Single(mouseCommit);
        Assert.Equal(SelectionPanelWidget.CopyModeMouseCommit, mouseCommit[0].ActionId);
    }

    [Fact]
    public void ConfigureDefaultBindings_WithoutHandler_DoesNotRegisterRightClickMouseBinding()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "Hello" } };

        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);

        Assert.Empty(bindings.MouseBindings);
    }

    [Fact]
    public async Task RightClickCommit_WithSelection_InvokesHandler_ExitsCopyMode_ReleasesCapture()
    {
        SelectionPanelCopyEventArgs? captured = null;
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "Hello" },
            CopyHandler = args => { captured = args; return Task.CompletedTask; },
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 5, 1));

        node.EnterCopyMode();
        node.SetCursor(0, 0);
        node.StartOrToggleSelection(SelectionMode.Block);
        node.SetCursor(0, 4);

        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);
        var rightClick = bindings.MouseBindings.Single(b =>
            b.Button == MouseButton.Right && b.ActionId == SelectionPanelWidget.CopyModeMouseCommit);

        var focusRing = new FocusRing();
        focusRing.CaptureInput(node);
        await rightClick.ExecuteAsync(new InputBindingActionContext(focusRing));

        Assert.NotNull(captured);
        Assert.Equal("Hello", captured!.Text);
        Assert.Equal(SelectionMode.Block, captured.Mode);
        Assert.False(node.IsInCopyMode);
        Assert.Null(focusRing.CapturedNode);
    }

    [Fact]
    public async Task RightClickCommit_WithoutSelection_IsNoOp()
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
        // No StartOrToggleSelection — cursor only.

        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);
        var rightClick = bindings.MouseBindings.Single(b =>
            b.Button == MouseButton.Right && b.ActionId == SelectionPanelWidget.CopyModeMouseCommit);

        var focusRing = new FocusRing();
        focusRing.CaptureInput(node);
        await rightClick.ExecuteAsync(new InputBindingActionContext(focusRing));

        Assert.False(handlerCalled);
        // Stays in copy mode and keeps capture (matches Y/Enter no-op semantics).
        Assert.True(node.IsInCopyMode);
        Assert.Same(node, focusRing.CapturedNode);
    }

    [Fact]
    public async Task RightClickCommit_OutsideCopyMode_IsNoOp()
    {
        bool handlerCalled = false;
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "Hello" },
            CopyHandler = _ => { handlerCalled = true; return Task.CompletedTask; },
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 5, 1));

        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);
        var rightClick = bindings.MouseBindings.Single(b =>
            b.Button == MouseButton.Right && b.ActionId == SelectionPanelWidget.CopyModeMouseCommit);

        await rightClick.ExecuteAsync(new InputBindingActionContext(new FocusRing()));

        Assert.False(handlerCalled);
        Assert.False(node.IsInCopyMode);
    }

    // ------------------------------------------------------------------
    // OnCopy overloads on the widget pass through the args correctly.
    // ------------------------------------------------------------------

    [Fact]
    public async Task OnCopy_StringOverload_ReceivesText()
    {
        string? received = null;
        var widget = new SelectionPanelWidget(new TextBlockWidget("Hello"))
            .OnCopy((string s) => { received = s; });

        var args = new SelectionPanelCopyEventArgs(
            "TheText", SelectionMode.Character, Rect.Zero, Rect.Zero, []);
        await widget.CopyHandler!(args);

        Assert.Equal("TheText", received);
    }

    [Fact]
    public async Task OnCopy_ArgsOverload_ReceivesFullPayload()
    {
        SelectionPanelCopyEventArgs? received = null;
        var widget = new SelectionPanelWidget(new TextBlockWidget("Hello"))
            .OnCopy((SelectionPanelCopyEventArgs a) => { received = a; });

        var args = new SelectionPanelCopyEventArgs(
            "X", SelectionMode.Line, new Rect(1, 2, 3, 4), new Rect(5, 6, 7, 8), []);
        await widget.CopyHandler!(args);

        Assert.Same(args, received);
    }
}
