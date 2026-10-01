namespace Hex1b.Documents;

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
