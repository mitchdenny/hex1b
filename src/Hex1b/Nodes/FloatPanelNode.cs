using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for <see cref="FloatPanelWidget"/>.
/// Positions children at absolute (x, y) character coordinates within the panel.
/// Widget order implies z-order — later children render on top of earlier ones.
/// </summary>
public sealed class FloatPanelNode : Hex1bNode, ILayoutProvider
{
    /// <summary>
    /// Represents a child node positioned at absolute coordinates within the panel.
    /// </summary>
    /// <param name="X">X offset relative to the panel's bounds.</param>
    /// <param name="Y">Y offset relative to the panel's bounds.</param>
    /// <param name="Node">The child node.</param>
    public record struct PositionedNode(int X, int Y, Hex1bNode Node);

    /// <summary>
    /// The positioned child nodes, in render order (first = bottom, last = top).
    /// </summary>
    public List<PositionedNode> Children { get; set; } = new();

    /// <summary>
    /// FloatPanel manages focus for its descendants.
    /// </summary>
    public override bool ManagesChildFocus => true;

    #region ILayoutProvider Implementation

    /// <summary>
    /// The clip mode for the FloatPanel's content. Defaults to Clip.
    /// </summary>
    public ClipMode ClipMode { get; set; } = ClipMode.Clip;

    /// <summary>
    /// The clip rectangle for child content.
    /// </summary>
    public Rect ClipRect => Bounds;

    /// <inheritdoc />
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);

    #endregion

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        foreach (var child in Children)
        {
            foreach (var focusable in child.Node.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        // FloatPanel fills available space by default
        return constraints.Constrain(new Size(constraints.MaxWidth, constraints.MaxHeight));
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);

        if (Children.Count == 0) return;

        // Check if any child is dirty — if so, mark ourselves dirty to ensure
        // the entire panel renders in order (maintaining z-order).
        if (Children.Count > 1)
        {
            foreach (var child in Children)
            {
                if (child.Node.NeedsRender())
                {
                    MarkDirty();
                    break;
                }
            }
        }

        // Arrange each child at its absolute position (offset from panel's origin)
        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            var childSize = child.Node.Measure(Constraints.Loose(bounds.Size));
            var childBounds = new Rect(
                bounds.X + child.X,
                bounds.Y + child.Y,
                childSize.Width,
                childSize.Height);
            child.Node.Arrange(childBounds);
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var previousLayout = context.CurrentLayoutProvider;
        ParentLayoutProvider = previousLayout;
        context.CurrentLayoutProvider = this;

        // Render children in order — first child is at bottom, last is on top
        for (int i = 0; i < Children.Count; i++)
        {
            context.RenderChild(Children[i].Node);
        }

        context.CurrentLayoutProvider = previousLayout;
        ParentLayoutProvider = null;
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        bindings.Key(Hex1bKey.Tab).Action(ctx => ctx.FocusNext(), "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => ctx.FocusPrevious(), "Previous focusable");
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren() => Children.Select(c => c.Node);
}
