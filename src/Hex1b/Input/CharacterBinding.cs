namespace Hex1b.Input;

/// <summary>
/// Represents a binding that matches text input based on a predicate.
/// Used for handling arbitrary printable text input in widgets like TextBox.
/// Supports both single characters and multi-char sequences like emojis.
/// </summary>
public sealed class CharacterBinding
{
    /// <summary>
    /// Predicate that determines if a text input should be handled.
    /// Common predicates: text => text.Length > 0 &amp;&amp; !char.IsControl(text[0]) for printable text.
    /// </summary>
    public Func<string, bool> Predicate { get; }

    /// <summary>
    /// The synchronous action to execute when matching text input is received.
    /// </summary>
    public Action<string>? Handler { get; }

    /// <summary>
    /// The async action to execute when matching text input is received.
    /// </summary>
    public Func<string, InputBindingActionContext, Task>? AsyncHandler { get; }

    /// <summary>
    /// Optional description for this binding (for help/documentation).
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// A stable identifier for the action this binding performs.
    /// Used for programmatic rebinding via <see cref="InputBindingsBuilder.Remove(ActionId)"/>.
    /// </summary>
    /// <remarks>
    /// When supplied via the constructor (e.g., for a binding registered through
    /// <see cref="InputBindingsBuilder.Add(CharacterBinding)"/>), this id supports
    /// <see cref="InputBindingsBuilder.Remove(ActionId)"/> only — it is NOT registered
    /// in the rebinding registry.
    /// </remarks>
    public ActionId? ActionId { get; }

    /// <summary>
    /// Creates a text binding with the given predicate and synchronous handler.
    /// </summary>
    public CharacterBinding(Func<string, bool> predicate, Action<string> handler, string? description = null, ActionId? actionId = null)
    {
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        Description = description;
        ActionId = actionId;
    }

    /// <summary>
    /// Creates a text binding with the given predicate and async handler.
    /// </summary>
    public CharacterBinding(Func<string, bool> predicate, Func<string, InputBindingActionContext, Task> handler, string? description = null, ActionId? actionId = null)
    {
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        AsyncHandler = handler ?? throw new ArgumentNullException(nameof(handler));
        Description = description;
        ActionId = actionId;
    }

    /// <summary>
    /// Checks if this binding matches the given text.
    /// </summary>
    public bool Matches(string text) => Predicate(text);

    /// <summary>
    /// Executes the binding's handler with the given text.
    /// </summary>
    public void Execute(string text) => Handler?.Invoke(text);

    /// <summary>
    /// Executes the binding's async handler with the given text and context.
    /// </summary>
    public async Task ExecuteAsync(string text, InputBindingActionContext context)
    {
        if (AsyncHandler != null)
        {
            await AsyncHandler(text, context);
        }
        else
        {
            Handler?.Invoke(text);
        }
    }
}
