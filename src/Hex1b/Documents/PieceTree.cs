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
    /// Validates internal consistency of the piece tree. Throws if corrupt.
    /// </summary>
    internal void VerifyIntegrity()
    {
        var (actualCount, actualBytes) = VerifyNode(Root, null);
        if (actualCount != Count)
            throw new InvalidOperationException($"Count mismatch: tracked={Count}, actual={actualCount}");
        if (actualBytes != TotalBytes)
            throw new InvalidOperationException($"TotalBytes mismatch: tracked={TotalBytes}, actual={actualBytes}");
    }

    private static (int nodeCount, int totalBytes) VerifyNode(Node? node, Node? expectedParent)
    {
        if (node == null) return (0, 0);
        if (node.Parent != expectedParent)
            throw new InvalidOperationException($"Parent pointer mismatch for node (src={node.Source}, start={node.Start}, len={node.Length})");
        if (node.Length < 0)
            throw new InvalidOperationException($"Negative length: {node.Length}");

        var (leftCount, leftBytes) = VerifyNode(node.Left, node);
        var (rightCount, rightBytes) = VerifyNode(node.Right, node);

        if (node.LeftSubtreeSize != leftBytes)
            throw new InvalidOperationException(
                $"LeftSubtreeSize mismatch: cached={node.LeftSubtreeSize}, actual={leftBytes} " +
                $"(node: src={node.Source}, start={node.Start}, len={node.Length})");

        return (leftCount + 1 + rightCount, leftBytes + node.Length + rightBytes);
    }

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
            // Append at end — try extending the rightmost node first
            var rightmost = GetRightmost(Root);
            if (rightmost.Source == source && rightmost.Start + rightmost.Length == start)
            {
                // Consecutive in the same buffer — just extend
                rightmost.Length += length;
                TotalBytes += length;
                UpdateSubtreeSizesUp(rightmost.Parent);
                return;
            }

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

        var (startNode, startOffset) = FindAt(byteOffset);
        if (startNode == null) return;

        var startNodeByteOffset = byteOffset - startOffset;
        var startNodeEnd = startNodeByteOffset + startNode.Length;

        // Fast path 1: delete is entirely within one node — split into left + right
        if (startNodeByteOffset < byteOffset && startNodeEnd > end)
        {
            var leftLen = byteOffset - startNodeByteOffset;
            var rightStart = startNode.Start + (end - startNodeByteOffset);
            var rightLen = startNodeEnd - end;

            var shrinkage = startNode.Length - leftLen;
            startNode.Length = leftLen;
            TotalBytes -= shrinkage;
            UpdateSubtreeSizesUp(startNode.Parent);

            InsertAfter(startNode, startNode.Source, rightStart, rightLen);
            return;
        }

        // Fast path 2: trim start of a node (delete covers the beginning)
        if (startNodeByteOffset == byteOffset && startNodeEnd > end)
        {
            var trimAmount = end - startNodeByteOffset;
            startNode.Start += trimAmount;
            startNode.Length -= trimAmount;
            TotalBytes -= trimAmount;
            UpdateSubtreeSizesUp(startNode.Parent);
            return;
        }

        // Fast path 3: trim end of a node (delete covers the tail)
        if (startNodeByteOffset < byteOffset && startNodeEnd == end)
        {
            var trimAmount = startNodeEnd - byteOffset;
            startNode.Length -= trimAmount;
            TotalBytes -= trimAmount;
            UpdateSubtreeSizesUp(startNode.Parent);
            return;
        }

        // Fast path 4: delete exactly one complete node — safe single RemoveNode
        if (startNodeByteOffset == byteOffset && startNodeEnd == end)
        {
            RemoveNode(startNode);
            TotalBytes -= deleteLength;
            return;
        }

        // General case: collect surviving pieces, rebuild tree.
        // Used for deletes spanning 3+ nodes (rare in practice).
        var surviving = new List<(BufferSource Source, int Start, int Length)>();
        var current = 0;

        InOrderTraversal((source, start, length) =>
        {
            var pieceEnd = current + length;

            if (pieceEnd <= byteOffset || current >= end)
            {
                surviving.Add((source, start, length));
            }
            else
            {
                if (current < byteOffset)
                    surviving.Add((source, start, byteOffset - current));
                if (pieceEnd > end)
                {
                    var rightOffset = end - current;
                    surviving.Add((source, start + rightOffset, pieceEnd - end));
                }
            }

            current = pieceEnd;
        });

        Root = null;
        Count = 0;
        TotalBytes = 0;

        foreach (var (source, start, length) in surviving)
        {
            Insert(TotalBytes, source, start, length);
        }
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

    private static Node? InOrderPredecessor(Node node)
    {
        if (node.Left != null)
            return GetRightmost(node.Left);

        var parent = node.Parent;
        while (parent != null && node == parent.Left)
        {
            node = parent;
            parent = parent.Parent;
        }
        return parent;
    }

    /// <summary>
    /// Tries to merge a node with its in-order predecessor and successor
    /// if they reference consecutive ranges in the same buffer.
    /// This prevents piece count from growing unbounded during byte-level edits.
    /// </summary>
    private void TryMergeWithNeighbors(Node node)
    {
        // Try merge with successor first
        var succ = InOrderSuccessor(node);
        if (succ != null &&
            succ.Source == node.Source &&
            succ.Start == node.Start + node.Length)
        {
            // Absorb successor into node
            node.Length += succ.Length;
            RemoveNode(succ);
            UpdateSubtreeSizesUp(node.Parent);
        }

        // Try merge with predecessor
        var pred = InOrderPredecessor(node);
        if (pred != null &&
            pred.Source == node.Source &&
            pred.Start + pred.Length == node.Start)
        {
            // Absorb node into predecessor
            pred.Length += node.Length;
            RemoveNode(node);
            UpdateSubtreeSizesUp(pred.Parent);
        }
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
