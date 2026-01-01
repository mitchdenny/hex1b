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
    /// Clears all bindings (including defaults).
    /// </summary>
    public void Clear() => _bindings.Clear();

    /// <summary>
    /// Builds the final list of bindings.
    /// </summary>
    internal IReadOnlyList<InputBinding> Build() => _bindings;
}
