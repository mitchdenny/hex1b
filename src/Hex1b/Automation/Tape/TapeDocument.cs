namespace Hex1b.Automation;

/// <summary>Contains an editable sequence of Tape instructions.</summary>
/// <remarks>Commands can be added, removed, reordered, or replaced before playback. Preparation snapshots the sequence for each invocation.</remarks>
public sealed class TapeDocument
{
    /// <summary>Creates an empty document.</summary>
    public TapeDocument() : this([]) { }

    /// <summary>Creates a document by copying the supplied instructions.</summary>
    /// <param name="commands">The instructions in source order.</param>
    /// <param name="sourceName">The optional source label.</param>
    public TapeDocument(IEnumerable<TapeCommand> commands, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var copy = commands.ToList();
        if (copy.Any(command => command is null))
            throw new ArgumentException("Commands cannot contain null entries.", nameof(commands));
        Commands = copy;
        SourceName = sourceName;
    }

    /// <summary>Gets the caller-supplied source label.</summary>
    public string? SourceName { get; }

    /// <summary>Gets the mutable list of instructions in execution order.</summary>
    /// <remarks>Entries must not be null. The list is not thread-safe; do not modify it concurrently with the initial playback snapshot.</remarks>
    public IList<TapeCommand> Commands { get; }

    internal TapeParser Parser { get; init; } = new();
}
