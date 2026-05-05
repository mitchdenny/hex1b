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
    private bool _overridesCapture;

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
    /// Marks this binding as overriding input capture. When the owning
    /// node has captured input, the binding fires on a matching mouse
    /// event whose coordinates fall within the captured node's hit-test
    /// bounds — even if a focusable child would otherwise consume the
    /// click. Mirrors <see cref="KeyStepBuilder.OverridesCapture"/> for
    /// keyboard bindings.
    /// </summary>
    /// <remarks>
    /// Currently only takes effect for mouse press (Down) events;
    /// <see cref="OnRelease"/> bindings marked with
    /// <see cref="OverridesCapture"/> are not consulted by the
    /// capture-aware routing path. Use a Down binding (the default) for
    /// modal commit-style interactions like right-click-to-confirm.
    /// </remarks>
    public MouseStepBuilder OverridesCapture()
    {
        _overridesCapture = true;
        return this;
    }

    /// <summary>
    /// Binds the action to execute when this mouse event occurs.
    /// </summary>
    public InputBindingsBuilder Action(Action action, string? description = null)
    {
        var binding = new MouseBinding(_button, _action, _modifiers, _clickCount, action, description, actionId: null, overridesCapture: _overridesCapture);
        _parent.Add(binding);
        return _parent;
    }

    /// <summary>
    /// Binds a context-aware action to execute when this mouse event occurs.
    /// </summary>
    public InputBindingsBuilder Action(Action<InputBindingActionContext> action, string? description = null)
    {
        var binding = new MouseBinding(_button, _action, _modifiers, _clickCount, action, description, actionId: null, overridesCapture: _overridesCapture);
        _parent.Add(binding);
        return _parent;
    }

    /// <summary>
    /// Binds an async context-aware action to execute when this mouse event occurs.
    /// </summary>
    public InputBindingsBuilder Action(Func<InputBindingActionContext, Task> action, string? description = null)
    {
        var binding = new MouseBinding(_button, _action, _modifiers, _clickCount, action, description, actionId: null, overridesCapture: _overridesCapture);
        _parent.Add(binding);
        return _parent;
    }

    /// <summary>
    /// Binds a mouse action and registers it for the specified action ID.
    /// The handler is registered in the action registry for future rebinding.
    /// </summary>
    public InputBindingsBuilder Triggers(ActionId actionId, Action action, string? description = null)
    {
        _parent.RegisterAction(actionId, _ => { action(); return Task.CompletedTask; }, description);
        var binding = new MouseBinding(_button, _action, _modifiers, _clickCount, action, description, actionId, overridesCapture: _overridesCapture);
        _parent.Add(binding);
        return _parent;
    }

    /// <summary>
    /// Binds a mouse action and registers it for the specified action ID.
    /// The handler is registered in the action registry for future rebinding.
    /// </summary>
    public InputBindingsBuilder Triggers(ActionId actionId, Action<InputBindingActionContext> action, string? description = null)
    {
        _parent.RegisterAction(actionId, ctx => { action(ctx); return Task.CompletedTask; }, description);
        var binding = new MouseBinding(_button, _action, _modifiers, _clickCount, action, description, actionId, overridesCapture: _overridesCapture);
        _parent.Add(binding);
        return _parent;
    }

    /// <summary>
    /// Binds a mouse action and registers it for the specified action ID.
    /// </summary>
    public InputBindingsBuilder Triggers(ActionId actionId, Func<InputBindingActionContext, Task> action, string? description = null)
    {
        _parent.RegisterAction(actionId, action, description);
        var binding = new MouseBinding(_button, _action, _modifiers, _clickCount, action, description, actionId, overridesCapture: _overridesCapture);
        _parent.Add(binding);
        return _parent;
    }
}
