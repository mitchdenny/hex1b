namespace Hex1b;

/// <summary>
/// Text styling attributes that can be applied to terminal cells.
/// These correspond to SGR (Select Graphic Rendition) parameters.
/// Multiple attributes can be combined using bitwise OR.
/// </summary>
[Flags]
public enum CellAttributes : ushort
{
    /// <summary>No attributes applied.</summary>
    None = 0,

    /// <summary>Bold or increased intensity (SGR 1).</summary>
    Bold = 1 << 0,

    /// <summary>Dim or decreased intensity (SGR 2).</summary>
    Dim = 1 << 1,

    /// <summary>Italic text (SGR 3).</summary>
    Italic = 1 << 2,

    /// <summary>Underlined text (SGR 4).</summary>
    Underline = 1 << 3,

    /// <summary>Blinking text (SGR 5).</summary>
    Blink = 1 << 4,

    /// <summary>Reverse video / inverse (SGR 7).</summary>
    Reverse = 1 << 5,

    /// <summary>Hidden / invisible text (SGR 8).</summary>
    Hidden = 1 << 6,

    /// <summary>Strikethrough / crossed out text (SGR 9).</summary>
    Strikethrough = 1 << 7,

    /// <summary>Overline (SGR 53).</summary>
    Overline = 1 << 8,
}
