namespace Hex1b.Markdown;

/// <summary>
/// A GFM table with header row, column alignments, and data rows.
/// </summary>
public sealed class TableBlock : MarkdownBlock
{
    /// <summary>Header cells (inline content per column).</summary>
    public IReadOnlyList<IReadOnlyList<MarkdownInline>> HeaderCells { get; }

    /// <summary>Column alignment specifications from the delimiter row.</summary>
    public IReadOnlyList<TableColumnAlignment> Alignments { get; }

    /// <summary>Data rows (each row is a list of inline-content cells).</summary>
    public IReadOnlyList<IReadOnlyList<IReadOnlyList<MarkdownInline>>> Rows { get; }

    public TableBlock(
        IReadOnlyList<IReadOnlyList<MarkdownInline>> headerCells,
        IReadOnlyList<TableColumnAlignment> alignments,
        IReadOnlyList<IReadOnlyList<IReadOnlyList<MarkdownInline>>> rows)
    {
        HeaderCells = headerCells;
        Alignments = alignments;
        Rows = rows;
    }
}
