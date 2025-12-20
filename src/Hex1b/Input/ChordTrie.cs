namespace Hex1b.Input;

/// <summary>
/// A trie (prefix tree) for efficient chord lookup.
/// Each node represents a key step; leaf nodes contain actions.
/// </summary>
public sealed class ChordTrie
{
    private readonly Dictionary<KeyStep, ChordTrie> _children = [];
    private InputBinding? _binding;

    /// <summary>
    /// Registers a binding in the trie.
    /// Later registrations with the same key sequence override earlier ones.
    /// </summary>
    public void Register(InputBinding binding)
    {
        Register(binding.Steps, 0, binding);
    }

    private void Register(IReadOnlyList<KeyStep> steps, int index, InputBinding binding)
    {
        if (index >= steps.Count)
        {
            // Leaf node - store the binding
            _binding = binding;
            return;
        }

        var step = steps[index];
        if (!_children.TryGetValue(step, out var child))
        {
            child = new ChordTrie();
            _children[step] = child;
        }
        child.Register(steps, index + 1, binding);
    }

    /// <summary>
    /// Looks up a key step in this trie node.
    /// </summary>
    public ChordLookupResult Lookup(KeyStep step)
    {
        if (_children.TryGetValue(step, out var child))
        {
            return new ChordLookupResult(child);
        }
        return ChordLookupResult.NoMatch;
    }

    /// <summary>
    /// Looks up a key step using a key event.
    /// </summary>
    public ChordLookupResult Lookup(Hex1bKeyEvent evt)
    {
        return Lookup(new KeyStep(evt.Key, evt.Modifiers));
    }

    /// <summary>
    /// Gets whether this node has an action (is a valid endpoint).
    /// </summary>
    public bool HasAction => _binding != null;

    /// <summary>
    /// Gets whether this node has children (more steps possible).
    /// </summary>
    public bool HasChildren => _children.Count > 0;

    /// <summary>
    /// Gets whether this is a leaf node (has action, no children).
    /// </summary>
    public bool IsLeaf => HasAction && !HasChildren;

    /// <summary>
    /// Executes the action if present with the given context.
    /// </summary>
    public Task ExecuteAsync(InputBindingActionContext context) => _binding?.ExecuteAsync(context) ?? Task.CompletedTask;

    /// <summary>
    /// Gets the description if present.
    /// </summary>
    public string? Description => _binding?.Description;

    /// <summary>
    /// Builds a trie from a collection of bindings.
    /// </summary>
    public static ChordTrie Build(IEnumerable<InputBinding> bindings)
    {
        var trie = new ChordTrie();
        foreach (var binding in bindings)
        {
            trie.Register(binding);
        }
        return trie;
    }
}
