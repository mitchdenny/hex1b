namespace Hex1b.Input;

/// <summary>
/// A binding that triggers an action on a mouse event.
/// </summary>
public sealed class MouseBinding
{
    /// <summary>
    /// The mouse button that triggers this binding.
    /// </summary>
    public MouseButton Button { get; }
    
    /// <summary>
    /// The mouse action that triggers this binding (default: Down for click).
    /// </summary>
    public MouseAction Action { get; }
    
    /// <summary>
    /// Required modifier keys.
    /// </summary>
    public Hex1bModifiers Modifiers { get; }
    
    /// <summary>
    /// The action to execute when the binding is triggered.
    /// </summary>
    public Action Handler { get; }
    
    /// <summary>
    /// Human-readable description of what this binding does.
    /// </summary>
    public string? Description { get; }

    public MouseBinding(MouseButton button, MouseAction action, Hex1bModifiers modifiers, Action handler, string? description)
    {
        Button = button;
        Action = action;
        Modifiers = modifiers;
        Handler = handler;
        Description = description;
    }

    /// <summary>
    /// Checks if this binding matches the given mouse event.
    /// </summary>
    public bool Matches(Hex1bMouseEvent mouseEvent)
    {
        return mouseEvent.Button == Button && 
               mouseEvent.Action == Action && 
               mouseEvent.Modifiers == Modifiers;
    }

    /// <summary>
    /// Executes the handler for this binding.
    /// </summary>
    public void Execute() => Handler();
}

/// <summary>
/// Fluent builder for constructing a mouse binding.
/// </summary>
public sealed class MouseStepBuilder
{
    private readonly InputBindingsBuilder _parent;
    private readonly MouseButton _button;
    private MouseAction _action = MouseAction.Down;
    private Hex1bModifiers _modifiers = Hex1bModifiers.None;

    internal MouseStepBuilder(InputBindingsBuilder parent, MouseButton button)
    {
        _parent = parent;
        _button = button;
    }

    /// <summary>
    /// Requires Ctrl modifier.
    /// </summary>
    public MouseStepBuilder Ctrl()
    {
        _modifiers |= Hex1bModifiers.Control;
        return this;
    }

    /// <summary>
    /// Requires Shift modifier.
    /// </summary>
    public MouseStepBuilder Shift()
    {
        _modifiers |= Hex1bModifiers.Shift;
        return this;
    }

    /// <summary>
    /// Requires Alt modifier.
    /// </summary>
    public MouseStepBuilder Alt()
    {
        _modifiers |= Hex1bModifiers.Alt;
        return this;
    }

    /// <summary>
    /// Binds to mouse up instead of mouse down.
    /// </summary>
    public MouseStepBuilder OnRelease()
    {
        _action = MouseAction.Up;
        return this;
    }

    /// <summary>
    /// Binds the action to execute when this mouse event occurs.
    /// </summary>
    public InputBindingsBuilder Action(Action action, string? description = null)
    {
        var binding = new MouseBinding(_button, _action, _modifiers, action, description);
        _parent.AddMouseBinding(binding);
        return _parent;
    }
}
