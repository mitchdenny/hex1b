namespace Hex1b.Events;

/// <summary>
/// Event arguments for interactable focus change events.
/// These fire from property setters (not input bindings), so no InputBindingActionContext is available.
/// </summary>
public sealed class InteractableFocusChangedEventArgs(bool isFocused)
{
    /// <summary>
    /// Whether the interactable is now focused.
    /// </summary>
    public bool IsFocused { get; } = isFocused;
}
