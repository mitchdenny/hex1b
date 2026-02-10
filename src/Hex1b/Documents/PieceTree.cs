using System.Text;

namespace Hex1b.Documents;

/// <summary>
/// A red-black tree of pieces for efficient O(log n) piece table operations.
/// Each node stores a piece (buffer reference) and caches the total byte size
/// of its left subtree, enabling O(log n) offset-to-piece lookup.
/// </summary>
internal sealed class PieceTree
{
    internal enum NodeColor : byte { Red, Black }
    internal enum BufferSource : byte { Original, Added }

    internal sealed class Node
    {
        public BufferSource Source;
        public int Start;
        public int Length;
        public int LeftSubtreeSize; // cached total bytes in left subtree
        public NodeColor Color;
        public Node? Left;
        public Node? Right;
        public Node? Parent;

        public Node(BufferSource source, int start, int length, NodeColor color)
        {
            Source = source;
            Start = start;
            Length = length;
            Color = color;
        }
    }

    internal Node? Root { get; private set; }
    public int Count { get; private set; }
    public int TotalBytes { get; private set; }

    /// <summary>
    /// Finds the node containing the given byte offset and returns the offset within that node.
    /// Returns (null, 0) if the tree is empty or offset equals TotalBytes (append position).
    /// </summary>
    public (Node? Node, int OffsetInNode) FindAt(int byteOffset)
    {
        if (Root == null || byteOffset >= TotalBytes)
            return (null, 0);

        var node = Root;
        var remaining = byteOffset;

        while (node != null)
        {
            var leftSize = node.LeftSubtreeSize;

            if (remaining < leftSize)
            {
                node = node.Left;
            }
            else if (remaining < leftSize + node.Length)
            {
                return (node, remaining - leftSize);
            }
            else
            {
                remaining -= leftSize + node.Length;
                node = node.Right;
            }
        }

        return (null, 0);
    }

    /// <summary>
    /// Inserts a new piece at the given byte offset, splitting existing nodes if needed.
    /// </summary>
    public void Insert(int byteOffset, BufferSource source, int start, int length)
    {
        if (length == 0) return;

        if (Root == null)
        {
            Root = new Node(source, start, length, NodeColor.Black);
            Count = 1;
            TotalBytes = length;
            return;
        }

        if (byteOffset >= TotalBytes)
        {
            // Append at end
            var rightmost = GetRightmost(Root);
            var newNode = new Node(source, start, length, NodeColor.Red)
            {
                Parent = rightmost
            };
            rightmost.Right = newNode;
            Count++;
            TotalBytes += length;
            FixAfterInsert(newNode);
            return;
        }

        var (targetNode, offsetInNode) = FindAt(byteOffset);
        if (targetNode == null)
        {
            // Append
            var rightmost = GetRightmost(Root);
            var newNode = new Node(source, start, length, NodeColor.Red)
            {
                Parent = rightmost
            };
            rightmost.Right = newNode;
            Count++;
            TotalBytes += length;
            FixAfterInsert(newNode);
            return;
        }

        if (offsetInNode == 0)
        {
            // Insert before targetNode
            InsertBefore(targetNode, source, start, length);
        }
        else
        {
            // Split targetNode at offsetInNode
            var origSource = targetNode.Source;
            var origStart = targetNode.Start;
            var origLength = targetNode.Length;

            // Shrink target to left portion — account for the lost bytes
            targetNode.Length = offsetInNode;
            TotalBytes -= (origLength - offsetInNode);
            UpdateSubtreeSizesUp(targetNode.Parent);

            // Insert new piece after target
            var newNode = InsertAfter(targetNode, source, start, length);

            // Insert right portion after new piece (restores the trimmed bytes)
            InsertAfter(newNode, origSource, origStart + offsetInNode, origLength - offsetInNode);
        }
    }

    /// <summary>
    /// Deletes bytes in range [byteOffset, byteOffset + deleteLength).
    /// </summary>
    public void Delete(int byteOffset, int deleteLength)
    {
        if (deleteLength == 0 || Root == null) return;

        var end = byteOffset + deleteLength;

        // Find start node
        var (startNode, startOffset) = FindAt(byteOffset);
        if (startNode == null) return;

        // Collect nodes to process
        var nodesToRemove = new List<Node>();
        Node? firstPartialStart = null;
        int firstPartialNewLength = 0;
        Node? lastPartialEnd = null;
        int lastPartialNewStart = 0;
        int lastPartialNewLength = 0;

        // Walk in-order from startNode
        var node = startNode;
        var currentByteOffset = byteOffset - startOffset; // byte offset of start of startNode

        while (node != null && currentByteOffset < end)
        {
            var nodeEnd = currentByteOffset + node.Length;

            if (currentByteOffset >= byteOffset && nodeEnd <= end)
            {
                // Entire node is within delete range
                nodesToRemove.Add(node);
            }
            else if (currentByteOffset < byteOffset && nodeEnd > end)
            {
                // Delete range is entirely within this node — split into left + right
                var leftLen = byteOffset - currentByteOffset;
                var rightStart = node.Start + (end - currentByteOffset);
                var rightLen = nodeEnd - end;

                // Shrink node to left portion, subtract the middle (deleted) + right
                var shrinkage = node.Length - leftLen;
                node.Length = leftLen;
                TotalBytes -= shrinkage;
                UpdateSubtreeSizesUp(node.Parent);

                // Re-add the right portion
                InsertAfter(node, node.Source, rightStart, rightLen);
                // TotalBytes is now correct: lost shrinkage, gained rightLen
                // shrinkage = deleteLength + rightLen, so net = -deleteLength ✓
                return; // skip the final TotalBytes -= deleteLength
            }
            else if (currentByteOffset < byteOffset)
            {
                // Partial start — trim right side
                firstPartialStart = node;
                firstPartialNewLength = byteOffset - currentByteOffset;
            }
            else if (nodeEnd > end)
            {
                // Partial end — trim left side
                lastPartialEnd = node;
                lastPartialNewStart = node.Start + (end - currentByteOffset);
                lastPartialNewLength = nodeEnd - end;
            }

            currentByteOffset = nodeEnd;
            node = InOrderSuccessor(node);
        }

        // Apply partial trims
        if (firstPartialStart != null)
        {
            firstPartialStart.Length = firstPartialNewLength;
            UpdateSubtreeSizes(firstPartialStart);
        }

        if (lastPartialEnd != null)
        {
            lastPartialEnd.Start = lastPartialNewStart;
            lastPartialEnd.Length = lastPartialNewLength;
            UpdateSubtreeSizes(lastPartialEnd);
        }

        // Remove fully deleted nodes
        foreach (var n in nodesToRemove)
        {
            RemoveNode(n);
        }

        TotalBytes -= deleteLength;
    }

    /// <summary>
    /// Visits all pieces in order, calling the action with (source, start, length).
    /// </summary>
    public void InOrderTraversal(Action<BufferSource, int, int> action)
    {
        InOrderTraversal(Root, action);
    }

    private static void InOrderTraversal(Node? node, Action<BufferSource, int, int> action)
    {
        if (node == null) return;
        InOrderTraversal(node.Left, action);
        action(node.Source, node.Start, node.Length);
        InOrderTraversal(node.Right, action);
    }

    /// <summary>
    /// Collects all nodes in order for diagnostics/iteration.
    /// </summary>
    public List<(BufferSource Source, int Start, int Length)> ToList()
    {
        var list = new List<(BufferSource, int, int)>(Count);
        InOrderTraversal((source, start, length) => list.Add((source, start, length)));
        return list;
    }

    /// <summary>
    /// Returns diagnostic info about the tree structure.
    /// </summary>
    public PieceTreeDiagnosticNode? GetDiagnosticRoot()
    {
        return BuildDiagnosticNode(Root);
    }

    private static PieceTreeDiagnosticNode? BuildDiagnosticNode(Node? node)
    {
        if (node == null) return null;
        return new PieceTreeDiagnosticNode
        {
            Source = node.Source == BufferSource.Original ? "Original" : "Added",
            Start = node.Start,
            Length = node.Length,
            LeftSubtreeSize = node.LeftSubtreeSize,
            Color = node.Color == NodeColor.Red ? "Red" : "Black",
            Left = BuildDiagnosticNode(node.Left),
            Right = BuildDiagnosticNode(node.Right)
        };
    }

    // ── Red-black tree operations ───────────────────────────────

    private void InsertBefore(Node target, BufferSource source, int start, int length)
    {
        var newNode = new Node(source, start, length, NodeColor.Red);

        if (target.Left == null)
        {
            target.Left = newNode;
            newNode.Parent = target;
            target.LeftSubtreeSize += length;
        }
        else
        {
            var pred = GetRightmost(target.Left);
            pred.Right = newNode;
            newNode.Parent = pred;
        }

        Count++;
        TotalBytes += length;
        UpdateSubtreeSizesUp(newNode.Parent);
        FixAfterInsert(newNode);
    }

    private Node InsertAfter(Node target, BufferSource source, int start, int length)
    {
        var newNode = new Node(source, start, length, NodeColor.Red);

        if (target.Right == null)
        {
            target.Right = newNode;
            newNode.Parent = target;
        }
        else
        {
            var succ = GetLeftmost(target.Right);
            succ.Left = newNode;
            newNode.Parent = succ;
            succ.LeftSubtreeSize += length;
        }

        Count++;
        TotalBytes += length;
        UpdateSubtreeSizesUp(newNode.Parent);
        FixAfterInsert(newNode);
        return newNode;
    }

    private void RemoveNode(Node node)
    {
        Count--;

        if (node.Left != null && node.Right != null)
        {
            // Replace with in-order successor
            var successor = GetLeftmost(node.Right);
            node.Source = successor.Source;
            node.Start = successor.Start;
            node.Length = successor.Length;
            UpdateSubtreeSizes(node);
            node = successor;
        }

        var child = node.Left ?? node.Right;
        if (child != null)
        {
            child.Parent = node.Parent;
            if (node.Parent == null)
            {
                Root = child;
            }
            else if (node == node.Parent.Left)
            {
                node.Parent.Left = child;
                UpdateSubtreeSizesUp(node.Parent);
            }
            else
            {
                node.Parent.Right = child;
                UpdateSubtreeSizesUp(node.Parent);
            }

            if (node.Color == NodeColor.Black)
                FixAfterDelete(child);
        }
        else if (node.Parent == null)
        {
            Root = null;
        }
        else
        {
            if (node.Color == NodeColor.Black)
                FixAfterDelete(node);

            if (node.Parent != null)
            {
                if (node == node.Parent.Left)
                {
                    node.Parent.Left = null;
                    UpdateSubtreeSizesUp(node.Parent);
                }
                else
                {
                    node.Parent.Right = null;
                    UpdateSubtreeSizesUp(node.Parent);
                }
                node.Parent = null;
            }
        }
    }

    private void FixAfterInsert(Node node)
    {
        while (node != Root && node.Parent?.Color == NodeColor.Red)
        {
            var parent = node.Parent!;
            var grandparent = parent.Parent;
            if (grandparent == null) break;

            if (parent == grandparent.Left)
            {
                var uncle = grandparent.Right;
                if (uncle?.Color == NodeColor.Red)
                {
                    parent.Color = NodeColor.Black;
                    uncle.Color = NodeColor.Black;
                    grandparent.Color = NodeColor.Red;
                    node = grandparent;
                }
                else
                {
                    if (node == parent.Right)
                    {
                        node = parent;
                        RotateLeft(node);
                        parent = node.Parent!;
                        grandparent = parent.Parent!;
                    }
                    parent.Color = NodeColor.Black;
                    grandparent.Color = NodeColor.Red;
                    RotateRight(grandparent);
                }
            }
            else
            {
                var uncle = grandparent.Left;
                if (uncle?.Color == NodeColor.Red)
                {
                    parent.Color = NodeColor.Black;
                    uncle.Color = NodeColor.Black;
                    grandparent.Color = NodeColor.Red;
                    node = grandparent;
                }
                else
                {
                    if (node == parent.Left)
                    {
                        node = parent;
                        RotateRight(node);
                        parent = node.Parent!;
                        grandparent = parent.Parent!;
                    }
                    parent.Color = NodeColor.Black;
                    grandparent.Color = NodeColor.Red;
                    RotateLeft(grandparent);
                }
            }
        }

        Root!.Color = NodeColor.Black;
    }

    private void FixAfterDelete(Node node)
    {
        while (node != Root && GetColor(node) == NodeColor.Black)
        {
            if (node.Parent == null) break;

            if (node == node.Parent.Left)
            {
                var sibling = node.Parent.Right;
                if (sibling == null) break;

                if (sibling.Color == NodeColor.Red)
                {
                    sibling.Color = NodeColor.Black;
                    node.Parent.Color = NodeColor.Red;
                    RotateLeft(node.Parent);
                    sibling = node.Parent.Right;
                    if (sibling == null) break;
                }

                if (GetColor(sibling.Left) == NodeColor.Black &&
                    GetColor(sibling.Right) == NodeColor.Black)
                {
                    sibling.Color = NodeColor.Red;
                    node = node.Parent;
                }
                else
                {
                    if (GetColor(sibling.Right) == NodeColor.Black)
                    {
                        if (sibling.Left != null)
                            sibling.Left.Color = NodeColor.Black;
                        sibling.Color = NodeColor.Red;
                        RotateRight(sibling);
                        sibling = node.Parent.Right;
                        if (sibling == null) break;
                    }

                    sibling.Color = node.Parent.Color;
                    node.Parent.Color = NodeColor.Black;
                    if (sibling.Right != null)
                        sibling.Right.Color = NodeColor.Black;
                    RotateLeft(node.Parent);
                    node = Root!;
                }
            }
            else
            {
                var sibling = node.Parent.Left;
                if (sibling == null) break;

                if (sibling.Color == NodeColor.Red)
                {
                    sibling.Color = NodeColor.Black;
                    node.Parent.Color = NodeColor.Red;
                    RotateRight(node.Parent);
                    sibling = node.Parent.Left;
                    if (sibling == null) break;
                }

                if (GetColor(sibling.Right) == NodeColor.Black &&
                    GetColor(sibling.Left) == NodeColor.Black)
                {
                    sibling.Color = NodeColor.Red;
                    node = node.Parent;
                }
                else
                {
                    if (GetColor(sibling.Left) == NodeColor.Black)
                    {
                        if (sibling.Right != null)
                            sibling.Right.Color = NodeColor.Black;
                        sibling.Color = NodeColor.Red;
                        RotateLeft(sibling);
                        sibling = node.Parent.Left;
                        if (sibling == null) break;
                    }

                    sibling.Color = node.Parent.Color;
                    node.Parent.Color = NodeColor.Black;
                    if (sibling.Left != null)
                        sibling.Left.Color = NodeColor.Black;
                    RotateRight(node.Parent);
                    node = Root!;
                }
            }
        }

        if (node != null)
            node.Color = NodeColor.Black;
    }

    private void RotateLeft(Node node)
    {
        var right = node.Right;
        if (right == null) return;

        node.Right = right.Left;
        if (right.Left != null)
            right.Left.Parent = node;

        right.Parent = node.Parent;
        if (node.Parent == null)
            Root = right;
        else if (node == node.Parent.Left)
            node.Parent.Left = right;
        else
            node.Parent.Right = right;

        right.Left = node;
        node.Parent = right;

        // Update subtree sizes
        right.LeftSubtreeSize += node.LeftSubtreeSize + node.Length;
        // node's LeftSubtreeSize is unchanged (its left subtree didn't change)
    }

    private void RotateRight(Node node)
    {
        var left = node.Left;
        if (left == null) return;

        node.Left = left.Right;
        if (left.Right != null)
            left.Right.Parent = node;

        left.Parent = node.Parent;
        if (node.Parent == null)
            Root = left;
        else if (node == node.Parent.Right)
            node.Parent.Right = left;
        else
            node.Parent.Left = left;

        left.Right = node;
        node.Parent = left;

        // Update subtree sizes
        node.LeftSubtreeSize -= left.LeftSubtreeSize + left.Length;
        // left's LeftSubtreeSize is unchanged (its left subtree didn't change)
    }

    private static NodeColor GetColor(Node? node) => node?.Color ?? NodeColor.Black;

    private static Node GetLeftmost(Node node)
    {
        while (node.Left != null) node = node.Left;
        return node;
    }

    private static Node GetRightmost(Node node)
    {
        while (node.Right != null) node = node.Right;
        return node;
    }

    private static Node? InOrderSuccessor(Node node)
    {
        if (node.Right != null)
            return GetLeftmost(node.Right);

        var parent = node.Parent;
        while (parent != null && node == parent.Right)
        {
            node = parent;
            parent = parent.Parent;
        }
        return parent;
    }

    /// <summary>
    /// Recomputes LeftSubtreeSize for a node from its left child.
    /// </summary>
    private static void UpdateSubtreeSizes(Node node)
    {
        node.LeftSubtreeSize = SubtreeSize(node.Left);
    }

    /// <summary>
    /// Walks up from a node, recomputing LeftSubtreeSize at each ancestor.
    /// </summary>
    private static void UpdateSubtreeSizesUp(Node? node)
    {
        while (node != null)
        {
            node.LeftSubtreeSize = SubtreeSize(node.Left);
            node = node.Parent;
        }
    }

    private static int SubtreeSize(Node? node)
    {
        if (node == null) return 0;
        return node.LeftSubtreeSize + node.Length + SubtreeSize(node.Right);
    }
}

/// <summary>
/// Diagnostic snapshot of a piece tree node for visualization.
/// </summary>
public sealed class PieceTreeDiagnosticNode
{
    public string Source { get; init; } = "";
    public int Start { get; init; }
    public int Length { get; init; }
    public int LeftSubtreeSize { get; init; }
    public string Color { get; init; } = "";
    public PieceTreeDiagnosticNode? Left { get; init; }
    public PieceTreeDiagnosticNode? Right { get; init; }
}
