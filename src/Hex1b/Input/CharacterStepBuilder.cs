namespace Hex1b.Input;

/// <summary>
/// Fluent builder for constructing character bindings.
/// </summary>
public sealed class CharacterStepBuilder
{
    private readonly InputBindingsBuilder _parent;
    private readonly Func<char, bool> _predicate;

    internal CharacterStepBuilder(InputBindingsBuilder parent, Func<char, bool> predicate)
    {
        _parent = parent;
        _predicate = predicate;
    }

    /// <summary>
    /// Completes the binding with the given action handler that receives the character.
    /// </summary>
    public void Action(Action<char> handler, string? description = null)
    {
        _parent.AddCharacterBinding(new CharacterBinding(_predicate, handler, description));
    }
}
