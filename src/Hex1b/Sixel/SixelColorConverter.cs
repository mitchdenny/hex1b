using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Deterministic DEC color conversions for Sixel color registers.
/// </summary>
/// <remarks>
/// <para>
/// All conversions use integer arithmetic so results are reproducible and
/// testable. Percentages are clamped to the DEC 0-100 domain and converted to
/// 8-bit components with nearest rounding (ties away from zero).
/// </para>
/// <para>
/// DEC HLS is not CSS HLS: hue 0 is blue, 120 is red, and 240 is green. Hue
/// wraps around the wheel rather than clamping.
/// </para>
/// </remarks>
internal static class SixelColorConverter
{
    private const int Scale = 10_000;

    /// <summary>
    /// Converts a DEC 0-100 percentage to an 8-bit component with nearest rounding.
    /// </summary>
    public static byte PercentToComponent(int percent)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        return (byte)(((clamped * 255) + 50) / 100);
    }

    /// <summary>
    /// Converts DEC RGB percentages to an opaque color.
    /// </summary>
    public static Rgba32 FromRgbPercent(int red, int green, int blue) => new(
        PercentToComponent(red),
        PercentToComponent(green),
        PercentToComponent(blue),
        255);

    /// <summary>
    /// Converts DEC HLS coordinates to an opaque color using the DEC hue wheel.
    /// </summary>
    /// <param name="hue">Hue in degrees; blue at 0, red at 120, green at 240. Wraps.</param>
    /// <param name="lightness">Lightness percentage, clamped to 0-100.</param>
    /// <param name="saturation">Saturation percentage, clamped to 0-100.</param>
    public static Rgba32 FromHls(int hue, int lightness, int saturation)
    {
        var lum = Math.Clamp(lightness, 0, 100) * (Scale / 100);
        var sat = Math.Clamp(saturation, 0, 100) * (Scale / 100);
        if (sat == 0)
        {
            var gray = ScaleToComponent(lum);
            return new Rgba32(gray, gray, gray, 255);
        }

        // DEC places blue at hue 0; the interpolation below uses the conventional
        // wheel where red is at 0, so rotate by 240 degrees.
        var wheel = Wrap360(hue + 240);
        var high = lum <= Scale / 2
            ? (int)DivideRounded((long)lum * (Scale + sat), Scale)
            : lum + sat - (int)DivideRounded((long)lum * sat, Scale);
        var low = (2 * lum) - high;

        return new Rgba32(
            ScaleToComponent(Interpolate(low, high, Wrap360(wheel + 120))),
            ScaleToComponent(Interpolate(low, high, wheel)),
            ScaleToComponent(Interpolate(low, high, Wrap360(wheel - 120))),
            255);
    }

    /// <summary>
    /// Converts a parsed DEC color introducer definition to a color.
    /// </summary>
    public static Rgba32 FromDefinition(SixelPaletteCommand command) => command.ColorSpace switch
    {
        SixelColorSpace.Rgb => FromRgbPercent(command.X ?? 0, command.Y ?? 0, command.Z ?? 0),
        SixelColorSpace.Hls => FromHls(command.X ?? 0, command.Y ?? 0, command.Z ?? 0),
        _ => new Rgba32(0, 0, 0, 255),
    };

    private static int Interpolate(int low, int high, int degrees)
    {
        if (degrees < 60)
        {
            return low + (int)DivideRounded((long)(high - low) * degrees, 60);
        }

        if (degrees < 180)
        {
            return high;
        }

        if (degrees < 240)
        {
            return low + (int)DivideRounded((long)(high - low) * (240 - degrees), 60);
        }

        return low;
    }

    private static byte ScaleToComponent(int scaled)
    {
        var clamped = Math.Clamp(scaled, 0, Scale);
        return (byte)(((clamped * 255) + (Scale / 2)) / Scale);
    }

    private static long DivideRounded(long value, long divisor) =>
        (value + (divisor / 2)) / divisor;

    private static int Wrap360(int degrees)
    {
        var wrapped = degrees % 360;
        return wrapped < 0 ? wrapped + 360 : wrapped;
    }
}
