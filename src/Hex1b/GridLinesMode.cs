namespace Hex1b;

/// <summary>
/// Controls whether gridlines (box-drawing borders) are rendered between cells in a <see cref="Widgets.GridWidget"/>.
/// </summary>
public enum GridLinesMode
{
    /// <summary>
    /// No gridlines are rendered. This is the default for backward compatibility.
    /// </summary>
    None = 0,

    /// <summary>
    /// Gridlines are rendered around all cells (outer border + inner dividers).
    /// </summary>
    All,

    /// <summary>
    /// Only a horizontal separator is rendered below the first row (header separator).
    /// No outer border is drawn.
    /// </summary>
    HeaderSeparator,
}
