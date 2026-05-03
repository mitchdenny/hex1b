namespace Hex1b.Widgets;

/// <summary>
/// Specifies the layout mode used when composing FIGcharacters in a <see cref="FigletTextWidget"/>.
/// </summary>
/// <remarks>
/// <para>
/// FIGfonts are rendered by placing one glyph after another and choosing how aggressively to
/// overlap adjacent glyphs:
/// <list type="bullet">
/// <item>
///   <description>
///   <see cref="FullWidth"/> — no overlap; glyphs are concatenated at their natural width.
///   </description>
/// </item>
/// <item>
///   <description>
///   <see cref="Fitted"/> — glyphs slide together until any non-space cells touch (kerning).
///   </description>
/// </item>
/// <item>
///   <description>
///   <see cref="Smushed"/> — glyphs overlap by the maximum number of columns where every row
///   pair can be merged using the active smushing rules.
///   </description>
/// </item>
/// <item>
///   <description>
///   <see cref="Default"/> — defer to the layout mode declared by the font itself.
///   </description>
/// </item>
/// </list>
/// </para>
/// <para>
/// The same enum is used for both horizontal and vertical layout. Vertical layout only applies
/// when the input text contains explicit newlines or when
/// <see cref="FigletHorizontalOverflow.Wrap"/> produces multiple FIGlet rows.
/// </para>
/// </remarks>
public enum FigletLayoutMode
{
    /// <summary>Use the layout mode declared by the font's header.</summary>
    Default,

    /// <summary>Concatenate glyphs at their natural width with no overlap.</summary>
    FullWidth,

    /// <summary>Slide glyphs together until any non-space cells touch (kerning).</summary>
    Fitted,

    /// <summary>Overlap glyphs by the maximum legal amount under the font's smushing rules.</summary>
    Smushed,
}
