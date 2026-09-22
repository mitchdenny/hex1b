namespace Hex1b.Theming;

/// <summary>
/// The kind of ANSI color encoding.
/// </summary>
public enum Hex1bColorKind : byte
{
    /// <summary>24-bit RGB color (SGR 38;2;R;G;B).</summary>
    Rgb,

    /// <summary>Standard ANSI color index 0–7 (SGR 30–37 / 40–47).</summary>
    Standard,

    /// <summary>Bright ANSI color index 0–7 (SGR 90–97 / 100–107).</summary>
    Bright,

    /// <summary>256-color palette index (SGR 38;5;N / 48;5;N).</summary>
    Indexed
}
