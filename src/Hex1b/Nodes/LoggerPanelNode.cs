using Hex1b.Logging;
using Hex1b.Nodes;

namespace Hex1b;

/// <summary>
/// Render node for LoggerPanelWidget.
/// Manages follow state for the log table.
/// </summary>
public sealed class LoggerPanelNode : CompositeNode
{
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
        var tableNode = FindTableNodePublic(ContentChild);
        tableNode?.ScrollToEnd();
    }

    /// <summary>
    /// Walks the node tree to find the inner TableNode.
    /// </summary>
    internal static TableNode<Hex1bLogEntry>? FindTableNodePublic(Hex1bNode? node)
    {
        if (node is TableNode<Hex1bLogEntry> table)
            return table;

        if (node == null)
            return null;

        foreach (var child in node.GetChildren())
        {
            var found = FindTableNodePublic(child);
            if (found != null)
                return found;
        }

        return null;
    }
}
