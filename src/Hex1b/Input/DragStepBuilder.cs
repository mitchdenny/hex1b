namespace Hex1b.Input;

/// <summary>
/// Fluent builder for constructing a drag binding.
/// </summary>
public sealed class DragStepBuilder
{
    private readonly InputBindingsBuilder _parent;
    private readonly MouseButton _button;
    private Hex1bModifiers _modifiers = Hex1bModifiers.None;

    internal DragStepBuilder(InputBindingsBuilder parent, MouseButton button)
    {
        _parent = parent;
        _button = button;
    }

    /// <summary>
    /// Requires Ctrl modifier.
    /// Cannot be combined with Shift modifier.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if Shift modifier is already set.</exception>
    public DragStepBuilder Ctrl()
    {
        if ((_modifiers & Hex1bModifiers.Shift) != 0)
            throw new InvalidOperationException("Cannot combine Ctrl and Shift modifiers. Use either Ctrl or Shift, but not both.");
        
        _modifiers |= Hex1bModifiers.Control;
        return this;
    }

    /// <summary>
    /// Requires Shift modifier.
    /// Cannot be combined with Ctrl modifier.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if Ctrl modifier is already set.</exception>
    public DragStepBuilder Shift()
    {
        if ((_modifiers & Hex1bModifiers.Control) != 0)
            throw new InvalidOperationException("Cannot combine Ctrl and Shift modifiers. Use either Ctrl or Shift, but not both.");
        
        _modifiers |= Hex1bModifiers.Shift;
        return this;
    }

    /// <summary>
    /// Binds the drag factory. The factory receives (startX, startY) local coordinates
    /// and returns a DragHandler that will receive move and end events.
    /// </summary>
    public InputBindingsBuilder Action(Func<int, int, DragHandler> factory, string? description = null)
    {
        var binding = new DragBinding(_button, _modifiers, factory, description);
        _parent.AddDragBinding(binding);
        return _parent;
    }
}
