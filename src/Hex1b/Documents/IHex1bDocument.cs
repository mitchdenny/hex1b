namespace Hex1b.Documents;

/// <summary>
/// Interface for a document that can be read, edited, and observed.
/// The document model is UI-independent and collaboration-ready.
/// Supports both character-level and byte-level access.
/// </summary>
public interface IHex1bDocument
{
    /// <summary>Total character count in the document.</summary>
    int Length { get; }

    /// <summary>Total byte count in the document's underlying byte buffer.</summary>
    int ByteCount { get; }

    /// <summary>Number of lines (at least 1, even for empty documents).</summary>
    int LineCount { get; }

    /// <summary>Monotonic version counter, incremented on every edit.</summary>
    long Version { get; }

    /// <summary>Get the full document text (UTF-8 decoded, with U+FFFD for invalid sequences).</summary>
    string GetText();

    /// <summary>Get text within a character range.</summary>
    string GetText(DocumentRange range);

    /// <summary>Get the full document content as raw bytes.</summary>
    ReadOnlyMemory<byte> GetBytes();

    /// <summary>Get a slice of the document's byte content.</summary>
    ReadOnlyMemory<byte> GetBytes(int byteOffset, int count);

    /// <summary>Get the text of a single line (1-based).</summary>
    string GetLineText(int line);

    /// <summary>Get the length of a single line (1-based), excluding line ending.</summary>
    int GetLineLength(int line);

    /// <summary>Convert an absolute character offset to a line/column position.</summary>
    DocumentPosition OffsetToPosition(DocumentOffset offset);

    /// <summary>Convert a line/column position to an absolute character offset.</summary>
    DocumentOffset PositionToOffset(DocumentPosition position);

    /// <summary>Apply a single character-level edit operation.</summary>
    EditResult Apply(EditOperation operation, string? source = null);

    /// <summary>Apply multiple character-level edit operations atomically.</summary>
    EditResult Apply(IReadOnlyList<EditOperation> operations, string? source = null);

    /// <summary>Apply a byte-level edit operation directly on the byte buffer.</summary>
    EditResult ApplyBytes(ByteEditOperation operation, string? source = null);

    /// <summary>Fired after any edit is applied (character or byte level).</summary>
    event EventHandler<DocumentChangedEventArgs>? Changed;

    /// <summary>
    /// Returns a diagnostic snapshot of the document's internal structure.
    /// Returns null if the implementation does not support diagnostics.
    /// </summary>
    DocumentDiagnosticInfo? GetDiagnosticInfo() => null;
}
