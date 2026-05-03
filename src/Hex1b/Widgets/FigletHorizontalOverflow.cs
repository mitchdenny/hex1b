namespace Hex1b.Widgets;

/// <summary>
/// Specifies how a <see cref="FigletTextWidget"/> handles content that exceeds the available
/// horizontal space.
/// </summary>
/// <remarks>
/// <para>
/// FIGcharacters are typically much wider than ordinary text, so even short input strings can
/// overflow narrow containers. Callers can either let the parent clip the rendered output or
/// opt in to word-wrapping that produces multiple FIGlet rows.
/// </para>
/// </remarks>
public enum FigletHorizontalOverflow
{
    /// <summary>
    /// Render the text at its natural unwrapped width. Content that exceeds the parent's
    /// width is clipped at the right edge by the surrounding layout container.
    /// </summary>
    Clip,

    /// <summary>
    /// Word-wrap input on whitespace boundaries so each rendered FIGlet block fits within the
    /// parent's width. Single words wider than the available width are emitted on their own row
    /// at natural width and fall back to clipping at the right edge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The wrap algorithm is a greedy fit: tokens are accumulated until adding the next would
    /// exceed the available width, at which point the accumulator is emitted as a complete
    /// FIGlet row and a new accumulator begins.
    /// </para>
    /// <para>
    /// This algorithm is intentionally <em>not</em> byte-compatible with reference FIGlet's
    /// <c>-w</c> CLI wrapping. The FIGfont 2.0 specification does not define line wrapping, so
    /// implementations differ; in particular, our renderer never breaks inside a word, never
    /// pads wrapped rows for justification, and collapses whitespace runs to a single space.
    /// </para>
    /// </remarks>
    Wrap,
}
