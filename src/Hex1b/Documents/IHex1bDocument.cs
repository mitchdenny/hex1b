namespace Hex1b.Documents;

/// <summary>
/// Interface for a document that can be read, edited, and observed.
/// The document model is UI-independent and collaboration-ready.
/// </summary>
public interface IHex1bDocument
{
    /// <summary>Total character count in the document.</summary>
    int Length { get; }

    /// <summary>Number of lines (at least 1, even for empty documents).</summary>
    int LineCount { get; }

    /// <summary>Monotonic version counter, incremented on every edit.</summary>
    long Version { get; }

    /// <summary>Get the full document text.</summary>
    string GetText();

    /// <summary>Get text within a range.</summary>
    string GetText(DocumentRange range);

    /// <summary>Get the text of a single line (1-based).</summary>
    string GetLineText(int line);

    /// <summary>Get the length of a single line (1-based), excluding line ending.</summary>
    int GetLineLength(int line);

    /// <summary>Convert an absolute offset to a line/column position.</summary>
    DocumentPosition OffsetToPosition(DocumentOffset offset);

    /// <summary>Convert a line/column position to an absolute offset.</summary>
    DocumentOffset PositionToOffset(DocumentPosition position);

    /// <summary>Apply a single edit operation.</summary>
    EditResult Apply(EditOperation operation, string? source = null);

    /// <summary>Apply multiple edit operations atomically.</summary>
    EditResult Apply(IReadOnlyList<EditOperation> operations, string? source = null);

    /// <summary>Fired after any edit is applied.</summary>
    event EventHandler<DocumentChangedEventArgs>? Changed;
}
