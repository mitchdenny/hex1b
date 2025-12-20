namespace Hex1b.Input;

/// <summary>
/// Fluent builder for constructing a mouse binding.
/// </summary>
public sealed class MouseStepBuilder
{
    private readonly InputBindingsBuilder _parent;
    private readonly MouseButton _button;
    private MouseAction _action = MouseAction.Down;
    private Hex1bModifiers _modifiers = Hex1bModifiers.None;
    private int _clickCount = 1;

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
    /// Requires a double-click (two clicks within the system threshold).
    /// </summary>
    public MouseStepBuilder DoubleClick()
    {
        _clickCount = 2;
        return this;
    }

    /// <summary>
    /// Requires a triple-click (three clicks within the system threshold).
    /// </summary>
    public MouseStepBuilder TripleClick()
    {
        _clickCount = 3;
        return this;
    }

    /// <summary>
    /// Binds the action to execute when this mouse event occurs.
    /// </summary>
    public InputBindingsBuilder Action(Action action, string? description = null)
    {
        var binding = new MouseBinding(_button, _action, _modifiers, _clickCount, action, description);
        _parent.AddMouseBinding(binding);
        return _parent;
    }

    /// <summary>
    /// Binds a context-aware action to execute when this mouse event occurs.
    /// </summary>
    public InputBindingsBuilder Action(Action<InputBindingActionContext> action, string? description = null)
    {
        var binding = new MouseBinding(_button, _action, _modifiers, _clickCount, action, description);
        _parent.AddMouseBinding(binding);
        return _parent;
    }

    /// <summary>
    /// Binds an async context-aware action to execute when this mouse event occurs.
    /// </summary>
    public InputBindingsBuilder Action(Func<InputBindingActionContext, Task> action, string? description = null)
    {
        var binding = new MouseBinding(_button, _action, _modifiers, _clickCount, action, description);
        _parent.AddMouseBinding(binding);
        return _parent;
    }
}
