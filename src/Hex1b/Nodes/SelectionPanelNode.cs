using System.Text;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="SelectionPanelWidget"/>. Implements a
/// terminal-style copy mode over the wrapped child.
/// </summary>
/// <remarks>
/// <para>
/// Outside copy mode the node is a pure pass-through: layout, focus, and
/// input flow to the child unchanged. While
/// <see cref="IsInCopyMode"/> is <c>true</c>, the node:
/// </para>
/// <list type="bullet">
/// <item>Holds focus capture (set up by the
///       <see cref="SelectionPanelWidget.EnterCopyMode"/> binding handler
///       via <see cref="InputBindingActionContext.CaptureInput"/>) so that
///       all subsequent input goes to its capture-override bindings
///       rather than to whatever widget had keyboard focus.</item>
/// <item>Renders an inverted-cell cursor (and, if a selection is active,
///       an inverted selection region) over the child output by
///       rendering the child to a viewport-sized temporary surface (the
///       intersection of the panel <c>Bounds</c> with the parent's
///       effective clip rect — so cost stays bounded by the visible
///       region even when the panel sits inside a long-scrolling
///       container), applying <see cref="CellAttributes.Reverse"/> to
///       the relevant cells, and compositing the modified surface
///       back.</item>
/// </list>
/// <para>
/// Cursor coordinates are surface-local: <see cref="CursorRow"/> is in
/// <c>0..Bounds.Height-1</c> and <see cref="CursorCol"/> is in
/// <c>0..Bounds.Width-1</c>. <see cref="EnterCopyMode"/> initialises the
/// cursor at the bottom-left of the surface to feel natural in
/// chat-style auto-scrolling viewports (where the most recent content is
/// at the bottom).
/// </para>
/// </remarks>
public sealed class SelectionPanelNode : Hex1bNode
{
    /// <summary>The child node wrapped by this panel.</summary>
    public Hex1bNode? Child { get; set; }

    /// <summary>
    /// Optional handler invoked with a <see cref="SelectionPanelCopyEventArgs"/>
    /// describing the current selection when the user commits via
    /// <see cref="SelectionPanelWidget.CopyModeCopy"/> (Y/Enter) or
    /// <see cref="SelectionPanelWidget.CopyModeMouseCommit"/> (right-click
    /// by default). When this is <c>null</c>, copy mode bindings are not
    /// registered (the panel behaves as a pure pass-through).
    /// </summary>
    public Func<SelectionPanelCopyEventArgs, Task>? CopyHandler { get; set; }

    /// <summary><c>true</c> while the user is in interactive copy mode.</summary>
    public bool IsInCopyMode { get; private set; }

    /// <summary>Cursor row in surface-local coordinates.</summary>
    public int CursorRow { get; private set; }

    /// <summary>Cursor column in surface-local coordinates.</summary>
    public int CursorCol { get; private set; }

    /// <summary>
    /// Anchor row of the active selection, or <c>null</c> when no
    /// selection is active (only the cursor is shown).
    /// </summary>
    public int? AnchorRow { get; private set; }

    /// <summary>
    /// Anchor column of the active selection, or <c>null</c> when no
    /// selection is active (only the cursor is shown).
    /// </summary>
    public int? AnchorCol { get; private set; }

    /// <summary>
    /// Geometry of the active selection. Has no effect when
    /// <see cref="AnchorRow"/>/<see cref="AnchorCol"/> are <c>null</c>.
    /// </summary>
    public SelectionMode CursorSelectionMode { get; private set; } = SelectionMode.Character;

    /// <summary><c>true</c> when both an anchor and cursor are set.</summary>
    public bool HasSelection => AnchorRow.HasValue && AnchorCol.HasValue;

    public override bool IsFocusable => false;

    public override bool IsFocused
    {
        get => false;
        set
        {
            if (Child != null)
                Child.IsFocused = value;
        }
    }

    protected override Size MeasureCore(Constraints constraints)
        => Child?.Measure(constraints) ?? constraints.Constrain(Size.Zero);

    protected override void ArrangeCore(Rect rect)
    {
        base.ArrangeCore(rect);
        Child?.Arrange(rect);

        // Bounds may shrink between frames (window resize, transcript
        // pruning); keep cursor + anchor inside the new surface.
        ClampCursorAndAnchor();
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Child is null) return;

        if (!IsInCopyMode || context is not SurfaceRenderContext surfaceCtx
            || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            context.RenderChild(Child);
            return;
        }

        // Snapshot parent state BEFORE descending. Many descendant nodes
        // temporarily mutate CurrentLayoutProvider on the shared context
        // and restore it on exit (ZStack, ScrollPanel, DragBarPanel,
        // etc.). Reading these props after Child.Render has run is
        // unsafe — capture them up front.
        int parentOffsetX = surfaceCtx.OffsetX;
        int parentOffsetY = surfaceCtx.OffsetY;
        var parentProvider = surfaceCtx.CurrentLayoutProvider;
        Rect parentClip = parentProvider != null
            ? LayoutProviderHelper.GetEffectiveClipRect(parentProvider)
            : new Rect(parentOffsetX, parentOffsetY,
                       surfaceCtx.Surface.Width, surfaceCtx.Surface.Height);

        // visibleTerm is the portion of this panel that the parent's
        // clip will actually let through to the screen. Bounds may
        // extend far above/below the viewport when the panel sits
        // inside a scrolled ScrollPanel; the intersection is what
        // matters for both rendering and selection inversion.
        if (!TryIntersectRect(Bounds, parentClip, out var visibleTerm))
        {
            // Panel entirely off-screen — nothing to draw.
            return;
        }

        // Render to a temp surface SIZED TO THE VISIBLE PORTION ONLY,
        // not the full panel bounds. This is the key perf win versus
        // the previous capture-modify-composite (which sized the temp
        // to the full transcript height) and versus a naive
        // context.RenderChild (which would cause SurfaceRenderContext
        // to rent a child.Bounds-sized surface internally because a
        // layout provider is active).
        var pool = surfaceCtx.SurfacePool;
        var tempSurface = pool != null
            ? pool.Rent(visibleTerm.Width, visibleTerm.Height, surfaceCtx.CellMetrics)
            : new Surface(visibleTerm.Width, visibleTerm.Height, surfaceCtx.CellMetrics);

        try
        {
            // Buffer (0,0) of tempSurface corresponds to terminal
            // (visibleTerm.X, visibleTerm.Y).
            var tempContext = new SurfaceRenderContext(
                tempSurface, visibleTerm.X, visibleTerm.Y,
                context.Theme, surfaceCtx.TrackedObjectStore)
            {
                CachingEnabled = surfaceCtx.CachingEnabled,
                MouseX = surfaceCtx.MouseX,
                MouseY = surfaceCtx.MouseY,
                CellMetrics = surfaceCtx.CellMetrics,
                SurfacePool = pool,
            };

            // Push a layout provider clipping to visibleTerm so any
            // child writes outside that rect are dropped at the cell
            // level. Chain to the parent provider so its clip still
            // applies further (e.g., grandparent panels).
            tempContext.CurrentLayoutProvider = new RectLayoutProvider(visibleTerm)
            {
                ParentLayoutProvider = parentProvider
            };
            tempContext.SetCursorPosition(Child.Bounds.X, Child.Bounds.Y);

            // Render directly — DO NOT go through tempContext.RenderChild,
            // which would re-allocate a Child.Bounds-sized surface and
            // defeat the point of this whole refactor.
            Child.Render(tempContext);

            ApplyCursorOverlayToViewport(tempSurface, visibleTerm);

            // Composite back into the parent surface. Honour the
            // parent's clip on the composite step too — RectLayout
            // already gates writes during render, but the parent may
            // have additional clipping (ancestor scrolls, etc.) that
            // we should respect on the final blit.
            Rect? clipRect = null;
            if (parentProvider != null)
            {
                var providerClip = parentProvider.ClipRect;
                clipRect = new Rect(
                    providerClip.X - parentOffsetX,
                    providerClip.Y - parentOffsetY,
                    providerClip.Width, providerClip.Height);
            }

            surfaceCtx.Surface.Composite(
                tempSurface,
                visibleTerm.X - parentOffsetX,
                visibleTerm.Y - parentOffsetY,
                clipRect);
        }
        finally
        {
            if (pool != null)
                pool.Return(tempSurface);
        }
    }

    // Rect intersection. Hex1b.Layout.Rect doesn't expose Intersect,
    // so inline the math here. Returns false (and out param is
    // Rect.Zero) when the rects don't overlap.
    private static bool TryIntersectRect(Rect a, Rect b, out Rect result)
    {
        int left = Math.Max(a.X, b.X);
        int top = Math.Max(a.Y, b.Y);
        int right = Math.Min(a.X + a.Width, b.X + b.Width);
        int bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);

        if (right <= left || bottom <= top)
        {
            result = new Rect(left, top, 0, 0);
            return false;
        }

        result = new Rect(left, top, right - left, bottom - top);
        return true;
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null) yield return Child;
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
                yield return focusable;
        }
    }

    // ------------------------------------------------------------------
    // Programmatic copy-mode controls (also exercised by tests).
    // ------------------------------------------------------------------

    /// <summary>
    /// Enters copy mode. The cursor is initialised at the bottom-left of
    /// the rendered surface (the natural starting point for
    /// chat-style content where the latest output sits at the bottom of
    /// an auto-scrolled viewport). Any existing selection is cleared.
    /// Note: this does <em>not</em> install input capture; the binding
    /// handler that calls <c>EnterCopyMode</c> also calls
    /// <see cref="InputBindingActionContext.CaptureInput"/>.
    /// </summary>
    public void EnterCopyMode()
    {
        IsInCopyMode = true;
        AnchorRow = null;
        AnchorCol = null;
        CursorSelectionMode = SelectionMode.Character;
        CursorRow = Math.Max(0, Bounds.Height - 1);
        CursorCol = 0;
        ClampCursorAndAnchor();
        MarkDirty();
    }

    /// <summary>
    /// Exits copy mode and clears the cursor / selection state. Pair with
    /// <see cref="InputBindingActionContext.ReleaseCapture"/> at the
    /// caller.
    /// </summary>
    public void ExitCopyMode()
    {
        IsInCopyMode = false;
        AnchorRow = null;
        AnchorCol = null;
        CursorSelectionMode = SelectionMode.Character;
        MarkDirty();
    }

    /// <summary>
    /// Moves the cursor by <paramref name="rowDelta"/> rows and
    /// <paramref name="colDelta"/> columns. Clamps to surface bounds.
    /// </summary>
    public void MoveCursor(int rowDelta, int colDelta)
    {
        SetCursor(CursorRow + rowDelta, CursorCol + colDelta);
    }

    /// <summary>
    /// Sets the cursor to the specified surface-local coordinates,
    /// clamped to <c>0..Width-1</c> and <c>0..Height-1</c>.
    /// </summary>
    public void SetCursor(int row, int col)
    {
        int w = Math.Max(1, Bounds.Width);
        int h = Math.Max(1, Bounds.Height);
        CursorRow = Math.Clamp(row, 0, h - 1);
        CursorCol = Math.Clamp(col, 0, w - 1);
        MarkDirty();
    }

    /// <summary>
    /// Starts a selection in <paramref name="mode"/> if no selection is
    /// active, switches the mode (preserving the anchor) if a selection
    /// is active in a different mode, or clears the selection if the
    /// same mode is requested again. Mirrors <see cref="TerminalWidget"/>'s
    /// vi-style toggle semantics.
    /// </summary>
    public void StartOrToggleSelection(SelectionMode mode)
    {
        if (!HasSelection)
        {
            AnchorRow = CursorRow;
            AnchorCol = CursorCol;
            CursorSelectionMode = mode;
        }
        else if (CursorSelectionMode == mode)
        {
            // Same mode → clear selection (anchor stays unset until next start).
            AnchorRow = null;
            AnchorCol = null;
            CursorSelectionMode = SelectionMode.Character;
        }
        else
        {
            // Different mode → keep anchor, change geometry.
            CursorSelectionMode = mode;
        }
        MarkDirty();
    }

    /// <summary>
    /// Reads back the plain text of the current selection. Returns an
    /// empty string when no selection is active or when bounds are zero.
    /// </summary>
    public string SnapshotText()
    {
        if (Child is null || !HasSelection)
        {
            return string.Empty;
        }

        var bounds = Child.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return string.Empty;
        }

        var surface = RenderChildToSurface(bounds);
        return ReadSelectionText(surface);
    }

    // ------------------------------------------------------------------
    // Input binding wiring.
    // ------------------------------------------------------------------

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        if (CopyHandler is null)
        {
            return;
        }

        // Entry — single global binding so the user can enter copy mode
        // regardless of which widget currently has keyboard focus.
        bindings.Key(Hex1bKey.F12).Global().Triggers(
            SelectionPanelWidget.EnterCopyMode,
            ctx =>
            {
                if (IsInCopyMode) return Task.CompletedTask; // re-entry guard
                EnterCopyMode();
                ctx.CaptureInput(this);
                ctx.Invalidate();
                return Task.CompletedTask;
            },
            "Enter SelectionPanel copy mode");

        // In-copy-mode actions are registered as capture-override bindings.
        // The router only checks these when SOMETHING has captured input,
        // and our IsInCopyMode guard ensures they no-op if capture belongs
        // to another widget. This keeps the bindings rebindable via
        // ActionId without polluting the global namespace.

        // Movement
        RegisterMove(bindings, Hex1bKey.UpArrow,    SelectionPanelWidget.CopyModeUp,    -1,  0, "Move cursor up");
        RegisterMove(bindings, Hex1bKey.K,          SelectionPanelWidget.CopyModeUp,    -1,  0, "Move cursor up (vi)");
        RegisterMove(bindings, Hex1bKey.DownArrow,  SelectionPanelWidget.CopyModeDown,   1,  0, "Move cursor down");
        RegisterMove(bindings, Hex1bKey.J,          SelectionPanelWidget.CopyModeDown,   1,  0, "Move cursor down (vi)");
        RegisterMove(bindings, Hex1bKey.LeftArrow,  SelectionPanelWidget.CopyModeLeft,   0, -1, "Move cursor left");
        RegisterMove(bindings, Hex1bKey.H,          SelectionPanelWidget.CopyModeLeft,   0, -1, "Move cursor left (vi)");
        RegisterMove(bindings, Hex1bKey.RightArrow, SelectionPanelWidget.CopyModeRight,  0,  1, "Move cursor right");
        RegisterMove(bindings, Hex1bKey.L,          SelectionPanelWidget.CopyModeRight,  0,  1, "Move cursor right (vi)");
        RegisterMove(bindings, Hex1bKey.PageUp,     SelectionPanelWidget.CopyModePageUp, -PageRows, 0, "Move cursor one page up");
        RegisterMove(bindings, Hex1bKey.PageDown,   SelectionPanelWidget.CopyModePageDown, PageRows, 0, "Move cursor one page down");

        // Word movement (whole-row scan against the rendered surface).
        RegisterCaptureOverride(bindings, Hex1bKey.W, SelectionPanelWidget.CopyModeWordForward,
            ctx => { if (IsInCopyMode) MoveWordForward(); }, "Move forward one word");
        RegisterCaptureOverride(bindings, Hex1bKey.B, SelectionPanelWidget.CopyModeWordBackward,
            ctx => { if (IsInCopyMode) MoveWordBackward(); }, "Move backward one word");

        // Line / buffer extremes
        RegisterCaptureOverride(bindings, Hex1bKey.Home, SelectionPanelWidget.CopyModeLineStart,
            ctx => { if (IsInCopyMode) SetCursor(CursorRow, 0); }, "Move to line start");
        RegisterCaptureOverride(bindings, Hex1bKey.D0, SelectionPanelWidget.CopyModeLineStart,
            ctx => { if (IsInCopyMode) SetCursor(CursorRow, 0); }, "Move to line start (vi)");
        RegisterCaptureOverride(bindings, Hex1bKey.End, SelectionPanelWidget.CopyModeLineEnd,
            ctx => { if (IsInCopyMode) SetCursor(CursorRow, int.MaxValue); }, "Move to line end");
        RegisterCaptureOverride(bindings, Hex1bKey.G, SelectionPanelWidget.CopyModeBufferTop,
            ctx => { if (IsInCopyMode) SetCursor(0, CursorCol); }, "Move to buffer top");
        bindings.Key(Hex1bKey.G).Shift().OverridesCapture().Triggers(
            SelectionPanelWidget.CopyModeBufferBottom,
            ctx => { if (IsInCopyMode) SetCursor(int.MaxValue, CursorCol); return Task.CompletedTask; },
            "Move to buffer bottom");

        // Selection mode
        RegisterCaptureOverride(bindings, Hex1bKey.V, SelectionPanelWidget.CopyModeStartSelection,
            ctx => { if (IsInCopyMode) StartOrToggleSelection(SelectionMode.Character); }, "Toggle character selection");
        RegisterCaptureOverride(bindings, Hex1bKey.Spacebar, SelectionPanelWidget.CopyModeStartSelection,
            ctx => { if (IsInCopyMode) StartOrToggleSelection(SelectionMode.Character); }, "Toggle character selection");
        bindings.Key(Hex1bKey.V).Shift().OverridesCapture().Triggers(
            SelectionPanelWidget.CopyModeToggleLineMode,
            ctx => { if (IsInCopyMode) StartOrToggleSelection(SelectionMode.Line); return Task.CompletedTask; },
            "Toggle line selection");
        bindings.Key(Hex1bKey.V).Alt().OverridesCapture().Triggers(
            SelectionPanelWidget.CopyModeToggleBlockMode,
            ctx => { if (IsInCopyMode) StartOrToggleSelection(SelectionMode.Block); return Task.CompletedTask; },
            "Toggle block selection");

        // Commit / cancel
        RegisterCopy(bindings, Hex1bKey.Y);
        RegisterCopy(bindings, Hex1bKey.Enter);
        RegisterCancel(bindings, Hex1bKey.Escape);
        RegisterCancel(bindings, Hex1bKey.Q);

        // Right-click commit. OverridesCapture means the binding fires
        // even though SelectionPanel is non-focusable: when the panel has
        // captured input (i.e., is in copy mode) and the click falls
        // within the panel's bounds, Hex1bApp routes the click to this
        // binding before normal hit-test routing. Outside copy mode the
        // panel doesn't hold capture so this binding never fires and
        // right-click reaches whatever focusable is underneath.
        bindings.Mouse(MouseButton.Right).OverridesCapture().Triggers(
            SelectionPanelWidget.CopyModeMouseCommit,
            CommitCopyAsync,
            "Copy selection (right-click) and exit copy mode");

        // Mouse drag-to-select. The framework defers activation until the
        // user actually moves the mouse (Hex1bApp's pending-bubble-drag
        // mechanism) so plain clicks on inner widgets aren't intercepted.
        // Modifier-aware: Ctrl drag = Line (matches TerminalWidget's
        // CopyModeBindingsOptions.MouseLineModifier default), Alt drag =
        // Block, Shift drag = Line as a fallback for terminals that pass
        // Shift through (Windows Terminal, GNOME Terminal, etc. consume
        // Shift+drag for OS-level native selection so Ctrl is the reliable
        // primary modifier).
        RegisterDrag(bindings, Hex1bModifiers.None, SelectionMode.Character, "Drag-select characters");
        RegisterDrag(bindings, Hex1bModifiers.Control, SelectionMode.Line, "Drag-select lines");
        RegisterDrag(bindings, Hex1bModifiers.Shift, SelectionMode.Line, "Drag-select lines (alt)");
        RegisterDrag(bindings, Hex1bModifiers.Alt, SelectionMode.Block, "Drag-select block");
    }

    private void RegisterDrag(InputBindingsBuilder bindings, Hex1bModifiers modifiers, SelectionMode mode, string description)
    {
        var step = bindings.Drag(MouseButton.Left);
        if (modifiers.HasFlag(Hex1bModifiers.Shift)) step = step.Shift();
        if (modifiers.HasFlag(Hex1bModifiers.Alt)) step = step.Alt();
        if (modifiers.HasFlag(Hex1bModifiers.Control)) step = step.Ctrl();

        step.Action((startX, startY) =>
        {
            // The drag binding fires on the first move event (deferred
            // activation in Hex1bApp). Coordinates are local to this node's
            // Bounds; for SelectionPanel that equals surface-local since the
            // panel is pass-through and its surface spans Bounds exactly.
            // Inside a ScrollPanel, Bounds.Y can be negative when scrolled,
            // so localY is still a valid surface row.
            int anchorRow = Math.Max(0, startY);
            int anchorCol = Math.Max(0, startX);

            EnterCopyMode();
            SetCursor(anchorRow, anchorCol);
            StartOrToggleSelection(mode);

            return new DragHandler(
                onMove: (ctx, dx, dy) =>
                {
                    if (!IsInCopyMode) return;
                    // Compute the cursor from the absolute mouse coordinates
                    // relative to the panel's CURRENT bounds rather than from
                    // anchor + delta. The delta-based model breaks if Bounds
                    // shifts between drag-start and drag-move (which happens
                    // when an enclosing ScrollPanel scrolls mid-drag in
                    // response to a wheel event). Using ctx.MouseX/Y - Bounds
                    // makes the cursor track whatever cell is under the mouse
                    // regardless of intervening scroll.
                    int row = ctx.MouseY - Bounds.Y;
                    int col = ctx.MouseX - Bounds.X;
                    SetCursor(row, col);
                    ctx.Invalidate();
                },
                onEnd: ctx =>
                {
                    if (!IsInCopyMode) return;
                    // Stay in copy mode — install keyboard capture so the
                    // user can refine the selection with arrows or commit
                    // with Y / Enter / cancel with Esc.
                    ctx.CaptureInput(this);
                    ctx.Invalidate();
                });
        }, description);
    }

    private const int PageRows = 20;

    private void RegisterMove(InputBindingsBuilder bindings, Hex1bKey key, ActionId actionId,
        int rowDelta, int colDelta, string description)
    {
        bindings.Key(key).OverridesCapture().Triggers(
            actionId,
            ctx =>
            {
                if (IsInCopyMode)
                {
                    MoveCursor(rowDelta, colDelta);
                    ctx.Invalidate();
                }
                return Task.CompletedTask;
            },
            description);
    }

    private void RegisterCaptureOverride(InputBindingsBuilder bindings, Hex1bKey key, ActionId actionId,
        Action<InputBindingActionContext> handler, string description)
    {
        bindings.Key(key).OverridesCapture().Triggers(
            actionId,
            ctx =>
            {
                if (IsInCopyMode)
                {
                    handler(ctx);
                    ctx.Invalidate();
                }
                return Task.CompletedTask;
            },
            description);
    }

    private void RegisterCopy(InputBindingsBuilder bindings, Hex1bKey key)
    {
        bindings.Key(key).OverridesCapture().Triggers(
            SelectionPanelWidget.CopyModeCopy,
            CommitCopyAsync,
            "Copy selection and exit copy mode");
    }

    /// <summary>
    /// Shared commit path used by the keyboard <see cref="SelectionPanelWidget.CopyModeCopy"/>
    /// (Y/Enter) bindings and the mouse <see cref="SelectionPanelWidget.CopyModeMouseCommit"/>
    /// (right-click) binding. Snapshots the selection, exits copy mode,
    /// releases capture, and dispatches the handler.
    /// </summary>
    private async Task CommitCopyAsync(InputBindingActionContext ctx)
    {
        if (!IsInCopyMode) return;

        // Commit-without-selection is a no-op — matches
        // TerminalWidgetHandle.CopySelection() which returns empty when
        // no selection is set. Stay in copy mode so a stray Y / Enter /
        // right-click doesn't kick the user out of a navigation session.
        if (!HasSelection)
        {
            return;
        }

        var handler = CopyHandler;
        var args = BuildCopyEventArgs();
        ExitCopyMode();
        ctx.ReleaseCapture();
        ctx.Invalidate();
        if (handler is not null)
        {
            await handler(args);
        }
    }

    private void RegisterCancel(InputBindingsBuilder bindings, Hex1bKey key)
    {
        bindings.Key(key).OverridesCapture().Triggers(
            SelectionPanelWidget.CopyModeCancel,
            ctx =>
            {
                if (!IsInCopyMode) return Task.CompletedTask;
                ExitCopyMode();
                ctx.ReleaseCapture();
                ctx.Invalidate();
                return Task.CompletedTask;
            },
            "Cancel copy mode without copying");
    }

    // ------------------------------------------------------------------
    // Surface readback + selection geometry.
    // ------------------------------------------------------------------

    private void ClampCursorAndAnchor()
    {
        int w = Math.Max(1, Bounds.Width);
        int h = Math.Max(1, Bounds.Height);
        if (CursorRow < 0) CursorRow = 0;
        if (CursorCol < 0) CursorCol = 0;
        if (CursorRow > h - 1) CursorRow = h - 1;
        if (CursorCol > w - 1) CursorCol = w - 1;

        if (AnchorRow.HasValue && AnchorCol.HasValue)
        {
            int ar = AnchorRow.Value;
            int ac = AnchorCol.Value;
            if (ar < 0) ar = 0;
            if (ac < 0) ac = 0;
            if (ar > h - 1) ar = h - 1;
            if (ac > w - 1) ac = w - 1;
            AnchorRow = ar;
            AnchorCol = ac;
        }
    }

    private Surface RenderChildToSurface(Rect bounds)
    {
        var surface = new Surface(bounds.Width, bounds.Height);
        var context = new SurfaceRenderContext(surface, bounds.X, bounds.Y);
        // Match the framework's RenderChild contract: position the
        // virtual cursor at the child's bounds origin before rendering
        // so that nodes which write their first line at "current cursor"
        // (TextBlockNode in the no-layout-provider branch) land at the
        // expected absolute coordinates instead of (0, 0).
        context.SetCursorPosition(bounds.X, bounds.Y);
        Child!.Render(context);
        return surface;
    }

    // Apply the cursor / selection inversion overlay to a temp surface
    // sized to the visible viewport. Geometry stays in panel-local
    // coords (cursor + anchor never need to be translated for any
    // logic outside Render); only the per-cell write maps panel-local
    // (col, row) → terminal absolute (Bounds.X+col, Bounds.Y+row) →
    // temp-buffer (-visibleTerm.X, -visibleTerm.Y) with a bounds
    // check, so cells outside the viewport are silently skipped.
    private void ApplyCursorOverlayToViewport(Surface tempSurface, Rect visibleTerm)
    {
        if (HasSelection)
        {
            ApplySelectionInversionToViewport(tempSurface, visibleTerm);
        }
        else
        {
            // Just the cursor cell.
            InvertCellInViewport(tempSurface, visibleTerm, CursorCol, CursorRow);
        }
    }

    private void ApplySelectionInversionToViewport(Surface tempSurface, Rect visibleTerm)
    {
        int w = Math.Max(0, Bounds.Width);

        switch (CursorSelectionMode)
        {
            case SelectionMode.Line:
            {
                int top = Math.Min(AnchorRow!.Value, CursorRow);
                int bottom = Math.Max(AnchorRow!.Value, CursorRow);
                for (int y = top; y <= bottom; y++)
                    InvertRowInViewport(tempSurface, visibleTerm, y, 0, w - 1);
                break;
            }
            case SelectionMode.Block:
            {
                int top = Math.Min(AnchorRow!.Value, CursorRow);
                int bottom = Math.Max(AnchorRow!.Value, CursorRow);
                int left = Math.Min(AnchorCol!.Value, CursorCol);
                int right = Math.Max(AnchorCol!.Value, CursorCol);
                for (int y = top; y <= bottom; y++)
                    InvertRowInViewport(tempSurface, visibleTerm, y, left, right);
                break;
            }
            case SelectionMode.Character:
            default:
            {
                var (sRow, sCol, eRow, eCol) = NormalizeStream();
                if (sRow == eRow)
                {
                    InvertRowInViewport(tempSurface, visibleTerm, sRow, sCol, eCol);
                }
                else
                {
                    InvertRowInViewport(tempSurface, visibleTerm, sRow, sCol, w - 1);
                    for (int y = sRow + 1; y < eRow; y++)
                        InvertRowInViewport(tempSurface, visibleTerm, y, 0, w - 1);
                    InvertRowInViewport(tempSurface, visibleTerm, eRow, 0, eCol);
                }
                break;
            }
        }
    }

    private (int startRow, int startCol, int endRow, int endCol) NormalizeStream()
    {
        int ar = AnchorRow!.Value, ac = AnchorCol!.Value;
        int cr = CursorRow, cc = CursorCol;
        if (ar < cr || (ar == cr && ac <= cc))
        {
            return (ar, ac, cr, cc);
        }
        return (cr, cc, ar, ac);
    }

    private void InvertRowInViewport(Surface tempSurface, Rect visibleTerm, int panelRow, int fromCol, int toColInclusive)
    {
        for (int x = fromCol; x <= toColInclusive; x++)
        {
            InvertCellInViewport(tempSurface, visibleTerm, x, panelRow);
        }
    }

    private void InvertCellInViewport(Surface tempSurface, Rect visibleTerm, int panelCol, int panelRow)
    {
        // Translate panel-local cell coord → temp surface buffer coord.
        int bufX = Bounds.X + panelCol - visibleTerm.X;
        int bufY = Bounds.Y + panelRow - visibleTerm.Y;
        if (bufX < 0 || bufX >= tempSurface.Width || bufY < 0 || bufY >= tempSurface.Height)
            return;

        if (tempSurface.TryGetCell(bufX, bufY, out var cell))
        {
            tempSurface.TrySetCell(bufX, bufY, cell.WithAddedAttributes(CellAttributes.Reverse));
        }
    }

    // Reads the active selection from the rendered surface as plain text,
    // mirroring the inversion geometry used by ApplySelectionInversion.
    private string ReadSelectionText(Surface surface)
    {
        var sb = new StringBuilder();
        switch (CursorSelectionMode)
        {
            case SelectionMode.Line:
            {
                int top = Math.Min(AnchorRow!.Value, CursorRow);
                int bottom = Math.Max(AnchorRow!.Value, CursorRow);
                for (int y = top; y <= bottom; y++)
                    AppendLine(surface, sb, y, 0, surface.Width - 1);
                break;
            }
            case SelectionMode.Block:
            {
                int top = Math.Min(AnchorRow!.Value, CursorRow);
                int bottom = Math.Max(AnchorRow!.Value, CursorRow);
                int left = Math.Min(AnchorCol!.Value, CursorCol);
                int right = Math.Max(AnchorCol!.Value, CursorCol);
                for (int y = top; y <= bottom; y++)
                    AppendLine(surface, sb, y, left, right);
                break;
            }
            case SelectionMode.Character:
            default:
            {
                var (sRow, sCol, eRow, eCol) = NormalizeStream();
                if (sRow == eRow)
                {
                    AppendLine(surface, sb, sRow, sCol, eCol);
                }
                else
                {
                    AppendLine(surface, sb, sRow, sCol, surface.Width - 1);
                    for (int y = sRow + 1; y < eRow; y++)
                        AppendLine(surface, sb, y, 0, surface.Width - 1);
                    AppendLine(surface, sb, eRow, 0, eCol);
                }
                break;
            }
        }
        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static void AppendLine(Surface surface, StringBuilder sb, int y, int fromCol, int toColInclusive)
    {
        int lineStart = sb.Length;
        int trimmedLength = 0;

        for (int x = fromCol; x <= toColInclusive; x++)
        {
            if (!surface.TryGetCell(x, y, out var cell))
            {
                sb.Append(' ');
                continue;
            }

            if (cell.IsContinuation)
            {
                continue;
            }

            var character = cell.Character;
            // Treat unwritten cells (UnwrittenMarker, "\uE000") and empty
            // strings as if they were a space — selecting past the end of
            // a row's painted content shouldn't surface the private-use
            // marker char in the copied text.
            if (string.IsNullOrEmpty(character) || character == SurfaceCells.UnwrittenMarker)
            {
                sb.Append(' ');
            }
            else
            {
                sb.Append(character);
                if (!IsAllWhitespace(character))
                {
                    trimmedLength = sb.Length - lineStart;
                }
            }
        }

        sb.Length = lineStart + trimmedLength;
        sb.AppendLine();
    }

    private static bool IsAllWhitespace(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            if (!char.IsWhiteSpace(s[i])) return false;
        }
        return true;
    }

    // Word movement: scan the cursor's row in the rendered surface for
    // the next/previous whitespace boundary. Re-renders the child each
    // call (acceptable for first pass; cache could be added if profiling
    // shows it as a hotspot).
    private void MoveWordForward()
    {
        if (Child is null) return;
        var bounds = Child.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var surface = RenderChildToSurface(bounds);
        int w = surface.Width;
        int row = CursorRow;
        int x = CursorCol;

        // Skip current word, then skip whitespace, land on next non-whitespace.
        while (x < w - 1 && !IsCellWhitespace(surface, x, row)) x++;
        while (x < w - 1 && IsCellWhitespace(surface, x, row)) x++;
        SetCursor(row, x);
    }

    private void MoveWordBackward()
    {
        if (Child is null) return;
        var bounds = Child.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var surface = RenderChildToSurface(bounds);
        int row = CursorRow;
        int x = CursorCol;

        // Step back one (so we don't get stuck), then skip whitespace,
        // then skip back to the start of the previous word.
        if (x > 0) x--;
        while (x > 0 && IsCellWhitespace(surface, x, row)) x--;
        while (x > 0 && !IsCellWhitespace(surface, x - 1, row)) x--;
        SetCursor(row, x);
    }

    private static bool IsCellWhitespace(Surface surface, int x, int y)
    {
        if (!surface.TryGetCell(x, y, out var cell)) return true;
        var ch = cell.Character;
        // Treat unwritten cells as whitespace so word movement crosses
        // the end of painted content the same way it crosses spaces.
        return string.IsNullOrEmpty(ch)
            || ch == SurfaceCells.UnwrittenMarker
            || IsAllWhitespace(ch);
    }

    // ------------------------------------------------------------------
    // Rich event args (text + geometry + per-node breakdown).
    // ------------------------------------------------------------------

    /// <summary>
    /// Builds the <see cref="SelectionPanelCopyEventArgs"/> for the
    /// current selection: snapshots the text, computes the bounding box
    /// in panel-local and terminal-absolute coordinates, and walks the
    /// child node tree to produce per-node intersection records.
    /// Returns args with an empty text and empty node list when no
    /// selection is active (callers gate on
    /// <see cref="HasSelection"/> before invoking).
    /// </summary>
    public SelectionPanelCopyEventArgs BuildCopyEventArgs()
    {
        if (!HasSelection)
        {
            return new SelectionPanelCopyEventArgs(
                string.Empty,
                CursorSelectionMode,
                Rect.Zero,
                Rect.Zero,
                Array.Empty<SelectionPanelSelectedNode>());
        }

        var text = SnapshotText();
        var panelBounds = ComputePanelLocalSelectionBounds();
        var terminalBounds = TranslateToTerminal(panelBounds);

        var nodes = new List<SelectionPanelSelectedNode>();
        if (Child is not null)
        {
            CollectSelectedNodes(Child, terminalBounds, nodes);
        }

        return new SelectionPanelCopyEventArgs(
            text,
            CursorSelectionMode,
            panelBounds,
            terminalBounds,
            nodes);
    }

    private Rect ComputePanelLocalSelectionBounds()
    {
        int w = Math.Max(1, Bounds.Width);
        int h = Math.Max(1, Bounds.Height);
        switch (CursorSelectionMode)
        {
            case SelectionMode.Line:
            {
                int top = Math.Min(AnchorRow!.Value, CursorRow);
                int bottom = Math.Max(AnchorRow!.Value, CursorRow);
                return new Rect(0, top, w, bottom - top + 1);
            }
            case SelectionMode.Block:
            {
                int top = Math.Min(AnchorRow!.Value, CursorRow);
                int bottom = Math.Max(AnchorRow!.Value, CursorRow);
                int left = Math.Min(AnchorCol!.Value, CursorCol);
                int right = Math.Max(AnchorCol!.Value, CursorCol);
                return new Rect(left, top, right - left + 1, bottom - top + 1);
            }
            case SelectionMode.Character:
            default:
            {
                var (sRow, sCol, eRow, eCol) = NormalizeStream();
                if (sRow == eRow)
                {
                    int left = Math.Min(sCol, eCol);
                    int right = Math.Max(sCol, eCol);
                    return new Rect(left, sRow, right - left + 1, 1);
                }
                // Multi-row stream: bounding box spans full width because
                // the start row reaches end-of-row and the end row starts
                // at column 0.
                return new Rect(0, sRow, w, eRow - sRow + 1);
            }
        }
    }

    private Rect TranslateToTerminal(Rect panelLocal)
        => new(panelLocal.X + Bounds.X, panelLocal.Y + Bounds.Y, panelLocal.Width, panelLocal.Height);

    private void CollectSelectedNodes(Hex1bNode node, Rect selectionTerminalBounds, List<SelectionPanelSelectedNode> sink)
    {
        var nodeTerminal = node.Bounds;
        if (nodeTerminal.Width > 0 && nodeTerminal.Height > 0)
        {
            // Cheap reject: if the node's bounds don't intersect the
            // selection's bbox at all, no per-row check can change that.
            var bboxIntersection = IntersectRects(nodeTerminal, selectionTerminalBounds);
            if (bboxIntersection.Width > 0 && bboxIntersection.Height > 0)
            {
                // Walk the node's rows and accumulate the bbox of cells
                // ACTUALLY in the selection. This is critical for
                // multi-row Character (stream) selections, where the
                // start row excludes cells before sCol and the end row
                // excludes cells after eCol — naive bbox intersection
                // would include nodes that touch the bbox but contain
                // zero selected cells.
                int nodeTopPanel = nodeTerminal.Y - Bounds.Y;
                int nodeLeftPanel = nodeTerminal.X - Bounds.X;
                int nodeBottomPanel = nodeTopPanel + nodeTerminal.Height; // exclusive
                int nodeRightPanel = nodeLeftPanel + nodeTerminal.Width;  // exclusive

                int minRow = int.MaxValue, maxRow = int.MinValue;
                int minCol = int.MaxValue, maxCol = int.MinValue;
                int fullyCoveredRows = 0;
                int rowsConsidered = 0;
                int nodeWidth = nodeRightPanel - nodeLeftPanel;

                for (int r = nodeTopPanel; r < nodeBottomPanel; r++)
                {
                    rowsConsidered++;
                    if (!TryGetPanelRowSelectedColRange(r, out int rowLo, out int rowHiExclusive))
                    {
                        continue;
                    }
                    int overlapLo = Math.Max(nodeLeftPanel, rowLo);
                    int overlapHi = Math.Min(nodeRightPanel, rowHiExclusive);
                    if (overlapLo >= overlapHi) continue;

                    if (r < minRow) minRow = r;
                    if (r > maxRow) maxRow = r;
                    if (overlapLo < minCol) minCol = overlapLo;
                    if (overlapHi - 1 > maxCol) maxCol = overlapHi - 1;

                    // A row is "fully covered" relative to this node when
                    // every cell in the node's horizontal extent on that
                    // row is selected.
                    if (overlapLo == nodeLeftPanel && overlapHi == nodeRightPanel)
                    {
                        fullyCoveredRows++;
                    }
                }

                if (minRow != int.MaxValue)
                {
                    var intersectionPanel = new Rect(
                        minCol,
                        minRow,
                        maxCol - minCol + 1,
                        maxRow - minRow + 1);
                    var intersectionTerminal = TranslateToTerminal(intersectionPanel);
                    var intersectionInNode = new Rect(
                        intersectionPanel.X - nodeLeftPanel,
                        intersectionPanel.Y - nodeTopPanel,
                        intersectionPanel.Width,
                        intersectionPanel.Height);
                    bool fullySelected = nodeWidth > 0 && fullyCoveredRows == rowsConsidered;
                    sink.Add(new SelectionPanelSelectedNode(node, fullySelected, intersectionInNode, intersectionTerminal));
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            CollectSelectedNodes(child, selectionTerminalBounds, sink);
        }
    }

    /// <summary>
    /// Returns the half-open <c>[lo, hiExclusive)</c> column range that is
    /// selected on the given panel-local row, or <c>false</c> if no cells
    /// on that row are selected. Columns are panel-local. Used to compute
    /// per-node actual-selection bounds without scanning every cell.
    /// </summary>
    private bool TryGetPanelRowSelectedColRange(int row, out int lo, out int hiExclusive)
    {
        lo = 0;
        hiExclusive = 0;
        if (!HasSelection) return false;

        int panelWidth = Math.Max(1, Bounds.Width);
        switch (CursorSelectionMode)
        {
            case SelectionMode.Line:
            {
                int top = Math.Min(AnchorRow!.Value, CursorRow);
                int bottom = Math.Max(AnchorRow!.Value, CursorRow);
                if (row < top || row > bottom) return false;
                lo = 0;
                hiExclusive = panelWidth;
                return true;
            }
            case SelectionMode.Block:
            {
                int top = Math.Min(AnchorRow!.Value, CursorRow);
                int bottom = Math.Max(AnchorRow!.Value, CursorRow);
                if (row < top || row > bottom) return false;
                int left = Math.Min(AnchorCol!.Value, CursorCol);
                int right = Math.Max(AnchorCol!.Value, CursorCol);
                lo = left;
                hiExclusive = right + 1;
                return true;
            }
            case SelectionMode.Character:
            default:
            {
                var (sRow, sCol, eRow, eCol) = NormalizeStream();
                if (row < sRow || row > eRow) return false;
                if (sRow == eRow)
                {
                    int left = Math.Min(sCol, eCol);
                    int right = Math.Max(sCol, eCol);
                    lo = left;
                    hiExclusive = right + 1;
                    return true;
                }
                if (row == sRow)
                {
                    lo = sCol;
                    hiExclusive = panelWidth;
                    return true;
                }
                if (row == eRow)
                {
                    lo = 0;
                    hiExclusive = eCol + 1;
                    return true;
                }
                lo = 0;
                hiExclusive = panelWidth;
                return true;
            }
        }
    }

    private static Rect IntersectRects(Rect a, Rect b)
    {
        int x = Math.Max(a.X, b.X);
        int y = Math.Max(a.Y, b.Y);
        int right = Math.Min(a.Right, b.Right);
        int bottom = Math.Min(a.Bottom, b.Bottom);
        if (right <= x || bottom <= y)
        {
            return Rect.Zero;
        }
        return new Rect(x, y, right - x, bottom - y);
    }
}
