namespace Hex1b.Input;

/// <summary>
/// Fluent builder for constructing key steps in an input binding.
/// Supports chaining modifiers, keys, and multi-step chords.
/// </summary>
public sealed class KeyStepBuilder
{
    private readonly InputBindingsBuilder _parent;
    private readonly List<KeyStep> _completedSteps = [];
    private Hex1bModifiers _currentModifiers = Hex1bModifiers.None;
    private Hex1bKey? _currentKey;
    private bool _isGlobal;
    private bool _overridesCapture;
    private ActionId? _actionId;

    internal KeyStepBuilder(InputBindingsBuilder parent)
    {
        _parent = parent;
    }

    /// <summary>
    /// Marks this binding as global (evaluated regardless of focus).
    /// Global bindings are checked before focus-based routing.
    /// Useful for menu bar accelerators that should work from anywhere.
    /// </summary>
    public KeyStepBuilder Global()
    {
        _isGlobal = true;
        return this;
    }
    
    /// <summary>
    /// Marks this binding as overriding input capture.
    /// When another node has captured all input, this binding will still be checked.
    /// Use for menu accelerators and app-level shortcuts that should always work.
    /// </summary>
    /// <remarks>
    /// This is typically combined with <see cref="Global"/> for bindings that should
    /// work regardless of both focus and capture state.
    /// </remarks>
    public KeyStepBuilder OverridesCapture()
    {
        _overridesCapture = true;
        return this;
    }

    /// <summary>
    /// Adds Ctrl modifier to the current step.
    /// </summary>
    public KeyStepBuilder Ctrl()
    {
        _currentModifiers |= Hex1bModifiers.Control;
        return this;
    }

    /// <summary>
    /// Adds Shift modifier to the current step.
    /// </summary>
    public KeyStepBuilder Shift()
    {
        _currentModifiers |= Hex1bModifiers.Shift;
        return this;
    }

    /// <summary>
    /// Adds Alt modifier to the current step.
    /// </summary>
    public KeyStepBuilder Alt()
    {
        _currentModifiers |= Hex1bModifiers.Alt;
        return this;
    }

    /// <summary>
    /// Sets the key for the current step.
    /// </summary>
    public KeyStepBuilder Key(Hex1bKey key)
    {
        _currentKey = key;
        return this;
    }

    /// <summary>
    /// Commits the current step and starts a new one for chords.
    /// </summary>
    public KeyStepBuilder Then()
    {
        CommitCurrentStep();
        return this;
    }

    /// <summary>
    /// Completes the binding with the given action handler.
    /// </summary>
    public void Action(Action handler, string? description = null)
    {
        CommitCurrentStep();
        var binding = new InputBinding([.. _completedSteps], handler, description, _isGlobal);
        binding.OverridesCapture = _overridesCapture;
        binding.ActionId = _actionId;
        _parent.AddBinding(binding);
    }

    /// <summary>
    /// Completes the binding with a context-aware action handler.
    /// The context provides access to app-level services like focus navigation.
    /// </summary>
    public void Action(Action<InputBindingActionContext> handler, string? description = null)
    {
        CommitCurrentStep();
        var binding = new InputBinding([.. _completedSteps], handler, description, _isGlobal);
        binding.OverridesCapture = _overridesCapture;
        binding.ActionId = _actionId;
        _parent.AddBinding(binding);
    }

    /// <summary>
    /// Completes the binding with an async context-aware action handler.
    /// </summary>
    public void Action(Func<InputBindingActionContext, Task> handler, string? description = null)
    {
        CommitCurrentStep();
        var binding = new InputBinding([.. _completedSteps], handler, description, _isGlobal);
        binding.OverridesCapture = _overridesCapture;
        binding.ActionId = _actionId;
        _parent.AddBinding(binding);
    }

    /// <summary>
    /// Completes the binding by registering it for the specified action.
    /// The handler is provided and registered in the action registry for future rebinding.
    /// </summary>
    /// <param name="actionId">The action this binding triggers.</param>
    /// <param name="handler">The handler to execute (no context).</param>
    /// <param name="description">Optional description for help/documentation.</param>
    public void Triggers(ActionId actionId, Action handler, string? description = null)
    {
        _actionId = actionId;
        _parent.RegisterAction(actionId, _ => { handler(); return Task.CompletedTask; }, description);
        Action(handler, description);
    }

    /// <summary>
    /// Completes the binding by registering it for the specified action.
    /// The handler is provided and registered in the action registry for future rebinding.
    /// </summary>
    /// <param name="actionId">The action this binding triggers.</param>
    /// <param name="handler">The handler to execute.</param>
    /// <param name="description">Optional description for help/documentation.</param>
    public void Triggers(ActionId actionId, Action<InputBindingActionContext> handler, string? description = null)
    {
        _actionId = actionId;
        _parent.RegisterAction(actionId, ctx => { handler(ctx); return Task.CompletedTask; }, description);
        Action(handler, description);
    }

    /// <summary>
    /// Completes the binding by registering it for the specified action.
    /// The handler is provided and registered in the action registry for future rebinding.
    /// </summary>
    public void Triggers(ActionId actionId, Func<InputBindingActionContext, Task> handler, string? description = null)
    {
        _actionId = actionId;
        _parent.RegisterAction(actionId, handler, description);
        Action(handler, description);
    }

    /// <summary>
    /// Completes the binding by rebinding a previously registered action to this key.
    /// The handler is auto-resolved from the action registry.
    /// </summary>
    /// <param name="actionId">The action to rebind. Must have been previously registered via 
    /// <see cref="Triggers(ActionId, Action{InputBindingActionContext}, string?)"/>.</param>
    /// <exception cref="InvalidOperationException">Thrown if the action has not been registered.</exception>
    public void Triggers(ActionId actionId)
    {
        var (handler, description) = _parent.GetRegisteredAction(actionId);
        _actionId = actionId;
        Action(handler, description);
    }

    private void CommitCurrentStep()
    {
        if (_currentKey is null)
            throw new InvalidOperationException("Key must be set before Then() or Action(). Use .Key(Hex1bKey.X) before calling Then() or Action().");

        _completedSteps.Add(new KeyStep(_currentKey.Value, _currentModifiers));
        _currentModifiers = Hex1bModifiers.None;
        _currentKey = null;
    }
}
