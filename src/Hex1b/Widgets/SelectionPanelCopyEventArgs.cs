using Hex1b.Layout;

namespace Hex1b.Widgets;

/// <summary>
/// Payload delivered to <see cref="SelectionPanelWidget.OnCopy(Action{SelectionPanelCopyEventArgs})"/>
/// when the user commits a copy in <see cref="SelectionPanelWidget"/> copy mode.
/// </summary>
/// <remarks>
/// <para>
/// Provides the copied <see cref="Text"/> alongside the geometric and
/// structural details of what was selected. This makes
/// <see cref="SelectionPanelWidget"/> useful for more than just clipboard
/// copy — agentic UIs can use it to ask "which transcript entries did
/// the user just select?" via <see cref="Nodes"/>, and overlay tools
/// can use <see cref="PanelBounds"/> / <see cref="TerminalBounds"/> to
/// position annotations.
/// </para>
/// <para>
/// Coordinate spaces:
/// </para>
/// <list type="bullet">
/// <item><see cref="PanelBounds"/> is in panel-local (surface) coordinates
///       — <c>(0, 0)</c> is the top-left of the wrapped child.</item>
/// <item><see cref="TerminalBounds"/> is in absolute terminal
///       coordinates and equals
///       <see cref="PanelBounds"/> translated by the panel's
///       <c>Bounds</c> top-left. When the
///       panel is nested inside a scrolled <c>ScrollPanel</c> some rows
///       of <see cref="TerminalBounds"/> may sit outside the visible
///       viewport.</item>
/// </list>
/// <para>
/// For <see cref="SelectionMode.Block"/> and <see cref="SelectionMode.Line"/>
/// the bounds rectangle is exact. For
/// <see cref="SelectionMode.Character"/> (cell-stream selection) the
/// rectangle is the bounding box of the start and end points and may
/// include cells that are not part of the actual flowing selection
/// (e.g., the lower-left and upper-right corners of a multi-row
/// stream selection).
/// </para>
/// </remarks>
public sealed class SelectionPanelCopyEventArgs : EventArgs
{
    /// <summary>
    /// Plain text of the selection, matching what
    /// <see cref="Hex1b.Nodes.SelectionPanelNode.SnapshotText"/> returns.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The geometry mode active when the user committed the copy.
    /// </summary>
    public SelectionMode Mode { get; }

    /// <summary>
    /// Bounding rectangle of the selection in panel-local (surface)
    /// coordinates. <c>(0, 0)</c> is the top-left of the wrapped child.
    /// </summary>
    public Rect PanelBounds { get; }

    /// <summary>
    /// Bounding rectangle of the selection in absolute terminal
    /// coordinates. Equals <see cref="PanelBounds"/> translated by the
    /// panel's <c>Bounds</c> top-left.
    /// </summary>
    public Rect TerminalBounds { get; }

    /// <summary>
    /// Per-node breakdown of the selection: every descendant of the
    /// panel's wrapped child whose layout overlaps at least one
    /// selected cell, plus the wrapped child itself when it does.
    /// Each entry exposes the actual selected-cell footprint within
    /// the node and a flag indicating whether the node is fully
    /// selected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nodes are filtered against actually-selected cells, not just
    /// the selection's bounding box. For multi-row
    /// <see cref="SelectionMode.Character"/> selections this means a
    /// node sitting on the start row at columns before the start
    /// column will not appear in the list, even though it intersects
    /// <see cref="PanelBounds"/>.
    /// </para>
    /// <para>
    /// The list contains nodes (not widgets). Hex1b widgets are
    /// immutable specs reconciled into mutable nodes each frame and do
    /// not carry back-references to the widget that produced them.
    /// Consumers can pattern-match on node types
    /// (<c>if (entry.Node is BorderNode b) ...</c>) or use the node
    /// instance as a stable identity within the lifetime of the panel.
    /// </para>
    /// </remarks>
    public IReadOnlyList<SelectionPanelSelectedNode> Nodes { get; }

    /// <summary>
    /// Creates a new <see cref="SelectionPanelCopyEventArgs"/>.
    /// </summary>
    public SelectionPanelCopyEventArgs(
        string text,
        SelectionMode mode,
        Rect panelBounds,
        Rect terminalBounds,
        IReadOnlyList<SelectionPanelSelectedNode> nodes)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Mode = mode;
        PanelBounds = panelBounds;
        TerminalBounds = terminalBounds;
        Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
    }
}
