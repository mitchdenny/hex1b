namespace Hex1b.Input;

/// <summary>
/// A mouse input event.
/// </summary>
/// <param name="Button">The mouse button involved (None for move events).</param>
/// <param name="Action">The type of mouse action.</param>
/// <param name="X">The X coordinate (0-based column).</param>
/// <param name="Y">The Y coordinate (0-based row).</param>
/// <param name="Modifiers">The modifier keys held during the event.</param>
/// <param name="ClickCount">The number of consecutive clicks (1=single, 2=double, 3=triple). Only relevant for Down actions.</param>
public sealed record Hex1bMouseEvent(
    MouseButton Button,
    MouseAction Action,
    int X,
    int Y,
    Hex1bModifiers Modifiers,
    int ClickCount = 1
) : Hex1bEvent
{
    /// <summary>
    /// Returns true if this is a double-click event (ClickCount == 2).
    /// </summary>
    public bool IsDoubleClick => ClickCount == 2;
    
    /// <summary>
    /// Returns true if this is a triple-click event (ClickCount == 3).
    /// </summary>
    public bool IsTripleClick => ClickCount == 3;
    
    /// <summary>
    /// Creates a copy of this event with a different click count.
    /// Used by Hex1bApp to set the computed click count.
    /// </summary>
    public Hex1bMouseEvent WithClickCount(int clickCount) => this with { ClickCount = clickCount };
}
