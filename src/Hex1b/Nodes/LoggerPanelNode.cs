using Hex1b.Layout;
using Hex1b.Logging;
using Hex1b.Nodes;

namespace Hex1b;

/// <summary>
/// Render node for LoggerPanelWidget.
/// Manages follow state for the log table.
/// </summary>
public sealed class LoggerPanelNode : Hex1bNode
{
    /// <summary>
    /// The reconciled content child node (the inner table).
    /// </summary>
    public Hex1bNode? ContentChild { get; set; }

    /// <summary>
    /// When true, the table auto-scrolls to the latest log entry.
    /// Defaults to true. Set to false when the user scrolls up.
    /// </summary>
    public bool IsFollowing { get; set; } = true;

    /// <summary>
    /// Reference to the log store for accessing the data source.
    /// </summary>
    internal IHex1bLogStore? LogStore { get; set; }

    /// <summary>
    /// Scrolls the inner table to the end when in follow mode.
    /// Called after reconciliation when new data has arrived.
    /// </summary>
    internal void ScrollTableToEnd()
    {
        var tableNode = FindTableNode(ContentChild);
        tableNode?.ScrollToEnd();
    }

    /// <summary>
    /// Walks the node tree to find the inner TableNode.
    /// </summary>
    internal static TableNode<Hex1bLogEntry>? FindTableNode(Hex1bNode? node)
    {
        if (node is TableNode<Hex1bLogEntry> table)
            return table;

        if (node == null)
            return null;

        foreach (var child in node.GetChildren())
        {
            var found = FindTableNode(child);
            if (found != null)
                return found;
        }

        return null;
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (ContentChild != null) yield return ContentChild;
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        return ContentChild?.Measure(constraints) ?? constraints.Constrain(Size.Zero);
    }

    protected override void ArrangeCore(Rect rect)
    {
        base.Arrange(rect);
        ContentChild?.Arrange(rect);
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (ContentChild != null)
        {
            context.RenderChild(ContentChild);
        }
    }

    public override bool IsFocusable => false;

    public override bool IsFocused
    {
        get => false;
        set
        {
            if (ContentChild != null)
                ContentChild.IsFocused = value;
        }
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (ContentChild != null)
        {
            foreach (var focusable in ContentChild.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }
}
