namespace Hex1b.Input;

/// <summary>
/// Fluent builder for constructing input bindings for a widget.
/// Pre-populated with widget defaults, allowing inspection and modification.
/// </summary>
public sealed class InputBindingsBuilder
{
    private readonly List<InputBinding> _bindings = [];
    private readonly List<CharacterBinding> _characterBindings = [];
    private readonly List<MouseBinding> _mouseBindings = [];
    private readonly List<DragBinding> _dragBindings = [];
    private readonly Dictionary<ActionId, (Func<InputBindingActionContext, Task> Handler, string? Description)> _actionRegistry = new();

    /// <summary>
    /// Gets all key bindings currently configured.
    /// Inspect this at debug time to see default bindings.
    /// </summary>
    public IReadOnlyList<InputBinding> Bindings => _bindings;

    /// <summary>
    /// Gets all character bindings currently configured.
    /// Character bindings are checked after key bindings as a fallback.
    /// </summary>
    public IReadOnlyList<CharacterBinding> CharacterBindings => _characterBindings;
    
    /// <summary>
    /// Gets all mouse bindings currently configured.
    /// </summary>
    public IReadOnlyList<MouseBinding> MouseBindings => _mouseBindings;

    /// <summary>
    /// Gets all drag bindings currently configured.
    /// </summary>
    public IReadOnlyList<DragBinding> DragBindings => _dragBindings;

    /// <summary>
    /// Creates an empty builder.
    /// </summary>
    public InputBindingsBuilder()
    {
    }

    /// <summary>
    /// Starts building a key step with Ctrl modifier.
    /// </summary>
    public KeyStepBuilder Ctrl() => new KeyStepBuilder(this).Ctrl();

    /// <summary>
    /// Starts building a key step with Shift modifier.
    /// </summary>
    public KeyStepBuilder Shift() => new KeyStepBuilder(this).Shift();

    /// <summary>
    /// Starts building a key step with Alt modifier.
    /// </summary>
    public KeyStepBuilder Alt() => new KeyStepBuilder(this).Alt();

    /// <summary>
    /// Starts building a key step with the given key (no modifiers).
    /// </summary>
    public KeyStepBuilder Key(Hex1bKey key) => new KeyStepBuilder(this).Key(key);

    /// <summary>
    /// Starts building a mouse binding for the given button.
    /// </summary>
    public MouseStepBuilder Mouse(MouseButton button) => new MouseStepBuilder(this, button);

    /// <summary>
    /// Starts building a drag binding for the given button.
    /// Drag bindings capture the mouse on button down and receive move/up events until release.
    /// </summary>
    public DragStepBuilder Drag(MouseButton button) => new DragStepBuilder(this, button);

    /// <summary>
    /// Starts building a character binding that matches any printable text.
    /// </summary>
    public CharacterStepBuilder AnyCharacter() => new CharacterStepBuilder(this, IsPrintableText);

    /// <summary>
    /// Starts building a character binding with a custom predicate.
    /// </summary>
    public CharacterStepBuilder Character(Func<string, bool> predicate) => new CharacterStepBuilder(this, predicate);

    /// <summary>
    /// Default predicate for printable text (non-empty and not a control character).
    /// Handles both single characters and multi-char sequences like emojis.
    /// </summary>
    private static bool IsPrintableText(string text) 
        => text.Length > 0 && (text.Length > 1 || !char.IsControl(text[0]));

    /// <summary>
    /// Adds a pre-built binding directly.
    /// </summary>
    internal void AddBinding(InputBinding binding)
    {
        _bindings.Add(binding);
    }

    /// <summary>
    /// Adds a pre-built character binding directly.
    /// </summary>
    internal void AddCharacterBinding(CharacterBinding binding)
    {
        _characterBindings.Add(binding);
    }

    /// <summary>
    /// Adds a pre-built mouse binding directly.
    /// </summary>
    internal void AddMouseBinding(MouseBinding binding)
    {
        _mouseBindings.Add(binding);
    }

    /// <summary>
    /// Adds a pre-built drag binding directly.
    /// </summary>
    internal void AddDragBinding(DragBinding binding)
    {
        _dragBindings.Add(binding);
    }

    /// <summary>
    /// Removes all bindings that start with the specified key and modifiers.
    /// </summary>
    public void Remove(Hex1bKey key, Hex1bModifiers modifiers = Hex1bModifiers.None)
    {
        _bindings.RemoveAll(b => 
            b.Steps.Count > 0 && 
            b.Steps[0].Key == key && 
            b.Steps[0].Modifiers == modifiers);
    }

    /// <summary>
    /// Removes all bindings (key, mouse, character, and drag) with the specified action ID.
    /// The action handler remains in the registry for future rebinding via 
    /// <see cref="KeyStepBuilder.Triggers(ActionId)"/>.
    /// </summary>
    public void Remove(ActionId actionId)
    {
        _bindings.RemoveAll(b => b.ActionId == actionId);
        _mouseBindings.RemoveAll(b => b.ActionId == actionId);
        _characterBindings.RemoveAll(b => b.ActionId == actionId);
        _dragBindings.RemoveAll(b => b.ActionId == actionId);
    }

    /// <summary>
    /// Clears all bindings (including defaults).
    /// </summary>
    public void Clear() => _bindings.Clear();

    /// <summary>
    /// Removes all bindings of every type (key, mouse, character, and drag).
    /// Action handlers remain in the registry for future rebinding.
    /// </summary>
    public void RemoveAll()
    {
        _bindings.Clear();
        _characterBindings.Clear();
        _mouseBindings.Clear();
        _dragBindings.Clear();
    }

    /// <summary>
    /// Returns all key bindings with the specified action ID.
    /// </summary>
    public IReadOnlyList<InputBinding> GetBindings(ActionId actionId)
        => _bindings.Where(b => b.ActionId == actionId).ToList();

    /// <summary>
    /// Returns all unique action IDs across all binding types.
    /// </summary>
    public IReadOnlyList<ActionId> GetAllActionIds()
    {
        var ids = new HashSet<ActionId>();
        foreach (var b in _bindings)
            if (b.ActionId is { } id) ids.Add(id);
        foreach (var b in _mouseBindings)
            if (b.ActionId is { } id) ids.Add(id);
        foreach (var b in _characterBindings)
            if (b.ActionId is { } id) ids.Add(id);
        foreach (var b in _dragBindings)
            if (b.ActionId is { } id) ids.Add(id);
        return [.. ids];
    }

    /// <summary>
    /// Registers an action handler in the internal registry.
    /// Called by <see cref="KeyStepBuilder.Triggers(ActionId, Action{InputBindingActionContext}, string?)"/>
    /// during default binding setup. The registry persists across <see cref="Remove(ActionId)"/> calls.
    /// </summary>
    internal void RegisterAction(ActionId actionId, Func<InputBindingActionContext, Task> handler, string? description)
    {
        // First registration wins — if the same action is registered multiple times
        // (e.g., Enter and Spacebar both trigger Activate), keep the first handler.
        _actionRegistry.TryAdd(actionId, (handler, description));
    }

    /// <summary>
    /// Gets a previously registered action handler from the registry.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the action has not been registered.</exception>
    internal (Func<InputBindingActionContext, Task> Handler, string? Description) GetRegisteredAction(ActionId actionId)
    {
        if (!_actionRegistry.TryGetValue(actionId, out var entry))
            throw new InvalidOperationException(
                $"Action '{actionId.Value}' has not been registered. " +
                $"Use Triggers(actionId, handler, description) to register it first.");
        return entry;
    }

    /// <summary>
    /// Builds the final list of bindings.
    /// </summary>
    internal IReadOnlyList<InputBinding> Build() => _bindings;
}
