namespace Hex1b.Input;

/// <summary>
/// Holds the chord state for an input router instance.
/// Each Hex1bApp has its own state, ensuring test isolation and multi-app support.
/// </summary>
public sealed class InputRouterState
{
    /// <summary>
    /// Current chord state - the trie node we're in mid-chord, if any.
    /// </summary>
    internal ChordTrie? ChordNode { get; set; }
    
    /// <summary>
    /// The path when the chord started (to detect focus changes).
    /// </summary>
    internal List<Hex1bNode>? ChordAnchorPath { get; set; }
    
    /// <summary>
    /// The layer index of the chord (which node in the path owns the chord).
    /// </summary>
    internal int ChordLayerIndex { get; set; } = -1;
    
    /// <summary>
    /// Gets whether we're currently mid-chord.
    /// </summary>
    public bool IsInChord => ChordNode != null;
    
    /// <summary>
    /// Event raised when chord state changes (for UI feedback).
    /// </summary>
    public event Action? OnChordStateChanged;
    
    /// <summary>
    /// Resets the chord state.
    /// </summary>
    public void Reset()
    {
        var wasInChord = ChordNode != null;
        ChordNode = null;
        ChordAnchorPath = null;
        ChordLayerIndex = -1;
        if (wasInChord)
        {
            OnChordStateChanged?.Invoke();
        }
    }
    
    /// <summary>
    /// Notifies that the chord state changed.
    /// </summary>
    internal void NotifyChordStateChanged()
    {
        OnChordStateChanged?.Invoke();
    }
}
