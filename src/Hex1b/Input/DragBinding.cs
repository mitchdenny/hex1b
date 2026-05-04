namespace Hex1b.Input;

/// <summary>
/// A binding that initiates a drag operation on mouse down.
/// The factory is called at drag start and returns a DragHandler that receives subsequent events.
/// </summary>
public sealed class DragBinding
{
    /// <summary>
    /// The mouse button that initiates the drag.
    /// </summary>
    public MouseButton Button { get; }
    
    /// <summary>
    /// Required modifier keys.
    /// </summary>
    public Hex1bModifiers Modifiers { get; }
    
    /// <summary>
    /// Factory that creates a DragHandler when the drag starts.
    /// Receives the local (x, y) coordinates where the drag began.
    /// </summary>
    public Func<int, int, DragHandler> Factory { get; }
    
    /// <summary>
    /// Human-readable description of what this drag binding does.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// A stable identifier for the action this binding performs.
    /// Used for programmatic rebinding via <see cref="InputBindingsBuilder.Remove(ActionId)"/>.
    /// </summary>
    /// <remarks>
    /// When supplied via the constructor (e.g., for a binding registered through
    /// <see cref="InputBindingsBuilder.Add(DragBinding)"/>), this id supports
    /// <see cref="InputBindingsBuilder.Remove(ActionId)"/> only — it is NOT registered
    /// in the rebinding registry.
    /// </remarks>
    public ActionId? ActionId { get; }

    public DragBinding(MouseButton button, Hex1bModifiers modifiers, Func<int, int, DragHandler> factory, string? description, ActionId? actionId = null)
    {
        Button = button;
        Modifiers = modifiers;
        Factory = factory;
        Description = description;
        ActionId = actionId;
    }

    /// <summary>
    /// Checks if this binding matches the given mouse down event.
    /// </summary>
    public bool Matches(Hex1bMouseEvent mouseEvent)
    {
        return mouseEvent.Button == Button && 
               mouseEvent.Action == MouseAction.Down && 
               mouseEvent.Modifiers == Modifiers;
    }

    /// <summary>
    /// Starts the drag by invoking the factory with the local coordinates.
    /// </summary>
    public DragHandler StartDrag(int localX, int localY) => Factory(localX, localY);
}
