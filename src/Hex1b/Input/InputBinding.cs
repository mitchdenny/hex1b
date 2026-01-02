namespace Hex1b.Input;

using Hex1b.Nodes;

/// <summary>
/// Represents a key binding that matches a sequence of key steps and executes an action.
/// Supports both single-key bindings and multi-step chords.
/// </summary>
public sealed class InputBinding
{
    /// <summary>
    /// The sequence of key steps that trigger this binding.
    /// Single-key bindings have exactly one step; chords have multiple steps.
    /// </summary>
    public IReadOnlyList<KeyStep> Steps { get; }

    /// <summary>
    /// The action to execute when the binding matches.
    /// All handlers are normalized to async for consistency.
    /// </summary>
    private Func<InputBindingActionContext, Task> Handler { get; }

    /// <summary>
    /// Optional description for this binding (for help/documentation).
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Whether this binding is global (evaluated regardless of focus).
    /// Global bindings are checked before focus-based routing.
    /// </summary>
    public bool IsGlobal { get; }

    /// <summary>
    /// The node that owns this binding (for conflict detection and debugging).
    /// </summary>
    public Hex1bNode? OwnerNode { get; internal set; }

    /// <summary>
    /// Creates an input binding with a simple action handler (no context).
    /// </summary>
    public InputBinding(IReadOnlyList<KeyStep> steps, Action action, string? description = null, bool isGlobal = false)
        : this(steps, _ => { action(); return Task.CompletedTask; }, description, isGlobal)
    {
        ArgumentNullException.ThrowIfNull(action);
    }

    /// <summary>
    /// Creates an input binding with a synchronous context-aware action handler.
    /// </summary>
    public InputBinding(IReadOnlyList<KeyStep> steps, Action<InputBindingActionContext> action, string? description = null, bool isGlobal = false)
        : this(steps, ctx => { action(ctx); return Task.CompletedTask; }, description, isGlobal)
    {
        ArgumentNullException.ThrowIfNull(action);
    }

    /// <summary>
    /// Creates an input binding with an async context-aware action handler.
    /// </summary>
    public InputBinding(IReadOnlyList<KeyStep> steps, Func<InputBindingActionContext, Task> handler, string? description = null, bool isGlobal = false)
    {
        if (steps.Count == 0)
            throw new ArgumentException("At least one key step is required.", nameof(steps));
        
        Steps = steps;
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        Description = description;
        IsGlobal = isGlobal;
    }

    /// <summary>
    /// Executes the binding's handler with the given context.
    /// </summary>
    public Task ExecuteAsync(InputBindingActionContext context) => Handler(context);

    /// <summary>
    /// Gets the first key step of this binding (for trie insertion).
    /// </summary>
    public KeyStep FirstStep => Steps[0];

    /// <summary>
    /// Gets whether this is a single-key binding (vs a chord).
    /// </summary>
    public bool IsSingleKey => Steps.Count == 1;

    public override string ToString()
        => string.Join(" → ", Steps.Select(s => s.ToString()));

    // Legacy factory methods for backward compatibility during migration
    
    /// <summary>
    /// Creates a binding for a plain key (no modifiers).
    /// </summary>
    [Obsolete("Use InputBindingsBuilder fluent API instead.")]
    public static InputBinding Plain(Hex1bKey key, Action handler, string? description = null)
        => new([new KeyStep(key, Hex1bModifiers.None)], handler, description);

    /// <summary>
    /// Creates a binding for Ctrl+Key.
    /// </summary>
    [Obsolete("Use InputBindingsBuilder fluent API instead.")]
    public static InputBinding Ctrl(Hex1bKey key, Action handler, string? description = null)
        => new([new KeyStep(key, Hex1bModifiers.Control)], handler, description);

    /// <summary>
    /// Creates a binding for Alt+Key.
    /// </summary>
    [Obsolete("Use InputBindingsBuilder fluent API instead.")]
    public static InputBinding Alt(Hex1bKey key, Action handler, string? description = null)
        => new([new KeyStep(key, Hex1bModifiers.Alt)], handler, description);

    /// <summary>
    /// Creates a binding for Shift+Key.
    /// </summary>
    [Obsolete("Use InputBindingsBuilder fluent API instead.")]
    public static InputBinding Shift(Hex1bKey key, Action handler, string? description = null)
        => new([new KeyStep(key, Hex1bModifiers.Shift)], handler, description);

    /// <summary>
    /// Creates a binding for Ctrl+Shift+Key.
    /// </summary>
    [Obsolete("Use InputBindingsBuilder fluent API instead.")]
    public static InputBinding CtrlShift(Hex1bKey key, Action handler, string? description = null)
        => new([new KeyStep(key, Hex1bModifiers.Control | Hex1bModifiers.Shift)], handler, description);
}
