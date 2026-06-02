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
[TestClass]
public class SelectionPanelNodeTests
{
    // ------------------------------------------------------------------
    // Pass-through tests (outside copy mode)
    // ------------------------------------------------------------------

    [TestMethod]
    public void Measure_ReturnsChildSize()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = new SelectionPanelNode { Child = child };

        var size = node.Measure(Constraints.Unbounded);

        Assert.AreEqual(5, size.Width);
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public void Measure_WithNoChild_ReturnsZero()
    {
        var node = new SelectionPanelNode { Child = null };

        var size = node.Measure(Constraints.Unbounded);

        Assert.AreEqual(0, size.Width);
        Assert.AreEqual(0, size.Height);
    }

    [TestMethod]
    public void Arrange_ForwardsRectToChild()
    {
        var child = new TextBlockNode { Text = "Hi" };
        var node = new SelectionPanelNode { Child = child };
        var rect = new Rect(3, 4, 10, 2);

        node.Measure(Constraints.Unbounded);
        node.Arrange(rect);

        Assert.AreEqual(rect, child.Bounds);
        Assert.AreEqual(rect, node.Bounds);
    }

    [TestMethod]
    public void IsFocusable_IsFalse()
    {
        var node = new SelectionPanelNode();

        Assert.IsFalse(node.IsFocusable);
    }

    [TestMethod]
    public void GetFocusableNodes_ReturnsChildFocusables()
    {
        var child = new TextBoxNode { Text = "x" };
        var node = new SelectionPanelNode { Child = child };

        var focusables = node.GetFocusableNodes().ToList();

        TestSeq.Single(focusables);
        Assert.AreSame(child, focusables[0]);
    }

    [TestMethod]
    public void GetChildren_ReturnsChild()
    {
        var child = new TextBlockNode { Text = "x" };
        var node = new SelectionPanelNode { Child = child };

        var children = node.GetChildren().ToList();

        TestSeq.Single(children);
        Assert.AreSame(child, children[0]);
    }

    [TestMethod]
    public void IsFocused_SetterForwardsToChild()
    {
        var child = new TextBoxNode { Text = "" };
        var node = new SelectionPanelNode { Child = child };

        node.IsFocused = true;

        Assert.IsTrue(child.IsFocused);
    }

    // ------------------------------------------------------------------
    // SnapshotText (selection-driven)
    // ------------------------------------------------------------------

    [TestMethod]
    public void SnapshotText_NoSelection_ReturnsEmpty()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "Hello" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 5, 1));

        // No anchor set → empty.
        Assert.AreEqual(string.Empty, node.SnapshotText());
    }

    [TestMethod]
    public void SnapshotText_NoChild_ReturnsEmpty()
    {
        var node = new SelectionPanelNode { Child = null };
        Assert.AreEqual(string.Empty, node.SnapshotText());
    }

    [TestMethod]
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

        Assert.AreEqual("CCCCCC", node.SnapshotText());
    }

    [TestMethod]
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

        Assert.AreEqual("CCCCCCCCCCCC\nDDDDDDDDDDDD\nEEEEEEEEEEEE\nFFFFFFFFFFFF", node.SnapshotText().Replace("\r\n", "\n"));
    }

    [TestMethod]
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

        Assert.AreEqual("CCCCCC\nDDDDDD\nEEEEEE\nFFFFFF", node.SnapshotText().Replace("\r\n", "\n"));
    }

    [TestMethod]
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

        Assert.AreEqual("CCCCCCCCC\nDDDDDDDDDDDD\nEEEEEEEEEEEE\nFFFFFFFFF", node.SnapshotText().Replace("\r\n", "\n"));
    }

    // ------------------------------------------------------------------
    // EnterCopyMode / ExitCopyMode / cursor + anchor state
    // ------------------------------------------------------------------

    [TestMethod]
    public void EnterCopyMode_StartsAtBottomLeft_AndClearsAnchor()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "x" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 12, 8));

        node.EnterCopyMode();

        Assert.IsTrue(node.IsInCopyMode);
        Assert.AreEqual(7, node.CursorRow);
        Assert.AreEqual(0, node.CursorCol);
        Assert.IsFalse(node.HasSelection);
        Assert.AreEqual(SelectionMode.Character, node.CursorSelectionMode);
    }

    [TestMethod]
    public void ExitCopyMode_ClearsAllState()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "x" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 12, 8));
        node.EnterCopyMode();
        node.StartOrToggleSelection(SelectionMode.Block);

        node.ExitCopyMode();

        Assert.IsFalse(node.IsInCopyMode);
        Assert.IsFalse(node.HasSelection);
    }

    [TestMethod]
    public void MoveCursor_ClampsToBounds()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "x" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 10, 5));

        node.EnterCopyMode();
        node.SetCursor(0, 0);

        node.MoveCursor(-100, -100);
        Assert.AreEqual(0, node.CursorRow);
        Assert.AreEqual(0, node.CursorCol);

        node.MoveCursor(100, 100);
        Assert.AreEqual(4, node.CursorRow);
        Assert.AreEqual(9, node.CursorCol);
    }

    [TestMethod]
    public void StartOrToggleSelection_FirstCall_SetsAnchorAtCursor()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "x" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 10, 5));
        node.EnterCopyMode();
        node.SetCursor(2, 3);

        node.StartOrToggleSelection(SelectionMode.Character);

        Assert.IsTrue(node.HasSelection);
        Assert.AreEqual(2, node.AnchorRow);
        Assert.AreEqual(3, node.AnchorCol);
        Assert.AreEqual(SelectionMode.Character, node.CursorSelectionMode);
    }

    [TestMethod]
    public void StartOrToggleSelection_SameMode_ClearsSelection()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "x" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 10, 5));
        node.EnterCopyMode();
        node.SetCursor(2, 3);
        node.StartOrToggleSelection(SelectionMode.Character);

        node.StartOrToggleSelection(SelectionMode.Character);

        Assert.IsFalse(node.HasSelection);
    }

    [TestMethod]
    public void StartOrToggleSelection_DifferentMode_KeepsAnchorChangesMode()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "x" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 10, 5));
        node.EnterCopyMode();
        node.SetCursor(2, 3);
        node.StartOrToggleSelection(SelectionMode.Character);

        node.StartOrToggleSelection(SelectionMode.Block);

        Assert.IsTrue(node.HasSelection);
        Assert.AreEqual(2, node.AnchorRow);
        Assert.AreEqual(3, node.AnchorCol);
        Assert.AreEqual(SelectionMode.Block, node.CursorSelectionMode);
    }

    // ------------------------------------------------------------------
    // ConfigureDefaultBindings
    // ------------------------------------------------------------------

    [TestMethod]
    public void ConfigureDefaultBindings_NoHandler_RegistersNoBinding()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "x" } };
        var bindings = new InputBindingsBuilder();

        node.ConfigureDefaultBindings(bindings);

        Assert.IsEmpty(bindings.Bindings);
    }

    [TestMethod]
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
        TestSeq.Single(globals);
        var entry = globals[0];
        Assert.AreEqual(SelectionPanelWidget.EnterCopyMode, entry.ActionId);
        Assert.AreEqual(Hex1bKey.F12, entry.Steps[0].Key);

        // Every other binding is a capture-override.
        var captureOverrides = bindings.Bindings.Where(b => !b.IsGlobal).ToList();
        Assert.IsNotEmpty(captureOverrides);
        TestSeq.All(captureOverrides, b => Assert.IsTrue(b.OverridesCapture, $"Non-global binding {b.ActionId} should be a capture-override binding."));

        // Spot-check key bindings exist.
        Assert.IsTrue(captureOverrides.Any(b => b.Steps[0].Key == Hex1bKey.UpArrow && b.ActionId == SelectionPanelWidget.CopyModeUp));
        Assert.IsTrue(captureOverrides.Any(b => b.Steps[0].Key == Hex1bKey.DownArrow && b.ActionId == SelectionPanelWidget.CopyModeDown));
        Assert.IsTrue(captureOverrides.Any(b => b.Steps[0].Key == Hex1bKey.Y && b.ActionId == SelectionPanelWidget.CopyModeCopy));
        Assert.IsTrue(captureOverrides.Any(b => b.Steps[0].Key == Hex1bKey.Enter && b.ActionId == SelectionPanelWidget.CopyModeCopy));
        Assert.IsTrue(captureOverrides.Any(b => b.Steps[0].Key == Hex1bKey.Escape && b.ActionId == SelectionPanelWidget.CopyModeCancel));
        Assert.IsTrue(captureOverrides.Any(b => b.Steps[0].Key == Hex1bKey.V && b.Steps[0].Modifiers == Hex1bModifiers.None && b.ActionId == SelectionPanelWidget.CopyModeStartSelection));
    }

    [TestMethod]
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

        Assert.IsTrue(node.IsInCopyMode);
        Assert.AreSame(node, focusRing.CapturedNode);
    }

    [TestMethod]
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

        Assert.AreEqual("Hello", captured);
        Assert.IsFalse(node.IsInCopyMode);
        Assert.IsNull(focusRing.CapturedNode);
    }

    [TestMethod]
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

        Assert.IsFalse(handlerCalled);
        // Still in copy mode (Y/Enter without selection is a no-op).
        Assert.IsTrue(node.IsInCopyMode);
        Assert.AreSame(node, focusRing.CapturedNode);
    }

    [TestMethod]
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

        Assert.IsFalse(handlerCalled);
        Assert.IsFalse(node.IsInCopyMode);
        Assert.IsNull(focusRing.CapturedNode);
    }

    [TestMethod]
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
        Assert.IsTrue(node.IsInCopyMode);
        Assert.AreEqual(2, node.CursorRow);
        Assert.AreEqual(3, node.CursorCol);
        Assert.IsTrue(node.HasSelection);
    }

    // ------------------------------------------------------------------
    // Render: cursor + selection inversion overlay
    // ------------------------------------------------------------------

    [TestMethod]
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
                Assert.IsTrue(surface.TryGetCell(x, y, out var cell));
                Assert.AreEqual(((char)('A' + y)).ToString(), cell.Character);
                Assert.IsFalse(cell.Attributes.HasFlag(CellAttributes.Reverse), $"Cell ({x},{y}) should not be inverted outside copy mode.");
            }
        }
    }

    [TestMethod]
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
                Assert.IsTrue(surface.TryGetCell(x, y, out var cell));
                bool expectInverted = (x == 5 && y == 3);
                Assert.AreEqual(expectInverted, cell.Attributes.HasFlag(CellAttributes.Reverse));
            }
        }
    }

    [TestMethod]
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
                Assert.IsTrue(surface.TryGetCell(x, y, out var cell));
                bool inside = y >= 2 && y <= 5 && x >= 3 && x <= 8;
                Assert.AreEqual(inside, cell.Attributes.HasFlag(CellAttributes.Reverse));
            }
        }
    }

    [TestMethod]
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
                Assert.IsTrue(surface.TryGetCell(x, y, out var cell));
                bool inside = y >= 2 && y <= 4;
                Assert.AreEqual(inside, cell.Attributes.HasFlag(CellAttributes.Reverse));
            }
        }
    }

    [TestMethod]
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
                Assert.IsTrue(surface.TryGetCell(x, y, out var cell));
                bool inside =
                    (y == 2 && x >= 3) ||
                    (y > 2 && y < 5) ||
                    (y == 5 && x <= 8);
                Assert.AreEqual(inside, cell.Attributes.HasFlag(CellAttributes.Reverse));
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

    [TestMethod]
    public void ConfigureDefaultBindings_WithHandler_RegistersFourDragBindings()
    {
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "x" },
            CopyHandler = _ => Task.CompletedTask,
        };
        var bindings = new InputBindingsBuilder();

        node.ConfigureDefaultBindings(bindings);

        Assert.AreEqual(4, bindings.DragBindings.Count);
        TestSeq.All(bindings.DragBindings, b => Assert.AreEqual(MouseButton.Left, b.Button));

        var modifierSets = bindings.DragBindings.Select(b => b.Modifiers).ToHashSet();
        Assert.Contains(Hex1bModifiers.None, modifierSets);
        Assert.Contains(Hex1bModifiers.Control, modifierSets);
        Assert.Contains(Hex1bModifiers.Shift, modifierSets);
        Assert.Contains(Hex1bModifiers.Alt, modifierSets);
    }

    [TestMethod]
    public void ConfigureDefaultBindings_WithoutHandler_RegistersNoDragBindings()
    {
        var node = new SelectionPanelNode
        {
            Child = new TextBlockNode { Text = "x" },
        };
        var bindings = new InputBindingsBuilder();

        node.ConfigureDefaultBindings(bindings);

        Assert.IsEmpty(bindings.DragBindings);
    }

    [TestMethod]
    public void DragBinding_StartDrag_EntersCopyMode_AnchorsCursor_StartsCharacterSelection()
    {
        var node = SetupArrangedNode(width: 20, height: 5);
        var dragBinding = node.BuildBindings().DragBindings.Single(b => b.Modifiers == Hex1bModifiers.None);

        // Drag begins at local (4, 2) within the node's surface.
        var handler = dragBinding.StartDrag(4, 2);

        Assert.IsTrue(node.IsInCopyMode);
        Assert.AreEqual(2, node.CursorRow);
        Assert.AreEqual(4, node.CursorCol);
        Assert.IsTrue(node.HasSelection);
        Assert.AreEqual(2, node.AnchorRow);
        Assert.AreEqual(4, node.AnchorCol);
        Assert.AreEqual(SelectionMode.Character, node.CursorSelectionMode);
        Assert.IsFalse(handler.IsEmpty);
    }

    [TestMethod]
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

        Assert.IsTrue(node.IsInCopyMode);
        Assert.AreEqual(1, node.AnchorRow);
        Assert.AreEqual(3, node.AnchorCol);
        Assert.AreEqual(3, node.CursorRow);
        Assert.AreEqual(8, node.CursorCol);
    }

    [TestMethod]
    public void DragBinding_OnMove_ClampsToNodeBounds()
    {
        var node = SetupArrangedNode(width: 10, height: 5);
        var dragBinding = node.BuildBindings().DragBindings.Single(b => b.Modifiers == Hex1bModifiers.None);

        var handler = dragBinding.StartDrag(5, 2);
        // Mouse far outside panel bounds → cursor clamps to last valid cell.
        var ctx = new InputBindingActionContext(new FocusRing(), mouseX: 100, mouseY: 100);

        handler.OnMove?.Invoke(ctx, 100, 100);

        Assert.AreEqual(4, node.CursorRow);
        Assert.AreEqual(9, node.CursorCol);
    }

    [TestMethod]
    public void DragBinding_OnEnd_InstallsKeyboardCapture()
    {
        var node = SetupArrangedNode(width: 20, height: 5);
        var dragBinding = node.BuildBindings().DragBindings.Single(b => b.Modifiers == Hex1bModifiers.None);
        var focusRing = new FocusRing();

        var handler = dragBinding.StartDrag(2, 1);
        handler.OnEnd?.Invoke(new InputBindingActionContext(focusRing));

        Assert.IsTrue(node.IsInCopyMode);
        Assert.AreSame(node, focusRing.CapturedNode);
    }

    [TestMethod]
    public void ShiftDrag_StartsLineSelection()
    {
        var node = SetupArrangedNode(width: 20, height: 5);
        var dragBinding = node.BuildBindings().DragBindings.Single(b => b.Modifiers == Hex1bModifiers.Shift);

        dragBinding.StartDrag(4, 2);

        Assert.IsTrue(node.IsInCopyMode);
        Assert.AreEqual(SelectionMode.Line, node.CursorSelectionMode);
        Assert.IsTrue(node.HasSelection);
    }

    [TestMethod]
    public void AltDrag_StartsBlockSelection()
    {
        var node = SetupArrangedNode(width: 20, height: 5);
        var dragBinding = node.BuildBindings().DragBindings.Single(b => b.Modifiers == Hex1bModifiers.Alt);

        dragBinding.StartDrag(4, 2);

        Assert.IsTrue(node.IsInCopyMode);
        Assert.AreEqual(SelectionMode.Block, node.CursorSelectionMode);
        Assert.IsTrue(node.HasSelection);
    }

    [TestMethod]
    public void DragBinding_NegativeLocalCoordinates_ClampToZero()
    {
        // When SelectionPanel is inside a scrolled ScrollPanel, Bounds.Y can be
        // negative. The drag binding must not anchor at a negative position.
        var node = SetupArrangedNode(width: 20, height: 5);
        var dragBinding = node.BuildBindings().DragBindings.Single(b => b.Modifiers == Hex1bModifiers.None);

        dragBinding.StartDrag(-3, -2);

        Assert.IsTrue(node.IsInCopyMode);
        Assert.AreEqual(0, node.CursorRow);
        Assert.AreEqual(0, node.CursorCol);
        Assert.AreEqual(0, node.AnchorRow);
        Assert.AreEqual(0, node.AnchorCol);
    }

    [TestMethod]
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
        Assert.AreEqual(10, node.CursorRow);
        Assert.AreEqual(5, node.CursorCol);

        // Simulate scroll: panel slides up by 3 rows (Bounds.Y goes from 0 to -3).
        node.Arrange(new Rect(0, -3, 20, 50));

        // Mouse stationary at terminal (5, 10) — but with shifted bounds, the
        // cell under the mouse is now panel-local row 13 (10 - (-3)).
        var ctxAfter = new InputBindingActionContext(new FocusRing(), mouseX: 5, mouseY: 10);
        handler.OnMove?.Invoke(ctxAfter, 0, 0);
        Assert.AreEqual(13, node.CursorRow);
        Assert.AreEqual(5, node.CursorCol);
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

    [TestMethod]
    public void BuildCopyEventArgs_NoSelection_ReturnsEmptyPayload()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "Hello" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 5, 1));
        node.EnterCopyMode();

        var args = node.BuildCopyEventArgs();

        Assert.AreEqual(string.Empty, args.Text);
        Assert.AreEqual(Rect.Zero, args.PanelBounds);
        Assert.AreEqual(Rect.Zero, args.TerminalBounds);
        Assert.IsEmpty(args.Nodes);
    }

    [TestMethod]
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

        Assert.AreEqual(SelectionMode.Block, args.Mode);
        Assert.AreEqual(new Rect(2, 0, 4, 1), args.PanelBounds);
        Assert.AreEqual(new Rect(12, 5, 4, 1), args.TerminalBounds);

        // The TextBlock child fills the panel so its bounds intersect.
        var textEntry = TestSeq.Single(args.Nodes, n => n.Node is TextBlockNode);
        Assert.IsTrue(textEntry.IntersectionInTerminal == new Rect(12, 5, 4, 1));
        Assert.IsFalse(textEntry.IsFullySelected); // only 4 of 10 cols selected
    }

    [TestMethod]
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

        Assert.AreEqual(SelectionMode.Line, args.Mode);
        Assert.AreEqual(new Rect(0, 0, 3, 3), args.PanelBounds);

        // All three TextBlocks should be fully selected (each is one full row).
        var leafEntries = args.Nodes.Where(n => n.Node is TextBlockNode).ToList();
        Assert.AreEqual(3, leafEntries.Count);
        TestSeq.All(leafEntries, e => Assert.IsTrue(e.IsFullySelected));
    }

    [TestMethod]
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

        Assert.AreEqual(new Rect(0, 1, 3, 1), args.PanelBounds);

        var leafEntries = args.Nodes.Where(n => n.Node is TextBlockNode).ToList();
        var single = TestSeq.Single(leafEntries);
        Assert.AreSame(t2, single.Node);
        Assert.IsTrue(single.IsFullySelected);
        Assert.AreEqual(new Rect(0, 1, 3, 1), single.IntersectionInTerminal);
        Assert.AreEqual(new Rect(0, 0, 3, 1), single.IntersectionInNode);
    }

    [TestMethod]
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

        Assert.AreEqual(new Rect(0, 0, 5, 1), args.PanelBounds);
        Assert.AreEqual(new Rect(20, 30, 5, 1), args.TerminalBounds);
    }

    [TestMethod]
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

        var entry = TestSeq.Single(args.Nodes, n => n.Node is TextBlockNode);
        Assert.IsFalse(entry.IsFullySelected);
        Assert.AreEqual(new Rect(0, 0, 8, 1), entry.IntersectionInNode);
        Assert.AreEqual(new Rect(10, 10, 8, 1), entry.IntersectionInTerminal);
    }

    [TestMethod]
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
        Assert.AreEqual(new Rect(0, 0, 9, 3), args.PanelBounds);

        // LEFT (cols 0..2 on row 0) must be filtered out — start-row
        // selected cells start at col 6.
        Assert.IsFalse(args.Nodes.Any(n => ReferenceEquals(n.Node, left)));

        // MIDDLE (cols 3..5 on row 0) is also entirely before col 6
        // and must be filtered out.
        Assert.IsFalse(args.Nodes.Any(n => ReferenceEquals(n.Node, middle)));

        // RIGHT (cols 6..8 on row 0) is at/after col 6 — included.
        Assert.IsTrue(args.Nodes.Any(n => ReferenceEquals(n.Node, right)));
    }

    [TestMethod]
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

        var startEntry = TestSeq.Single(args.Nodes, n => ReferenceEquals(n.Node, startRowText));
        // Start-row selection runs from col 4 to end-of-row (col 7), so
        // 4 cells wide starting at col 4. In node-local coords that is
        // (4, 0, 4, 1).
        Assert.AreEqual(new Rect(4, 0, 4, 1), startEntry.IntersectionInNode);
        Assert.AreEqual(new Rect(4, 0, 4, 1), startEntry.IntersectionInTerminal);
        Assert.IsFalse(startEntry.IsFullySelected);

        var endEntry = TestSeq.Single(args.Nodes, n => ReferenceEquals(n.Node, nextRowText));
        // End-row selection runs from col 0 to col 1 inclusive → 2 wide.
        Assert.AreEqual(new Rect(0, 0, 2, 1), endEntry.IntersectionInNode);
        Assert.AreEqual(new Rect(0, 1, 2, 1), endEntry.IntersectionInTerminal);
        Assert.IsFalse(endEntry.IsFullySelected);
    }

    [TestMethod]
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

        var middleEntry = TestSeq.Single(args.Nodes, n => ReferenceEquals(n.Node, t1));
        Assert.IsTrue(middleEntry.IsFullySelected);
        Assert.AreEqual(new Rect(0, 0, 3, 1), middleEntry.IntersectionInNode);
        Assert.AreEqual(new Rect(0, 1, 3, 1), middleEntry.IntersectionInTerminal);

        // Start-row node only partially selected (cols 1..2).
        var startEntry = TestSeq.Single(args.Nodes, n => ReferenceEquals(n.Node, t0));
        Assert.IsFalse(startEntry.IsFullySelected);
        Assert.AreEqual(new Rect(1, 0, 2, 1), startEntry.IntersectionInNode);

        // End-row node only partially selected (cols 0..1).
        var endEntry = TestSeq.Single(args.Nodes, n => ReferenceEquals(n.Node, t2));
        Assert.IsFalse(endEntry.IsFullySelected);
        Assert.AreEqual(new Rect(0, 0, 2, 1), endEntry.IntersectionInNode);
    }

    // ------------------------------------------------------------------
    // Right-click commit binding.
    // ------------------------------------------------------------------

    [TestMethod]
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
        TestSeq.Single(mouseCommit);
        Assert.AreEqual(SelectionPanelWidget.CopyModeMouseCommit, mouseCommit[0].ActionId);
    }

    [TestMethod]
    public void ConfigureDefaultBindings_WithoutHandler_DoesNotRegisterRightClickMouseBinding()
    {
        var node = new SelectionPanelNode { Child = new TextBlockNode { Text = "Hello" } };

        var bindings = new InputBindingsBuilder();
        node.ConfigureDefaultBindings(bindings);

        Assert.IsEmpty(bindings.MouseBindings);
    }

    [TestMethod]
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

        Assert.IsNotNull(captured);
        Assert.AreEqual("Hello", captured!.Text);
        Assert.AreEqual(SelectionMode.Block, captured.Mode);
        Assert.IsFalse(node.IsInCopyMode);
        Assert.IsNull(focusRing.CapturedNode);
    }

    [TestMethod]
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

        Assert.IsFalse(handlerCalled);
        // Stays in copy mode and keeps capture (matches Y/Enter no-op semantics).
        Assert.IsTrue(node.IsInCopyMode);
        Assert.AreSame(node, focusRing.CapturedNode);
    }

    [TestMethod]
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

        Assert.IsFalse(handlerCalled);
        Assert.IsFalse(node.IsInCopyMode);
    }

    // ------------------------------------------------------------------
    // OnCopy overloads on the widget pass through the args correctly.
    // ------------------------------------------------------------------

    [TestMethod]
    public async Task OnCopy_StringOverload_ReceivesText()
    {
        string? received = null;
        var widget = new SelectionPanelWidget(new TextBlockWidget("Hello"))
            .OnCopy((string s) => { received = s; });

        var args = new SelectionPanelCopyEventArgs(
            "TheText", SelectionMode.Character, Rect.Zero, Rect.Zero, []);
        await widget.CopyHandler!(args);

        Assert.AreEqual("TheText", received);
    }

    [TestMethod]
    public async Task OnCopy_ArgsOverload_ReceivesFullPayload()
    {
        SelectionPanelCopyEventArgs? received = null;
        var widget = new SelectionPanelWidget(new TextBlockWidget("Hello"))
            .OnCopy((SelectionPanelCopyEventArgs a) => { received = a; });

        var args = new SelectionPanelCopyEventArgs(
            "X", SelectionMode.Line, new Rect(1, 2, 3, 4), new Rect(5, 6, 7, 8), []);
        await widget.CopyHandler!(args);

        Assert.AreSame(args, received);
    }

    // ------------------------------------------------------------------
    // Render with a parent clip narrower than Bounds (the ScrollPanel
    // perf path). Verifies that:
    //   1. Cells outside visibleTerm are NEITHER painted NOR inverted.
    //   2. Selection cells inside visibleTerm ARE inverted.
    //   3. Negative Bounds.Y / partially-visible panels work.
    //   4. Block / Character selections clip correctly at viewport edges.
    // ------------------------------------------------------------------

    [TestMethod]
    public void Render_WithParentClipNarrowerThanBounds_OnlyClipRegionPainted()
    {
        // Panel is 12 wide × 20 rows tall (PaintingTestNode paints rows
        // 'A'..'T'). Parent clip: the middle 6 rows starting at row 5
        // (terminal rows 5..10 inclusive). Anything outside MUST be
        // untouched in the parent surface.
        var painter = new PaintingTestNode();
        var node = new SelectionPanelNode { Child = painter };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 12, 20));

        // Enter copy mode so the new viewport-bounded path runs (the
        // pass-through path doesn't allocate a temp surface at all).
        node.EnterCopyMode();

        var surface = new Surface(12, 20);
        var ctx = new SurfaceRenderContext(surface)
        {
            CurrentLayoutProvider = new RectLayoutProvider(new Rect(0, 5, 12, 6))
        };
        node.Render(ctx);

        // Rows inside parent clip [5..10] should have the painted
        // characters from the painter (rows 'F'..'K').
        for (int y = 5; y <= 10; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                Assert.IsTrue(surface.TryGetCell(x, y, out var cell));
                Assert.AreEqual(((char)('A' + y)).ToString(), cell.Character);
            }
        }

        // Rows outside parent clip should be untouched (still in their
        // initial SurfaceCells.Empty state).
        for (int y = 0; y < 5; y++)
            AssertRowUntouched(surface, y, "above parent clip");
        for (int y = 11; y < 20; y++)
            AssertRowUntouched(surface, y, "below parent clip");
    }

    [TestMethod]
    public void Render_LineSelection_SpanningOffscreenAndOnscreen_OnlyVisibleRowsInverted()
    {
        // 20-row panel; parent clip exposes terminal rows 5..10. Line
        // selection covers panel-local rows 2..15 — the visible
        // intersection is rows 5..10 (all inverted), while panel-local
        // rows 2..4 and 11..15 are off-clip.
        var painter = new PaintingTestNode();
        var node = new SelectionPanelNode { Child = painter };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 12, 20));

        node.EnterCopyMode();
        node.SetCursor(2, 0);
        node.StartOrToggleSelection(SelectionMode.Line);
        node.SetCursor(15, 11);

        var surface = new Surface(12, 20);
        var ctx = new SurfaceRenderContext(surface)
        {
            CurrentLayoutProvider = new RectLayoutProvider(new Rect(0, 5, 12, 6))
        };
        node.Render(ctx);

        // All cells in the visible band (rows 5..10) must be inverted —
        // Line selection covers the whole row width.
        for (int y = 5; y <= 10; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                Assert.IsTrue(surface.TryGetCell(x, y, out var cell));
                Assert.IsTrue(cell.Attributes.HasFlag(CellAttributes.Reverse), $"Cell ({x},{y}) inside visible selection should be inverted.");
            }
        }

        // Cells outside the parent clip must be untouched (no paint,
        // no inversion) — even though they're inside the selection
        // geometry, they're not visible.
        for (int y = 0; y < 5; y++)
            AssertRowUntouched(surface, y, "above parent clip");
        for (int y = 11; y < 20; y++)
            AssertRowUntouched(surface, y, "below parent clip");
    }

    [TestMethod]
    public void Render_NoSelection_CursorOffscreen_NoInversion()
    {
        // Cursor at panel-local (0, 0), but parent clip exposes only
        // terminal rows 5..10 — so the cursor cell is off-clip and
        // must NOT be inverted.
        var painter = new PaintingTestNode();
        var node = new SelectionPanelNode { Child = painter };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 12, 20));

        node.EnterCopyMode();
        node.SetCursor(0, 0);

        var surface = new Surface(12, 20);
        var ctx = new SurfaceRenderContext(surface)
        {
            CurrentLayoutProvider = new RectLayoutProvider(new Rect(0, 5, 12, 6))
        };
        node.Render(ctx);

        // No inverted cells anywhere on the parent surface.
        for (int y = 0; y < 20; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                Assert.IsTrue(surface.TryGetCell(x, y, out var cell));
                Assert.IsFalse(cell.Attributes.HasFlag(CellAttributes.Reverse), $"Cell ({x},{y}) should not be inverted (cursor is off-clip).");
            }
        }
    }

    [TestMethod]
    public void Render_BlockSelection_ClippedAtRightViewportEdge()
    {
        // Panel 20 cols × 8 rows. Parent clip exposes only the LEFT
        // 12 columns. Block selection from (col 3, row 1) to (col 17,
        // row 4) — the right edge (cols 12..17) should be off-clip
        // and not inverted; the visible portion (cols 3..11) IS
        // inverted.
        var painter = new PaintingTestNode();
        var node = new SelectionPanelNode { Child = painter };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 20, 8));

        node.EnterCopyMode();
        node.SetCursor(1, 3);
        node.StartOrToggleSelection(SelectionMode.Block);
        node.SetCursor(4, 17);

        var surface = new Surface(20, 8);
        var ctx = new SurfaceRenderContext(surface)
        {
            CurrentLayoutProvider = new RectLayoutProvider(new Rect(0, 0, 12, 8))
        };
        node.Render(ctx);

        // Inside the visible portion of the block: cols 3..11, rows 1..4 → inverted.
        for (int y = 1; y <= 4; y++)
        {
            for (int x = 3; x <= 11; x++)
            {
                Assert.IsTrue(surface.TryGetCell(x, y, out var cell));
                Assert.IsTrue(cell.Attributes.HasFlag(CellAttributes.Reverse), $"Cell ({x},{y}) inside visible block selection should be inverted.");
            }
        }

        // Cols 12..19 are outside parent clip — untouched (still Empty).
        for (int y = 0; y < 8; y++)
        {
            for (int x = 12; x < 20; x++)
            {
                Assert.IsTrue(surface.TryGetCell(x, y, out var cell));
                Assert.AreEqual(SurfaceCells.Empty, cell);
            }
        }

        // Cells inside parent clip but outside the block selection must
        // be painted (visible) yet NOT inverted.
        for (int x = 0; x <= 2; x++)
        {
            Assert.IsTrue(surface.TryGetCell(x, 1, out var cell));
            Assert.IsFalse(cell.Attributes.HasFlag(CellAttributes.Reverse), $"Cell ({x},1) left of block selection should not be inverted.");
        }
    }

    [TestMethod]
    public void Render_NegativeBoundsY_RendersAndInvertsOnlyBottomSlice()
    {
        // Panel scrolled so that Bounds.Y = -5: panel-local row 0 sits
        // at terminal row -5; the visible portion is panel-local rows
        // 5..12 (terminal rows 0..7).
        // PaintingTestNode paints panel-local row y with char ('A'+y),
        // so terminal row 0 should show 'F', row 7 should show 'M'.
        var painter = new PaintingTestNode();
        var node = new SelectionPanelNode { Child = painter };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, -5, 12, 15));

        // Line selection spanning panel-local rows 6..11 (visible
        // portion) — should show inverted in terminal rows 1..6.
        node.EnterCopyMode();
        node.SetCursor(6, 0);
        node.StartOrToggleSelection(SelectionMode.Line);
        node.SetCursor(11, 0);

        var surface = new Surface(12, 8);
        var ctx = new SurfaceRenderContext(surface)
        {
            CurrentLayoutProvider = new RectLayoutProvider(new Rect(0, 0, 12, 8))
        };
        node.Render(ctx);

        // Painted characters must be 'F'..'M' (panel-local rows 5..12)
        // mapped to terminal rows 0..7.
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                Assert.IsTrue(surface.TryGetCell(x, y, out var cell));
                Assert.AreEqual(((char)('A' + 5 + y)).ToString(), cell.Character);
            }
        }

        // Inverted cells: panel-local rows 6..11 → terminal rows 1..6.
        for (int y = 0; y < 8; y++)
        {
            bool inSelection = y >= 1 && y <= 6;
            for (int x = 0; x < 12; x++)
            {
                Assert.IsTrue(surface.TryGetCell(x, y, out var cell));
                Assert.AreEqual(inSelection, cell.Attributes.HasFlag(CellAttributes.Reverse));
            }
        }
    }

    [TestMethod]
    public void Render_PanelEntirelyOffscreen_LeavesParentSurfaceUntouched()
    {
        // Panel sits entirely above the parent clip — TryIntersectRect
        // returns false and Render bails before allocating a temp.
        var painter = new PaintingTestNode();
        var node = new SelectionPanelNode { Child = painter };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, -20, 12, 8));

        node.EnterCopyMode();
        node.SetCursor(2, 3);
        node.StartOrToggleSelection(SelectionMode.Line);
        node.SetCursor(5, 8);

        var surface = new Surface(12, 8);
        var ctx = new SurfaceRenderContext(surface)
        {
            CurrentLayoutProvider = new RectLayoutProvider(new Rect(0, 0, 12, 8))
        };
        node.Render(ctx);

        for (int y = 0; y < 8; y++)
            AssertRowUntouched(surface, y, "panel entirely offscreen");
    }

    private static void AssertRowUntouched(Surface surface, int y, string reason)
    {
        for (int x = 0; x < surface.Width; x++)
        {
            Assert.IsTrue(surface.TryGetCell(x, y, out var cell));
            Assert.AreEqual(SurfaceCells.Empty, cell);
            Assert.IsFalse(cell.Attributes.HasFlag(CellAttributes.Reverse), $"Cell ({x},{y}) should not be inverted ({reason}).");
        }
    }
}
