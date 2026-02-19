using Hex1b.Documents;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating a Document Diagnostic Panel widget.
/// Renders the internal piece table structure of an IHex1bDocument as a tree.
/// </summary>
public static class DocumentDiagnosticPanelExtensions
{
    /// <summary>
    /// Creates a Tree widget that displays the internal piece table structure
    /// of the given document. Useful for debugging and understanding document state.
    /// </summary>
    public static TreeWidget DocumentDiagnosticPanel<TParent>(
        this WidgetContext<TParent> ctx,
        IHex1bDocument document)
        where TParent : Hex1bWidget
    {
        var tc = new TreeContext();
        var items = BuildDiagnosticTree(tc, document);
        return new TreeWidget(items.ToList());
    }

    private static IEnumerable<TreeItemWidget> BuildDiagnosticTree(
        TreeContext tc, IHex1bDocument document)
    {
        var info = document.GetDiagnosticInfo();
        if (info is null)
        {
            yield return tc.Item("⚠ Diagnostics not available for this document type");
            yield break;
        }

        // Document summary
        yield return tc.Item($"📄 Document v{info.Version}", sub =>
        [
            sub.Item($"Characters: {info.CharCount:N0}"),
            sub.Item($"Bytes: {info.ByteCount:N0}"),
            sub.Item($"Lines: {info.LineCount:N0}"),
        ]).Expanded();

        // Buffer info
        yield return tc.Item("🗄 Buffers", sub =>
        [
            sub.Item($"Original: {info.OriginalBufferSize:N0} bytes"),
            sub.Item($"Added: {info.AddBufferSize:N0} bytes"),
        ]).Expanded();

        // Piece tree structure
        yield return tc.Item($"🌳 Piece Tree ({info.PieceCount} nodes)", sub =>
            BuildTreeStructure(sub, info.TreeRoot)).Expanded();

        // Flat piece list (in-order)
        yield return tc.Item($"📋 Pieces in order ({info.Pieces.Count})", sub =>
            BuildPieceItems(sub, info.Pieces));
    }

    private static IEnumerable<TreeItemWidget> BuildTreeStructure(
        TreeContext tc, PieceTreeDiagnosticNode? root)
    {
        if (root == null)
        {
            yield return tc.Item("(empty)");
            yield break;
        }

        yield return BuildTreeNode(tc, root, "Root");
    }

    private static TreeItemWidget BuildTreeNode(
        TreeContext tc, PieceTreeDiagnosticNode node, string position)
    {
        var colorIcon = node.Color == "Red" ? "🔴" : "⚫";
        var sourceIcon = node.Source == "Original" ? "📦" : "✏️";
        var label = $"{colorIcon} {position}: {sourceIcon} {node.Source} @{node.Start} len={node.Length} (left={node.LeftSubtreeSize})";

        if (node.Left == null && node.Right == null)
            return tc.Item(label);

        return tc.Item(label, sub =>
        {
            var children = new List<TreeItemWidget>();
            if (node.Left != null)
                children.Add(BuildTreeNode(sub, node.Left, "L"));
            else
                children.Add(sub.Item("L: (nil)"));
            if (node.Right != null)
                children.Add(BuildTreeNode(sub, node.Right, "R"));
            else
                children.Add(sub.Item("R: (nil)"));
            return children;
        }).Expanded();
    }

    private static IEnumerable<TreeItemWidget> BuildPieceItems(
        TreeContext tc, IReadOnlyList<PieceDiagnosticInfo> pieces)
    {
        for (var i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            var icon = piece.Source == "Original" ? "📦" : "✏️";
            var label = $"{icon} [{piece.Index}] {piece.Source} @{piece.Start} len={piece.Length}";

            yield return tc.Item(label, sub =>
            {
                var items = new List<TreeItemWidget>();

                // Hex preview
                var hexStr = BitConverter.ToString(piece.PreviewBytes).Replace("-", " ");
                if (piece.Length > piece.PreviewBytes.Length)
                    hexStr += " …";
                items.Add(sub.Item($"Hex: {hexStr}"));

                // Text preview — escape control chars for display
                var textPreview = EscapeForDisplay(piece.PreviewText);
                if (piece.Length > piece.PreviewBytes.Length)
                    textPreview += "…";
                items.Add(sub.Item($"Text: {textPreview}"));

                return items;
            });
        }
    }

    private static string EscapeForDisplay(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch == '\n') sb.Append("\\n");
            else if (ch == '\r') sb.Append("\\r");
            else if (ch == '\t') sb.Append("\\t");
            else if (ch == '\0') sb.Append("\\0");
            else if (char.IsControl(ch)) sb.Append($"\\u{(int)ch:X4}");
            else sb.Append(ch);
            }
        return sb.ToString();
    }
}
