namespace Hex1b.Theming;

/// <summary>
/// Represents a color that can be used in the terminal.
/// </summary>
public readonly struct Hex1bColor
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public bool IsDefault { get; }

    private Hex1bColor(byte r, byte g, byte b, bool isDefault = false)
    {
        R = r;
        G = g;
        B = b;
        IsDefault = isDefault;
    }

    /// <summary>
    /// Creates a color from RGB values.
    /// </summary>
    public static Hex1bColor FromRgb(byte r, byte g, byte b) => new(r, g, b);

    /// <summary>
    /// The default terminal foreground/background color.
    /// </summary>
    public static Hex1bColor Default => new(0, 0, 0, isDefault: true);

    // Common colors
    public static Hex1bColor Black => FromRgb(0, 0, 0);
    public static Hex1bColor White => FromRgb(255, 255, 255);
    public static Hex1bColor Red => FromRgb(255, 0, 0);
    public static Hex1bColor Green => FromRgb(0, 255, 0);
    public static Hex1bColor Blue => FromRgb(0, 0, 255);
    public static Hex1bColor Yellow => FromRgb(255, 255, 0);
    public static Hex1bColor Cyan => FromRgb(0, 255, 255);
    public static Hex1bColor Magenta => FromRgb(255, 0, 255);
    public static Hex1bColor Gray => FromRgb(128, 128, 128);
    public static Hex1bColor DarkGray => FromRgb(64, 64, 64);
    public static Hex1bColor LightGray => FromRgb(192, 192, 192);

    /// <summary>
    /// Gets the ANSI escape code for setting this as the foreground color.
    /// </summary>
    public string ToForegroundAnsi() => IsDefault ? "\x1b[39m" : $"\x1b[38;2;{R};{G};{B}m";

    /// <summary>
    /// Gets the ANSI escape code for setting this as the background color.
    /// </summary>
    public string ToBackgroundAnsi() => IsDefault ? "\x1b[49m" : $"\x1b[48;2;{R};{G};{B}m";
}
