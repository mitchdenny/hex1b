using Hex1b.Layout;
using Hex1b.Surfaces;

namespace Hex1b.Nodes;

/// <summary>
/// A single-child container node that intercepts rendering to apply visual effects.
/// Layout, arrangement, and focus are passed through to the child unchanged.
/// During rendering, the child's output is captured to a temporary <see cref="Surface"/>,
/// the effect callback modifies it, then the result is composited to the parent.
/// </summary>
public sealed class EffectPanelNode : Hex1bNode
{
    /// <summary>
    /// Gets or sets the child node to render.
    /// </summary>
    public Hex1bNode? Child { get; set; }

    /// <summary>
    /// Gets or sets the effect callback that post-processes the child's rendered surface.
    /// </summary>
    public Action<Surface>? Effect { get; set; }

    public override Size Measure(Constraints constraints)
    {
        if (Child is null)
            return constraints.Constrain(Size.Zero);
        return Child.Measure(constraints);
    }

    public override void Arrange(Rect rect)
    {
        base.Arrange(rect);
        Child?.Arrange(rect);
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Child is null) return;

        if (Effect is null || context is not SurfaceRenderContext surfaceCtx)
        {
            // No effect or non-surface context: pass through unchanged
            context.RenderChild(Child);
            return;
        }

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        // 1. Create temp surface sized to our bounds
        var tempSurface = new Surface(Bounds.Width, Bounds.Height, surfaceCtx.CellMetrics);

        // 2. Create child context with offset (same pattern as SurfaceRenderContext.RenderChild)
        var tempContext = new SurfaceRenderContext(
            tempSurface, Bounds.X, Bounds.Y, context.Theme, surfaceCtx.TrackedObjectStore)
        {
            CachingEnabled = surfaceCtx.CachingEnabled,
            MouseX = surfaceCtx.MouseX,
            MouseY = surfaceCtx.MouseY,
            CellMetrics = surfaceCtx.CellMetrics
        };

        // 3. Render child subtree to temp surface
        tempContext.SetCursorPosition(Child.Bounds.X, Child.Bounds.Y);
        tempContext.RenderChild(Child);

        // 4. Apply effect
        Effect(tempSurface);

        // 5. Composite modified surface into parent, respecting clip region
        Rect? clipRect = null;
        if (surfaceCtx.CurrentLayoutProvider != null)
        {
            var providerClip = surfaceCtx.CurrentLayoutProvider.ClipRect;
            clipRect = new Rect(
                providerClip.X - surfaceCtx.OffsetX,
                providerClip.Y - surfaceCtx.OffsetY,
                providerClip.Width,
                providerClip.Height);
        }
        surfaceCtx.Surface.Composite(
            tempSurface,
            Bounds.X - surfaceCtx.OffsetX,
            Bounds.Y - surfaceCtx.OffsetY,
            clipRect);
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child is not null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
                yield return focusable;
        }
    }

    public override IReadOnlyList<Hex1bNode> GetChildren()
    {
        return Child is not null ? [Child] : [];
    }
}
