namespace Custard.Input;

/// <summary>
/// Represents a keyboard shortcut with a key binding, action, and description.
/// </summary>
public sealed record Shortcut(KeyBinding Binding, Action Action, string Description)
{
    /// <summary>
    /// Checks if this shortcut matches the given input event.
    /// </summary>
    public bool Matches(KeyInputEvent evt) => Binding.Matches(evt);

    /// <summary>
    /// Executes the shortcut action.
    /// </summary>
    public void Execute() => Action();
}
