namespace Hex1b.Events;

/// <summary>
/// Event arguments for interactable hover change events.
/// These fire from property setters (not input bindings), so no InputBindingActionContext is available.
/// </summary>
public sealed class InteractableHoverChangedEventArgs(bool isHovered)
{
    /// <summary>
    /// Whether the interactable is now hovered.
    /// </summary>
    public bool IsHovered { get; } = isHovered;
}
