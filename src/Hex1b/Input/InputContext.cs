namespace Hex1b.Input;

/// <summary>
/// Contains the context for input handling, including the event and collected bindings.
/// Passed through the node tree during input routing.
/// </summary>
public sealed class InputContext
{
    private readonly Dictionary<(Hex1bKey, Hex1bModifiers), InputBinding> _bindings = new();

    /// <summary>
    /// The key event being processed.
    /// </summary>
    public Hex1bKeyEvent KeyEvent { get; }

    /// <summary>
    /// Creates a new input context for the given key event.
    /// </summary>
    public InputContext(Hex1bKeyEvent keyEvent)
    {
        KeyEvent = keyEvent;
    }

    /// <summary>
    /// Adds bindings to the context. Later bindings override earlier ones with the same key combination.
    /// This enables the "child wins" behavior - bindings added by child nodes override parent bindings.
    /// </summary>
    public void AddBindings(IEnumerable<InputBinding> bindings)
    {
        foreach (var binding in bindings)
        {
            var key = (binding.Key, binding.Modifiers);
            _bindings[key] = binding; // Override any existing binding for this key combo
        }
    }

    /// <summary>
    /// Tries to find and execute a binding that matches the current key event.
    /// </summary>
    /// <returns>True if a binding was matched and executed.</returns>
    public bool TryExecuteBinding()
    {
        var key = (KeyEvent.Key, KeyEvent.Modifiers);
        if (_bindings.TryGetValue(key, out var binding))
        {
            binding.Execute();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets all bindings currently in scope.
    /// </summary>
    public IEnumerable<InputBinding> GetAllBindings() => _bindings.Values;
}
