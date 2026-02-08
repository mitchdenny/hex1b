namespace Hex1b.Documents;

/// <summary>
/// Result of applying edit operation(s) to a document.
/// </summary>
public sealed record EditResult(
    long PreviousVersion,
    long NewVersion,
    IReadOnlyList<EditOperation> Applied,
    IReadOnlyList<EditOperation> Inverse);
