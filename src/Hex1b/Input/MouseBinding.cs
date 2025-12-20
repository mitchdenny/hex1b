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
    /// Creates a mouse binding with a simple action handler (no context).
    /// </summary>
    public MouseBinding(MouseButton button, MouseAction action, Hex1bModifiers modifiers, Action handler, string? description)
        : this(button, action, modifiers, 1, _ => { handler(); return Task.CompletedTask; }, description)
    {
        ArgumentNullException.ThrowIfNull(handler);
    }

    /// <summary>
    /// Creates a mouse binding with a simple action handler and click count (no context).
    /// </summary>
    public MouseBinding(MouseButton button, MouseAction action, Hex1bModifiers modifiers, int clickCount, Action handler, string? description)
        : this(button, action, modifiers, clickCount, _ => { handler(); return Task.CompletedTask; }, description)
    {
        ArgumentNullException.ThrowIfNull(handler);
    }

    /// <summary>
    /// Creates a mouse binding with a synchronous context-aware handler.
    /// </summary>
    public MouseBinding(MouseButton button, MouseAction action, Hex1bModifiers modifiers, int clickCount, Action<InputBindingActionContext> handler, string? description)
        : this(button, action, modifiers, clickCount, ctx => { handler(ctx); return Task.CompletedTask; }, description)
    {
        ArgumentNullException.ThrowIfNull(handler);
    }

    /// <summary>
    /// Creates a mouse binding with an async context-aware handler.
    /// </summary>
    public MouseBinding(MouseButton button, MouseAction action, Hex1bModifiers modifiers, int clickCount, Func<InputBindingActionContext, Task> handler, string? description)
    {
        Button = button;
        Action = action;
        Modifiers = modifiers;
        ClickCount = clickCount;
        AsyncHandler = handler ?? throw new ArgumentNullException(nameof(handler));
        Description = description;
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
