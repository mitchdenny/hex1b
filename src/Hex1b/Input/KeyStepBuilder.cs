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
    /// Adds Ctrl modifier to the current step.
    /// Cannot be combined with Shift modifier.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if Shift modifier is already set.</exception>
    public KeyStepBuilder Ctrl()
    {
        if ((_currentModifiers & Hex1bModifiers.Shift) != 0)
            throw new InvalidOperationException("Cannot combine Ctrl and Shift modifiers. Use either Ctrl+Key or Shift+Key, but not both.");
        
        _currentModifiers |= Hex1bModifiers.Control;
        return this;
    }

    /// <summary>
    /// Adds Shift modifier to the current step.
    /// Cannot be combined with Ctrl modifier.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if Ctrl modifier is already set.</exception>
    public KeyStepBuilder Shift()
    {
        if ((_currentModifiers & Hex1bModifiers.Control) != 0)
            throw new InvalidOperationException("Cannot combine Ctrl and Shift modifiers. Use either Ctrl+Key or Shift+Key, but not both.");
        
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
        _parent.AddBinding(new InputBinding([.. _completedSteps], handler, description, _isGlobal));
    }

    /// <summary>
    /// Completes the binding with a context-aware action handler.
    /// The context provides access to app-level services like focus navigation.
    /// </summary>
    public void Action(Action<InputBindingActionContext> handler, string? description = null)
    {
        CommitCurrentStep();
        _parent.AddBinding(new InputBinding([.. _completedSteps], handler, description, _isGlobal));
    }

    /// <summary>
    /// Completes the binding with an async context-aware action handler.
    /// </summary>
    public void Action(Func<InputBindingActionContext, Task> handler, string? description = null)
    {
        CommitCurrentStep();
        _parent.AddBinding(new InputBinding([.. _completedSteps], handler, description, _isGlobal));
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
