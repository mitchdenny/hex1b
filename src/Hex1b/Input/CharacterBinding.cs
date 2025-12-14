namespace Hex1b.Input;

/// <summary>
/// Represents a binding that matches character input based on a predicate.
/// Used for handling arbitrary printable character input in widgets like TextBox.
/// </summary>
public sealed class CharacterBinding
{
    /// <summary>
    /// Predicate that determines if a character should be handled.
    /// Common predicates: c => !char.IsControl(c) for printable chars.
    /// </summary>
    public Func<char, bool> Predicate { get; }

    /// <summary>
    /// The action to execute when a matching character is received.
    /// </summary>
    public Action<char> Handler { get; }

    /// <summary>
    /// Optional description for this binding (for help/documentation).
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Creates a character binding with the given predicate and handler.
    /// </summary>
    public CharacterBinding(Func<char, bool> predicate, Action<char> handler, string? description = null)
    {
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        Description = description;
    }

    /// <summary>
    /// Checks if this binding matches the given character.
    /// </summary>
    public bool Matches(char c) => Predicate(c);

    /// <summary>
    /// Executes the binding's handler with the given character.
    /// </summary>
    public void Execute(char c) => Handler(c);
}
