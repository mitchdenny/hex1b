namespace Hex1b.Documents;

/// <summary>
/// Event args for document change notifications.
/// </summary>
public sealed class DocumentChangedEventArgs : EventArgs
{
    public long Version { get; }
    public long PreviousVersion { get; }
    public IReadOnlyList<EditOperation> Operations { get; }
    public IReadOnlyList<EditOperation> Inverse { get; }

    /// <summary>
    /// Optional tag identifying the source of the edit (e.g., "local", "remote-agent").
    /// Useful for filtering own changes in collaborative scenarios.
    /// </summary>
    public string? Source { get; }

    public DocumentChangedEventArgs(
        long version,
        long previousVersion,
        IReadOnlyList<EditOperation> operations,
        IReadOnlyList<EditOperation> inverse,
        string? source = null)
    {
        Version = version;
        PreviousVersion = previousVersion;
        Operations = operations;
        Inverse = inverse;
        Source = source;
    }
}
