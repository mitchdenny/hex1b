using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="WindowPanelWidget"/>.
/// A container that hosts floating windows rendered on top of main content.
/// </summary>
public sealed class WindowPanelNode : Hex1bNode, IWindowHost, ILayoutProvider
{
    /// <summary>
    /// Optional name for this panel. Used when multiple WindowPanels exist.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The main content node displayed behind windows.
    /// </summary>
    public Hex1bNode? Content { get; set; }

    /// <summary>
    /// The window nodes, in z-order (bottom to top).
    /// </summary>
    public List<WindowNode> WindowNodes { get; } = [];

    /// <summary>
    /// The window manager for this panel.
    /// </summary>
    public WindowManager Windows { get; } = new();

    /// <summary>
    /// The clip mode for the panel's content. Defaults to Clip.
    /// </summary>
    public ClipMode ClipMode { get; set; } = ClipMode.Clip;

    /// <summary>
    /// The clipping scope for this panel's content.
    /// </summary>
    public ClipScope ClipScopeValue { get; set; } = ClipScope.Parent;

    /// <summary>
    /// Resolved clip rectangle based on ClipScopeValue.
    /// Computed during Arrange.
    /// </summary>
    private Rect _resolvedClipRect;

    #region ILayoutProvider Implementation

    /// <summary>
    /// The clip rectangle for child content.
    /// </summary>
    public Rect ClipRect => _resolvedClipRect;

    /// <inheritdoc />
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);

    #endregion

    /// <summary>
    /// WindowPanel manages focus for its descendants including windows.
    /// </summary>
    public override bool ManagesChildFocus => true;

    /// <summary>
    /// Reconciles window nodes from the WindowManager.
    /// Called by WindowPanelWidget after reconciling content.
    /// </summary>
    internal async Task ReconcileWindowsAsync(ReconcileContext context)
    {
        var entries = Windows.All;
        var activeWindow = Windows.ActiveWindow;
        var newWindowNodes = new List<WindowNode>();

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            // Find existing node for this entry
            var existingNode = WindowNodes.FirstOrDefault(n => n.Entry?.Id == entry.Id);

            // Create the window widget and reconcile
            var windowWidget = new WindowWidget(entry);
            var childContext = context.WithLayoutAxis(LayoutAxis.Vertical);
            var reconciledNode = await childContext.ReconcileChildAsync(existingNode, windowWidget, this) as WindowNode;

            if (reconciledNode != null)
            {
                reconciledNode.IsActive = ReferenceEquals(entry, activeWindow);
                newWindowNodes.Add(reconciledNode);
            }
        }

        // Track removed windows for bounds clearing
        foreach (var oldNode in WindowNodes)
        {
            if (!newWindowNodes.Any(n => n.Entry?.Id == oldNode.Entry?.Id))
            {
                if (oldNode.Bounds.Width > 0 && oldNode.Bounds.Height > 0)
                {
                    AddOrphanedChildBounds(oldNode.Bounds);
                }
            }
        }

        WindowNodes.Clear();
        WindowNodes.AddRange(newWindowNodes);

        // Focus management: if windows exist, focus the active window's first focusable
        if (activeWindow?.Node != null)
        {
            var focusables = activeWindow.Node.GetFocusableNodes().ToList();
            if (focusables.Count > 0 && !focusables.Any(f => f.IsFocused))
            {
                // Clear focus from other nodes first
                if (Content != null)
                {
                    foreach (var focusable in Content.GetFocusableNodes())
                    {
                        if (focusable.IsFocused)
                        {
                            ReconcileContext.SetNodeFocus(focusable, false);
                        }
                    }
                }

                ReconcileContext.SetNodeFocus(focusables[0], true);
            }
        }
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // If there are windows, only return focusables from the topmost window
        // (unless modal, in which case only the modal's focusables)
        if (WindowNodes.Count > 0)
        {
            // If there's a modal, only its focusables are accessible
            var modal = WindowNodes.LastOrDefault(w => w.IsModal);
            if (modal != null)
            {
                foreach (var focusable in modal.GetFocusableNodes())
                {
                    yield return focusable;
                }
                yield break;
            }

            // Otherwise, return focusables from all windows (topmost last for priority)
            // and the main content
            if (Content != null)
            {
                foreach (var focusable in Content.GetFocusableNodes())
                {
                    yield return focusable;
                }
            }

            foreach (var windowNode in WindowNodes)
            {
                foreach (var focusable in windowNode.GetFocusableNodes())
                {
                    yield return focusable;
                }
            }
        }
        else
        {
            // No windows - just return content focusables
            if (Content != null)
            {
                foreach (var focusable in Content.GetFocusableNodes())
                {
                    yield return focusable;
                }
            }
        }
    }

    public override Size Measure(Constraints constraints)
    {
        // Measure content to fill available space
        var contentSize = Content?.Measure(constraints) ?? Size.Zero;

        // Measure windows (they have their own fixed sizes)
        foreach (var windowNode in WindowNodes)
        {
            windowNode.Measure(constraints);
        }

        return constraints.Constrain(contentSize);
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        // Resolve the clip scope
        var parentClip = ParentLayoutProvider?.ClipRect;
        var screenBounds = new Rect(0, 0, 1000, 1000);
        _resolvedClipRect = ClipScopeValue.Resolve(bounds, parentClip, screenBounds, widgetNodeResolver: null);

        // Arrange content to fill bounds
        Content?.Arrange(bounds);

        // Arrange windows at their positions
        foreach (var windowNode in WindowNodes)
        {
            if (windowNode.Entry == null) continue;

            var entry = windowNode.Entry;
            var windowWidth = entry.Width;
            var windowHeight = entry.Height;

            // Calculate position (center if not specified)
            int windowX, windowY;
            if (entry.X.HasValue && entry.Y.HasValue)
            {
                windowX = entry.X.Value;
                windowY = entry.Y.Value;
            }
            else
            {
                // Use position spec to calculate initial position
                (windowX, windowY) = entry.PositionSpec.Calculate(
                    bounds, 
                    windowWidth, 
                    windowHeight,
                    entry.X,
                    entry.Y);

                // Store calculated position so subsequent renders use the same position
                entry.X = windowX;
                entry.Y = windowY;
            }

            // Clamp to panel bounds (in case of resize or explicit out-of-bounds position)
            windowX = Math.Max(bounds.X, Math.Min(windowX, bounds.X + bounds.Width - windowWidth));
            windowY = Math.Max(bounds.Y, Math.Min(windowY, bounds.Y + bounds.Height - windowHeight));

            var windowBounds = new Rect(windowX, windowY, windowWidth, windowHeight);
            windowNode.Arrange(windowBounds);
        }

        // Mark dirty if any window is dirty (z-order maintenance)
        if (WindowNodes.Count > 1)
        {
            foreach (var windowNode in WindowNodes)
            {
                if (windowNode.NeedsRender())
                {
                    MarkDirty();
                    break;
                }
            }
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var previousLayout = context.CurrentLayoutProvider;
        ParentLayoutProvider = previousLayout;
        context.CurrentLayoutProvider = this;

        // Render content first (bottom layer)
        if (Content != null)
        {
            context.RenderChild(Content);
        }

        // Render windows in z-order (bottom to top)
        foreach (var windowNode in WindowNodes)
        {
            context.RenderChild(windowNode);
        }

        context.CurrentLayoutProvider = previousLayout;
        ParentLayoutProvider = null;
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Tab navigation through focusables
        bindings.Key(Hex1bKey.Tab).Action(ctx => ctx.FocusNext(), "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => ctx.FocusPrevious(), "Previous focusable");
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Content != null) yield return Content;
        foreach (var windowNode in WindowNodes)
        {
            yield return windowNode;
        }
    }
}
