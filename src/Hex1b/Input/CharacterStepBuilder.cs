namespace Hex1b.Input;

/// <summary>
/// Fluent builder for constructing character/text bindings.
/// </summary>
public sealed class CharacterStepBuilder
{
    private readonly InputBindingsBuilder _parent;
    private readonly Func<string, bool> _predicate;

    internal CharacterStepBuilder(InputBindingsBuilder parent, Func<string, bool> predicate)
    {
        _parent = parent;
        _predicate = predicate;
    }

    /// <summary>
    /// Completes the binding with the given action handler that receives the text.
    /// </summary>
    public void Action(Action<string> handler, string? description = null)
    {
        _parent.AddCharacterBinding(new CharacterBinding(_predicate, handler, description));
    }

    /// <summary>
    /// Completes the binding with the given async action handler that receives the text and context.
    /// </summary>
    public void Action(Func<string, InputBindingActionContext, Task> handler, string? description = null)
    {
        _parent.AddCharacterBinding(new CharacterBinding(_predicate, handler, description));
    }
}
