namespace Hex1b;

/// <summary>
/// Describes the DEC width and height of a physical terminal row.
/// Double-height halves are independent rows, both with double-width characters.
/// </summary>
public enum LineRendition
{
    /// <summary>Normal width and height.</summary>
    SingleWidth,
    /// <summary>Double width, normal height.</summary>
    DoubleWidth,
    /// <summary>The top half of double-width, double-height characters.</summary>
    DoubleHeightTop,
    /// <summary>The bottom half of double-width, double-height characters.</summary>
    DoubleHeightBottom
}
