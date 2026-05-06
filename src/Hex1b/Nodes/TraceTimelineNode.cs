using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Composite node for the trace timeline widget. Manages a horizontal split
/// layout with a tree on the left and timeline panel on the right.
/// </summary>
internal sealed class TraceTimelineNode<T> : Hex1bNode
{
    /// <summary>
    /// The tree node (left panel).
    /// </summary>
    public Hex1bNode? TreeChild { get; set; }

    /// <summary>
    /// The timeline VStack node (right panel).
    /// </summary>
    public Hex1bNode? TimelineChild { get; set; }

    /// <summary>
    /// Width of the left (tree) panel. Defaults to 30.
    /// </summary>
    public int TreePanelWidth { get; set; } = 30;

    protected override Size MeasureCore(Constraints constraints)
    {
        if (TreeChild == null)
            return constraints.Constrain(new Size(0, 0));

        var treeWidth = Math.Min(TreePanelWidth, constraints.MaxWidth);
        TreeChild.Measure(new Constraints(0, treeWidth, 0, constraints.MaxHeight));

        var timelineWidth = Math.Max(0, constraints.MaxWidth - treeWidth - 1); // -1 for divider
        TimelineChild?.Measure(new Constraints(0, timelineWidth, 0, constraints.MaxHeight));

        return constraints.Constrain(new Size(constraints.MaxWidth, constraints.MaxHeight));
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);

        var treeWidth = Math.Min(TreePanelWidth, bounds.Width);
        TreeChild?.Arrange(new Rect(bounds.X, bounds.Y, treeWidth, bounds.Height));

        var timelineX = bounds.X + treeWidth + 1; // +1 for divider
        var timelineWidth = Math.Max(0, bounds.Width - treeWidth - 1);
        TimelineChild?.Arrange(new Rect(timelineX, bounds.Y, timelineWidth, bounds.Height));
    }

    public override void Render(Hex1bRenderContext context)
    {
        TreeChild?.Render(context);

        // Draw divider
        if (TreeChild != null && TimelineChild != null)
        {
            var dividerX = Bounds.X + TreePanelWidth;
            var dividerColor = context.Theme.Get(SplitterTheme.DividerColor);
            for (int y = Bounds.Y; y < Bounds.Y + Bounds.Height; y++)
            {
                context.SetCursorPosition(dividerX, y);
                context.Write($"{dividerColor.ToForegroundAnsi()}│{context.Theme.GetResetToGlobalCodes()}");
            }
        }

        TimelineChild?.Render(context);
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (TreeChild != null) yield return TreeChild;
        if (TimelineChild != null) yield return TimelineChild;
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (TreeChild != null)
        {
            foreach (var focusable in TreeChild.GetFocusableNodes())
                yield return focusable;
        }
        if (TimelineChild != null)
        {
            foreach (var focusable in TimelineChild.GetFocusableNodes())
                yield return focusable;
        }
    }
}
