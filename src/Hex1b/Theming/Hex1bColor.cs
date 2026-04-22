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

/// <summary>
/// Represents a color that can be used in the terminal.
/// </summary>
/// <remarks>
/// Colors preserve their original encoding so that when re-serialized
/// they emit the same SGR code the workload originally sent. This ensures
/// standard ANSI colors (e.g., SGR 34 = blue) remain palette-relative
/// instead of being converted to fixed RGB values.
/// </remarks>
public readonly struct Hex1bColor
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public bool IsDefault { get; }

    /// <summary>
    /// The color encoding kind.
    /// </summary>
    public Hex1bColorKind Kind { get; }

    /// <summary>
    /// The ANSI color index (0–7 for standard/bright, 0–255 for indexed).
    /// Only meaningful when <see cref="Kind"/> is not <see cref="Hex1bColorKind.Rgb"/>.
    /// </summary>
    public byte AnsiIndex { get; }

    private Hex1bColor(byte r, byte g, byte b, bool isDefault = false)
    {
        R = r;
        G = g;
        B = b;
        IsDefault = isDefault;
        Kind = Hex1bColorKind.Rgb;
        AnsiIndex = 0;
    }

    private Hex1bColor(byte r, byte g, byte b, Hex1bColorKind kind, byte ansiIndex)
    {
        R = r;
        G = g;
        B = b;
        IsDefault = false;
        Kind = kind;
        AnsiIndex = ansiIndex;
    }

    /// <summary>
    /// Creates a color from RGB values.
    /// </summary>
    public static Hex1bColor FromRgb(byte r, byte g, byte b) => new(r, g, b);

    /// <summary>
    /// Creates a standard ANSI color (indices 0–7, corresponding to SGR 30–37).
    /// </summary>
    /// <param name="index">Color index 0–7.</param>
    /// <param name="r">Approximate RGB red component (for rendering when palette is unavailable).</param>
    /// <param name="g">Approximate RGB green component.</param>
    /// <param name="b">Approximate RGB blue component.</param>
    public static Hex1bColor FromStandard(byte index, byte r, byte g, byte b) =>
        new(r, g, b, Hex1bColorKind.Standard, index);

    /// <summary>
    /// Creates a bright ANSI color (indices 0–7, corresponding to SGR 90–97).
    /// </summary>
    /// <param name="index">Color index 0–7.</param>
    /// <param name="r">Approximate RGB red component.</param>
    /// <param name="g">Approximate RGB green component.</param>
    /// <param name="b">Approximate RGB blue component.</param>
    public static Hex1bColor FromBright(byte index, byte r, byte g, byte b) =>
        new(r, g, b, Hex1bColorKind.Bright, index);

    /// <summary>
    /// Creates a 256-color palette color (SGR 38;5;N).
    /// </summary>
    /// <param name="index">Color index 0–255.</param>
    /// <param name="r">Approximate RGB red component.</param>
    /// <param name="g">Approximate RGB green component.</param>
    /// <param name="b">Approximate RGB blue component.</param>
    public static Hex1bColor FromIndexed(byte index, byte r, byte g, byte b) =>
        new(r, g, b, Hex1bColorKind.Indexed, index);

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
    public string ToForegroundAnsi() => Kind switch
    {
        _ when IsDefault => "\x1b[39m",
        Hex1bColorKind.Standard => $"\x1b[{30 + AnsiIndex}m",
        Hex1bColorKind.Bright => $"\x1b[{90 + AnsiIndex}m",
        Hex1bColorKind.Indexed => $"\x1b[38;5;{AnsiIndex}m",
        _ => $"\x1b[38;2;{R};{G};{B}m"
    };

    /// <summary>
    /// Gets the ANSI escape code for setting this as the background color.
    /// </summary>
    public string ToBackgroundAnsi() => Kind switch
    {
        _ when IsDefault => "\x1b[49m",
        Hex1bColorKind.Standard => $"\x1b[{40 + AnsiIndex}m",
        Hex1bColorKind.Bright => $"\x1b[{100 + AnsiIndex}m",
        Hex1bColorKind.Indexed => $"\x1b[48;5;{AnsiIndex}m",
        _ => $"\x1b[48;2;{R};{G};{B}m"
    };

    /// <summary>
    /// Gets the ANSI escape code for setting this as the underline color (SGR 58).
    /// </summary>
    public string ToUnderlineColorAnsi() => IsDefault ? "\x1b[59m" : $"\x1b[58;2;{R};{G};{B}m";
}
