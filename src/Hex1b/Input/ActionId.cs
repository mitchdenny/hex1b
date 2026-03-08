namespace Hex1b.Input;

/// <summary>
/// A strongly typed identifier for a rebindable action.
/// Used to tag keybindings so they can be referenced by action rather than by key.
/// </summary>
/// <param name="Value">
/// The identifier string. Convention: "WidgetName.ActionName" in PascalCase
/// (e.g., "List.MoveUp", "TextBox.DeleteBackward", "Button.Activate").
/// </param>
public readonly record struct ActionId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;
}
