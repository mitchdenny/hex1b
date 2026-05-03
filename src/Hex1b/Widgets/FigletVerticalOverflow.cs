namespace Hex1b.Widgets;

/// <summary>
/// Specifies how a <see cref="FigletTextWidget"/> handles content that exceeds the available
/// vertical space.
/// </summary>
/// <remarks>
/// <para>
/// A FIGfont row consists of <see cref="FigletFont.Height"/> sub-character rows. When the
/// parent container is shorter than the rendered height, partial rows of glyphs may appear at
/// the bottom and look broken. <see cref="Truncate"/> drops those partial rows entirely.
/// </para>
/// </remarks>
public enum FigletVerticalOverflow
{
    /// <summary>
    /// Render the text at its natural full height. Content that exceeds the parent's height
    /// is clipped at the bottom by the surrounding layout container, which may leave a partial
    /// FIGlet row visible.
    /// </summary>
    Clip,

    /// <summary>
    /// Drop entire FIGlet rows (each <see cref="FigletFont.Height"/> sub-character rows tall)
    /// that do not fully fit. Never emits a partial row of glyphs at the bottom.
    /// </summary>
    Truncate,
}
