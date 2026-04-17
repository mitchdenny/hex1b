namespace Hex1b.Theming;

/// <summary>
/// Represents a color that can be used in the terminal.
/// Supports RGB, indexed (palette 0-255), and default terminal colors.
/// </summary>
/// <remarks>
/// <para>
/// Indexed colors (0-15) are re-emitted as standard SGR codes (e.g., <c>\e[32m</c> for green),
/// allowing the parent terminal to apply its own color scheme. Indices 16-255 use the
/// <c>38;5;N</c> / <c>48;5;N</c> form. RGB colors always use <c>38;2;R;G;Bm</c>.
/// </para>
/// <para>
/// The <see cref="R"/>, <see cref="G"/>, <see cref="B"/> properties return resolved RGB values
/// for all color modes, including indexed colors (using the standard VGA/xterm palette as fallback).
/// </para>
/// </remarks>
public readonly struct Hex1bColor
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public bool IsDefault { get; }

    private readonly short _index; // -1 = not indexed; 0-255 = palette index

    /// <summary>
    /// Gets whether this color is an indexed (palette) color rather than an explicit RGB color.
    /// </summary>
    public bool IsIndexed => _index >= 0;

    /// <summary>
    /// Gets the palette index if <see cref="IsIndexed"/> is true, otherwise -1.
    /// </summary>
    public short Index => _index;

    private Hex1bColor(byte r, byte g, byte b, bool isDefault = false)
    {
        R = r;
        G = g;
        B = b;
        IsDefault = isDefault;
        _index = -1;
    }

    private Hex1bColor(byte index, byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
        IsDefault = false;
        _index = index;
    }

    /// <summary>
    /// Creates a color from RGB values.
    /// </summary>
    public static Hex1bColor FromRgb(byte r, byte g, byte b) => new(r, g, b);

    /// <summary>
    /// Creates an indexed (palette) color. The RGB fallback values are resolved from the
    /// standard xterm 256-color palette for use in non-ANSI contexts (SVG, HTML, etc.).
    /// When rendered as ANSI, the original index is preserved so the parent terminal can
    /// apply its own color scheme.
    /// </summary>
    /// <param name="index">The palette index (0-255).</param>
    public static Hex1bColor FromIndex(byte index)
    {
        var (r, g, b) = ResolveIndexRgb(index);
        return new Hex1bColor(index, r, g, b);
    }

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
    /// Indexed colors 0-7 emit <c>\e[30-37m</c>, 8-15 emit <c>\e[90-97m</c>,
    /// 16-255 emit <c>\e[38;5;Nm</c>, and RGB colors emit <c>\e[38;2;R;G;Bm</c>.
    /// </summary>
    public string ToForegroundAnsi()
    {
        if (IsDefault) return "\x1b[39m";
        if (_index >= 0)
        {
            if (_index < 8) return $"\x1b[{30 + _index}m";
            if (_index < 16) return $"\x1b[{90 + _index - 8}m";
            return $"\x1b[38;5;{_index}m";
        }
        return $"\x1b[38;2;{R};{G};{B}m";
    }

    /// <summary>
    /// Gets the ANSI escape code for setting this as the background color.
    /// Indexed colors 0-7 emit <c>\e[40-47m</c>, 8-15 emit <c>\e[100-107m</c>,
    /// 16-255 emit <c>\e[48;5;Nm</c>, and RGB colors emit <c>\e[48;2;R;G;Bm</c>.
    /// </summary>
    public string ToBackgroundAnsi()
    {
        if (IsDefault) return "\x1b[49m";
        if (_index >= 0)
        {
            if (_index < 8) return $"\x1b[{40 + _index}m";
            if (_index < 16) return $"\x1b[{100 + _index - 8}m";
            return $"\x1b[48;5;{_index}m";
        }
        return $"\x1b[48;2;{R};{G};{B}m";
    }

    /// <summary>
    /// Gets the ANSI escape code for setting this as the underline color (SGR 58).
    /// </summary>
    public string ToUnderlineColorAnsi()
    {
        if (IsDefault) return "\x1b[59m";
        if (_index >= 0) return $"\x1b[58;5;{_index}m";
        return $"\x1b[58;2;{R};{G};{B}m";
    }

    /// <summary>
    /// Resolves an xterm 256-color palette index to its standard RGB values.
    /// </summary>
    private static (byte R, byte G, byte B) ResolveIndexRgb(int index) => index switch
    {
        // Standard colors (0-7)
        0 => (0, 0, 0),
        1 => (128, 0, 0),
        2 => (0, 128, 0),
        3 => (128, 128, 0),
        4 => (0, 0, 128),
        5 => (128, 0, 128),
        6 => (0, 128, 128),
        7 => (192, 192, 192),
        // Bright colors (8-15)
        8 => (128, 128, 128),
        9 => (255, 0, 0),
        10 => (0, 255, 0),
        11 => (255, 255, 0),
        12 => (0, 0, 255),
        13 => (255, 0, 255),
        14 => (0, 255, 255),
        15 => (255, 255, 255),
        // 6x6x6 color cube (16-231)
        >= 16 and <= 231 => ResolveColorCube(index - 16),
        // Grayscale ramp (232-255)
        >= 232 and <= 255 => ResolveGrayscale(index - 232),
        _ => (0, 0, 0)
    };

    private static (byte R, byte G, byte B) ResolveColorCube(int offset)
    {
        int b = offset % 6;
        int g = (offset / 6) % 6;
        int r = offset / 36;
        return ((byte)(r == 0 ? 0 : 55 + r * 40),
                (byte)(g == 0 ? 0 : 55 + g * 40),
                (byte)(b == 0 ? 0 : 55 + b * 40));
    }

    private static (byte R, byte G, byte B) ResolveGrayscale(int offset)
    {
        byte v = (byte)(8 + offset * 10);
        return (v, v, v);
    }
}
