namespace Hex1b.Input;

/// <summary>
/// Result of looking up a key step in a chord trie.
/// </summary>
public readonly struct ChordLookupResult
{
    /// <summary>
    /// The matched trie node, or null if no match.
    /// </summary>
    public ChordTrie? Node { get; }

    /// <summary>
    /// Whether the lookup found a match.
    /// </summary>
    public bool IsMatch => Node != null;

    /// <summary>
    /// Whether the lookup found no match.
    /// </summary>
    public bool IsNoMatch => Node == null;

    /// <summary>
    /// Whether the matched node is a leaf (has action, no further children).
    /// </summary>
    public bool IsLeaf => Node?.IsLeaf ?? false;

    /// <summary>
    /// Whether the matched node has an action (could be executed now).
    /// </summary>
    public bool HasAction => Node?.HasAction ?? false;

    /// <summary>
    /// Whether the matched node has children (more steps possible).
    /// </summary>
    public bool HasChildren => Node?.HasChildren ?? false;

    public ChordLookupResult(ChordTrie node)
    {
        Node = node;
    }

    /// <summary>
    /// A result indicating no match was found.
    /// </summary>
    public static ChordLookupResult NoMatch => default;

    /// <summary>
    /// Executes the action if present with the given context.
    /// </summary>
    public Task ExecuteAsync(InputBindingActionContext context) => Node?.ExecuteAsync(context) ?? Task.CompletedTask;
}
