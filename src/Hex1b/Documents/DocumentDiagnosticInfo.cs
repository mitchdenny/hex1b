namespace Hex1b.Documents;

/// <summary>
/// Diagnostic snapshot of a document's internal piece table state.
/// </summary>
public sealed class DocumentDiagnosticInfo
{
    /// <summary>Monotonic version counter at time of snapshot.</summary>
    public long Version { get; init; }

    /// <summary>Total character count.</summary>
    public int CharCount { get; init; }

    /// <summary>Total byte count across all pieces.</summary>
    public int ByteCount { get; init; }

    /// <summary>Number of lines.</summary>
    public int LineCount { get; init; }

    /// <summary>Size of the original (immutable) buffer in bytes.</summary>
    public int OriginalBufferSize { get; init; }

    /// <summary>Size of the add (append-only) buffer in bytes.</summary>
    public int AddBufferSize { get; init; }

    /// <summary>Ordered list of pieces in the piece table (in-order traversal).</summary>
    public IReadOnlyList<PieceDiagnosticInfo> Pieces { get; init; } = [];

    /// <summary>Number of pieces in the tree.</summary>
    public int PieceCount { get; init; }

    /// <summary>Root of the red-black piece tree for structure visualization.</summary>
    public PieceTreeDiagnosticNode? TreeRoot { get; init; }
}

/// <summary>
/// Diagnostic snapshot of a single piece in the piece table.
/// </summary>
public sealed class PieceDiagnosticInfo
{
    /// <summary>Zero-based index of this piece.</summary>
    public int Index { get; init; }

    /// <summary>Which buffer this piece references: "Original" or "Added".</summary>
    public string Source { get; init; } = "";

    /// <summary>Start offset within the source buffer.</summary>
    public int Start { get; init; }

    /// <summary>Length in bytes.</summary>
    public int Length { get; init; }

    /// <summary>The raw bytes of this piece (truncated to first 64 bytes for large pieces).</summary>
    public byte[] PreviewBytes { get; init; } = [];

    /// <summary>UTF-8 decoded preview text (may contain U+FFFD for invalid sequences).</summary>
    public string PreviewText { get; init; } = "";
}
