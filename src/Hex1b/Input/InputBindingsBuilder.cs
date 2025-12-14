namespace Hex1b.Input;

/// <summary>
/// Fluent builder for constructing input bindings for a widget.
/// Pre-populated with widget defaults, allowing inspection and modification.
/// </summary>
public sealed class InputBindingsBuilder
{
    private readonly List<InputBinding> _bindings = [];
    private readonly List<CharacterBinding> _characterBindings = [];

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
    /// Starts building a character binding that matches any printable character.
    /// </summary>
    public CharacterStepBuilder AnyCharacter() => new CharacterStepBuilder(this, c => !char.IsControl(c));

    /// <summary>
    /// Starts building a character binding with a custom predicate.
    /// </summary>
    public CharacterStepBuilder Character(Func<char, bool> predicate) => new CharacterStepBuilder(this, predicate);

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
