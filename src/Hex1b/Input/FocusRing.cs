namespace Hex1b.Input;

/// <summary>
/// Maintains a flat ring of all focusable nodes from the current render.
/// Built after each render by traversing GetFocusableNodes() on the root.
/// Provides efficient navigation between focusables regardless of tree structure.
/// </summary>
public sealed class FocusRing
{
    private readonly List<Hex1bNode> _focusables = [];
    
    /// <summary>
    /// Tracks the previously focused node so we can clear its focus if it leaves the ring.
    /// </summary>
    private Hex1bNode? _previouslyFocusedNode;
    
    /// <summary>
    /// Gets all focusable nodes in render order.
    /// </summary>
    public IReadOnlyList<Hex1bNode> Focusables => _focusables;
    
    /// <summary>
    /// Gets the currently focused node, or null if none.
    /// </summary>
    public Hex1bNode? FocusedNode => _focusables.FirstOrDefault(n => n.IsFocused);
    
    /// <summary>
    /// Gets the index of the currently focused node, or -1 if none.
    /// </summary>
    public int FocusedIndex => _focusables.FindIndex(n => n.IsFocused);

    /// <summary>
    /// Rebuilds the focus ring from the given root node.
    /// Call this after each render.
    /// </summary>
    public void Rebuild(Hex1bNode? root)
    {
        // Remember who was focused before rebuilding
        _previouslyFocusedNode = FocusedNode;
        
        _focusables.Clear();
        if (root != null)
        {
            _focusables.AddRange(root.GetFocusableNodes());
        }
        
        // If the previously focused node is no longer in the ring, clear its focus
        if (_previouslyFocusedNode != null && 
            _previouslyFocusedNode.IsFocused && 
            !_focusables.Contains(_previouslyFocusedNode))
        {
            _previouslyFocusedNode.IsFocused = false;
            _previouslyFocusedNode = null;
        }
    }

    /// <summary>
    /// Moves focus to the next focusable node in the ring.
    /// Wraps around from last to first.
    /// </summary>
    /// <returns>True if focus was moved, false if there are no focusables.</returns>
    public bool FocusNext()
    {
        if (_focusables.Count == 0) return false;
        
        var currentIndex = FocusedIndex;
        
        // DEBUG: Track what we're doing
        var fromNode = currentIndex >= 0 ? _focusables[currentIndex] : null;
        
        if (currentIndex >= 0)
        {
            _focusables[currentIndex].IsFocused = false;
        }
        
        var nextIndex = (currentIndex + 1) % _focusables.Count;
        var toNode = _focusables[nextIndex];
        _focusables[nextIndex].IsFocused = true;
        SyncAncestorFocusState(_focusables[nextIndex]);
        
        // DEBUG: Log the focus change
        LastFocusChange = $"FocusNext: from index {currentIndex} to {nextIndex}, from {fromNode?.GetType().Name} to {toNode.GetType().Name}";
        
        return true;
    }
    
    /// <summary>
    /// Debug log of the last focus change. Useful for testing.
    /// </summary>
    public string? LastFocusChange { get; private set; }

    /// <summary>
    /// Moves focus to the previous focusable node in the ring.
    /// Wraps around from first to last.
    /// </summary>
    /// <returns>True if focus was moved, false if there are no focusables.</returns>
    public bool FocusPrevious()
    {
        if (_focusables.Count == 0) return false;
        
        var currentIndex = FocusedIndex;
        if (currentIndex >= 0)
        {
            _focusables[currentIndex].IsFocused = false;
        }
        
        var prevIndex = currentIndex <= 0 ? _focusables.Count - 1 : currentIndex - 1;
        _focusables[prevIndex].IsFocused = true;
        SyncAncestorFocusState(_focusables[prevIndex]);
        
        return true;
    }

    /// <summary>
    /// Focuses a specific node if it's in the ring.
    /// </summary>
    /// <returns>True if the node was found and focused.</returns>
    public bool Focus(Hex1bNode node)
    {
        var index = _focusables.IndexOf(node);
        if (index < 0) return false;
        
        var currentIndex = FocusedIndex;
        if (currentIndex >= 0)
        {
            _focusables[currentIndex].IsFocused = false;
        }
        
        node.IsFocused = true;
        SyncAncestorFocusState(node);
        
        return true;
    }
    
    /// <summary>
    /// Focuses the first node in the ring that matches the predicate.
    /// </summary>
    /// <param name="predicate">A function that returns true for the node to focus.</param>
    /// <returns>True if a matching node was found and focused, false otherwise.</returns>
    public bool FocusWhere(Func<Hex1bNode, bool> predicate)
    {
        var node = _focusables.FirstOrDefault(predicate);
        if (node == null) return false;
        
        return Focus(node);
    }

    /// <summary>
    /// Ensures the first focusable has focus if nothing is currently focused.
    /// </summary>
    public void EnsureFocus()
    {
        if (_focusables.Count > 0 && FocusedIndex < 0)
        {
            _focusables[0].IsFocused = true;
            SyncAncestorFocusState(_focusables[0]);
        }
    }

    /// <summary>
    /// Syncs ancestor container focus indices to match the newly focused node.
    /// </summary>
    private static void SyncAncestorFocusState(Hex1bNode focusedNode)
    {
        // Walk up the tree and call SyncFocusIndex on each ancestor
        var current = focusedNode.Parent;
        while (current != null)
        {
            current.SyncFocusIndex();
            current = current.Parent;
        }
    }
    
    /// <summary>
    /// Finds the focusable node at the given screen coordinates.
    /// Returns the topmost (last in render order) focusable node whose bounds contain the point.
    /// </summary>
    /// <param name="x">X coordinate (0-based column).</param>
    /// <param name="y">Y coordinate (0-based row).</param>
    /// <returns>The focusable node at the position, or null if none.</returns>
    public Hex1bNode? HitTest(int x, int y)
    {
        // Search in reverse order (last rendered = topmost)
        for (int i = _focusables.Count - 1; i >= 0; i--)
        {
            var node = _focusables[i];
            // Use HitTestBounds which may be more specific than Bounds
            // (e.g., SplitterNode returns only the divider area)
            var bounds = node.HitTestBounds;
            
            if (x >= bounds.X && x < bounds.Right && 
                y >= bounds.Y && y < bounds.Bottom)
            {
                return node;
            }
        }
        
        return null;
    }
}
