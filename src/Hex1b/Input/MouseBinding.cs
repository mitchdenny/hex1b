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
    /// The minimum click count required to trigger this binding.
    /// 1 = single click (default), 2 = double click, 3 = triple click.
    /// A binding with ClickCount=2 will match events with ClickCount >= 2.
    /// </summary>
    public int ClickCount { get; }
    
    /// <summary>
    /// The async action to execute when the binding is triggered.
    /// All handlers are normalized to async for consistency.
    /// </summary>
    private Func<InputBindingActionContext, Task> AsyncHandler { get; }
    
    /// <summary>
    /// Human-readable description of what this binding does.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// A stable identifier for the action this binding performs.
    /// Used for programmatic rebinding via <see cref="InputBindingsBuilder.Remove(ActionId)"/>.
    /// </summary>
    /// <remarks>
    /// When supplied via the constructor (e.g., for a binding registered through
    /// <see cref="InputBindingsBuilder.Add(MouseBinding)"/>), this id supports
    /// <see cref="InputBindingsBuilder.Remove(ActionId)"/> only — it is NOT registered
    /// in the rebinding registry.
    /// </remarks>
    public ActionId? ActionId { get; }

    /// <summary>
    /// Whether this binding overrides input capture.
    /// When <c>true</c> and the owning node has captured input, the binding
    /// is checked even if the click would not normally route to the node
    /// (the click only needs to fall within the captured node's bounds).
    /// Mirrors <see cref="InputBinding.OverridesCapture"/> for keyboard
    /// bindings — useful for "while in this mode, my widget owns this
    /// mouse button" patterns (e.g., right-click commit during copy mode).
    /// </summary>
    public bool OverridesCapture { get; }

    /// <summary>
    /// Creates a mouse binding with a simple action handler (no context).
    /// </summary>
    public MouseBinding(MouseButton button, MouseAction action, Hex1bModifiers modifiers, Action handler, string? description, ActionId? actionId = null, bool overridesCapture = false)
        : this(button, action, modifiers, 1, _ => { handler(); return Task.CompletedTask; }, description, actionId, overridesCapture)
    {
        ArgumentNullException.ThrowIfNull(handler);
    }

    /// <summary>
    /// Creates a mouse binding with a simple action handler and click count (no context).
    /// </summary>
    public MouseBinding(MouseButton button, MouseAction action, Hex1bModifiers modifiers, int clickCount, Action handler, string? description, ActionId? actionId = null, bool overridesCapture = false)
        : this(button, action, modifiers, clickCount, _ => { handler(); return Task.CompletedTask; }, description, actionId, overridesCapture)
    {
        ArgumentNullException.ThrowIfNull(handler);
    }

    /// <summary>
    /// Creates a mouse binding with a synchronous context-aware handler.
    /// </summary>
    public MouseBinding(MouseButton button, MouseAction action, Hex1bModifiers modifiers, int clickCount, Action<InputBindingActionContext> handler, string? description, ActionId? actionId = null, bool overridesCapture = false)
        : this(button, action, modifiers, clickCount, ctx => { handler(ctx); return Task.CompletedTask; }, description, actionId, overridesCapture)
    {
        ArgumentNullException.ThrowIfNull(handler);
    }

    /// <summary>
    /// Creates a mouse binding with an async context-aware handler.
    /// </summary>
    public MouseBinding(MouseButton button, MouseAction action, Hex1bModifiers modifiers, int clickCount, Func<InputBindingActionContext, Task> handler, string? description, ActionId? actionId = null, bool overridesCapture = false)
    {
        Button = button;
        Action = action;
        Modifiers = modifiers;
        ClickCount = clickCount;
        AsyncHandler = handler ?? throw new ArgumentNullException(nameof(handler));
        Description = description;
        ActionId = actionId;
        OverridesCapture = overridesCapture;
    }

    /// <summary>
    /// Checks if this binding matches the given mouse event.
    /// For click count: binding matches if event's click count is >= binding's required count.
    /// </summary>
    public bool Matches(Hex1bMouseEvent mouseEvent)
    {
        return mouseEvent.Button == Button && 
               mouseEvent.Action == Action && 
               mouseEvent.Modifiers == Modifiers &&
               mouseEvent.ClickCount >= ClickCount;
    }

    /// <summary>
    /// Executes the handler for this binding.
    /// </summary>
    public Task ExecuteAsync(InputBindingActionContext context) => AsyncHandler(context);
}
