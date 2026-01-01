using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for <see cref="ZStackWidget"/>.
/// Stacks children on the Z-axis, with later children rendering on top of earlier ones.
/// </summary>
public sealed class ZStackNode : Hex1bNode, ILayoutProvider, IPopupHost
{
    /// <summary>
    /// The child nodes, in render order (first = bottom, last = top).
    /// </summary>
    public List<Hex1bNode> Children { get; set; } = new();
    
    /// <summary>
    /// The popup stack for this ZStack. Content pushed here appears as overlay layers.
    /// </summary>
    public PopupStack Popups { get; } = new();
    
    /// <summary>
    /// Tracks the topmost popup entry from the last reconcile cycle.
    /// Used to detect when the popup was replaced (not just added/removed).
    /// </summary>
    internal PopupEntry? LastTopmostPopupEntry { get; set; }

    /// <summary>
    /// The clip mode for the ZStack's content. Defaults to Clip.
    /// </summary>
    public ClipMode ClipMode { get; set; } = ClipMode.Clip;
    
    /// <summary>
    /// The clipping scope for this ZStack's content.
    /// </summary>
    public ClipScope ClipScopeValue { get; set; } = ClipScope.Parent;
    
    /// <summary>
    /// Resolved clip rectangle based on ClipScopeValue.
    /// Computed during Arrange.
    /// </summary>
    private Rect _resolvedClipRect;

    /// <summary>
    /// ZStack manages focus for its descendants, so nested containers don't independently set focus.
    /// </summary>
    public override bool ManagesChildFocus => true;

    #region ILayoutProvider Implementation
    
    /// <summary>
    /// The clip rectangle for child content.
    /// Uses the resolved clip scope.
    /// </summary>
    public Rect ClipRect => _resolvedClipRect;
    
    /// <inheritdoc />
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);
    
    #endregion

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // Only return focusables from the TOPMOST layer that has any focusables.
        // This prevents focus from escaping to lower layers when an overlay is shown.
        // Iterate children in reverse (topmost first) and return focusables from the first layer that has any.
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            var focusables = Children[i].GetFocusableNodes().ToList();
            if (focusables.Count > 0)
            {
                foreach (var focusable in focusables)
                {
                    yield return focusable;
                }
                yield break; // Stop after the first (topmost) layer with focusables
            }
        }
    }

    public override Size Measure(Constraints constraints)
    {
        // ZStack: take the maximum width and maximum height across all children
        var maxWidth = 0;
        var maxHeight = 0;

        foreach (var child in Children)
        {
            // All children get the full available space
            var childSize = child.Measure(constraints);
            maxWidth = Math.Max(maxWidth, childSize.Width);
            maxHeight = Math.Max(maxHeight, childSize.Height);
        }

        return constraints.Constrain(new Size(maxWidth, maxHeight));
    }

    /// <summary>
    /// ZStack needs special dirty handling: if ANY child is dirty, the entire stack
    /// must re-render in order to maintain proper z-ordering (later children on top).
    /// Exception: Single-child ZStacks (like the root popup host) don't need this.
    /// </summary>
    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        
        // Resolve the clip scope
        // For now, we don't have screen bounds or widget resolver - use simplified resolution
        var parentClip = ParentLayoutProvider?.ClipRect;
        
        // Approximate screen bounds as a large area (will be refined when we have terminal size)
        var screenBounds = new Rect(0, 0, 1000, 1000);
        
        _resolvedClipRect = ClipScopeValue.Resolve(
            bounds,
            parentClip,
            screenBounds,
            widgetNodeResolver: null); // TODO: Add widget resolver when needed

        if (Children.Count == 0) return;

        // Check if any child is dirty - if so, mark ourselves dirty to ensure
        // the entire ZStack renders in order (maintaining z-order).
        // Skip this for single-child ZStacks as there's no z-order to maintain.
        if (Children.Count > 1)
        {
            foreach (var child in Children)
            {
                if (child.NeedsRender())
                {
                    MarkDirty();
                    break;
                }
            }
        }

        // All children get the full bounds - they stack on top of each other
        foreach (var child in Children)
        {
            // For now, all children get the full bounds
            // Future: support alignment/positioning within the ZStack
            child.Arrange(bounds);
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var previousLayout = context.CurrentLayoutProvider;
        ParentLayoutProvider = previousLayout;
        context.CurrentLayoutProvider = this;
        
        // Render children in order - first child is at bottom, last is on top
        // Later children will overwrite cells from earlier children
        for (int i = 0; i < Children.Count; i++)
        {
            context.SetCursorPosition(Children[i].Bounds.X, Children[i].Bounds.Y);
            Children[i].Render(context);
        }
        
        context.CurrentLayoutProvider = previousLayout;
        ParentLayoutProvider = null;
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Tab navigation is handled by the app-level FocusRing
        bindings.Key(Hex1bKey.Tab).Action(ctx => ctx.FocusNext(), "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => ctx.FocusPrevious(), "Previous focusable");
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren() => Children;
}
