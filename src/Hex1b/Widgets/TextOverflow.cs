namespace Hex1b.Widgets;

/// <summary>
/// Specifies how a <see cref="TextBlockWidget"/> handles text that exceeds its available width.
/// </summary>
/// <remarks>
/// <para>
/// This enum controls the layout and rendering behavior when text content is wider than
/// the constraints allow. The choice affects both the measured size of the widget and
/// how the text is displayed.
/// </para>
/// </remarks>
/// <seealso cref="TextBlockWidget"/>
public enum TextOverflow
{
    /// <summary>
    /// Text extends beyond its allocated bounds without modification.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the default behavior for backward compatibility. The text is rendered
    /// at its full width; clipping is handled by a parent <see cref="LayoutWidget"/>
    /// if one is present with <see cref="ClipMode.Clip"/> enabled.
    /// </para>
    /// <para>
    /// Use this when you want parent containers to control visibility of overflowing content.
    /// </para>
    /// </remarks>
    Overflow,
    
    /// <summary>
    /// Text wraps to the next line when it exceeds the available width.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wrapping occurs at word boundaries when possible. Words that are wider than
    /// the available width are broken mid-word. This affects the measured height
    /// of the widget, which increases based on the number of wrapped lines.
    /// </para>
    /// <para>
    /// Use this for paragraph text or content where all text should be visible.
    /// </para>
    /// </remarks>
    Wrap,
    
    /// <summary>
    /// Text is truncated with an ellipsis ("...") when it exceeds the available width.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The text is shortened to fit within the available width, with "..." appended
    /// to indicate truncation. The measured height remains 1 line. If the available
    /// width is less than 4 characters, text may be truncated without an ellipsis.
    /// </para>
    /// <para>
    /// Use this for single-line displays like list items or headers where space is limited.
    /// </para>
    /// </remarks>
    Ellipsis,
}
