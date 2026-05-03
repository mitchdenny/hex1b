namespace Hex1b.Widgets;

/// <summary>
/// Internal DTO holding the parsed FIGfont header (the <c>flf2a$</c> line). Kept private to the
/// parser/loader path; <see cref="FigletFont"/> projects the salient values into virtual properties
/// and the resolved <see cref="FigletLayoutInfo"/>.
/// </summary>
internal sealed record FigletFontHeader
{
    public required char Hardblank { get; init; }
    public required int Height { get; init; }
    public required int Baseline { get; init; }
    public required int MaxLength { get; init; }
    public required int OldLayout { get; init; }
    public required int CommentLines { get; init; }

    /// <summary>0 = LTR, 1 = RTL. Ignored in v1 (the renderer is always LTR) but parsed for forward compatibility.</summary>
    public int? PrintDirection { get; init; }

    /// <summary>The optional <c>full_layout</c> field (legal range 0..32767). When present it overrides <see cref="OldLayout"/>.</summary>
    public int? FullLayout { get; init; }

    /// <summary>The optional advisory count of code-tagged FIGcharacters. Parsed but not enforced.</summary>
    public int? CodetagCount { get; init; }
}
