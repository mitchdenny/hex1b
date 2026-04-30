using System.Diagnostics;
using Hex1b.Surfaces;
using Hex1b.Theming;

namespace CloudTermDemo;

/// <summary>
/// Surface cell-compute effects for the splash screen — swirling blue gradient
/// background and cloud logo fade-in.
/// </summary>
internal static class CloudEffects
{
    /// <summary>
    /// Animated swirling blue gradient background. Uses time-based sine/cosine waves
    /// to create a gentle, flowing effect across shades of blue and cyan.
    /// </summary>
    public static CellCompute SwirlingBlueBackground(double elapsedSeconds)
    {
        return ctx =>
        {
            var t = elapsedSeconds * 0.8;

            // Multi-frequency sine waves for organic movement
            var wave1 = Math.Sin(ctx.X * 0.15 + ctx.Y * 0.1 + t * 1.2);
            var wave2 = Math.Cos(ctx.X * 0.08 - ctx.Y * 0.12 + t * 0.9);
            var wave3 = Math.Sin((ctx.X + ctx.Y) * 0.06 + t * 1.5);
            var combined = (wave1 + wave2 + wave3) / 3.0;

            // Map to blue/cyan color range
            var intensity = (combined + 1.0) / 2.0; // normalize 0..1
            var r = (byte)(10 + intensity * 30);
            var g = (byte)(20 + intensity * 80);
            var b = (byte)(80 + intensity * 175);

            // Pick a character that adds subtle texture
            var charIndex = (int)((Math.Sin(ctx.X * 0.3 + t) + 1) * 2) % 4;
            var ch = charIndex switch
            {
                0 => "░",
                1 => "▒",
                2 => "░",
                _ => " ",
            };

            var bg = Hex1bColor.FromRgb(r, g, b);
            var fg = Hex1bColor.FromRgb(
                (byte)Math.Min(255, r + 30),
                (byte)Math.Min(255, g + 30),
                (byte)Math.Min(255, b + 30));

            return new SurfaceCell(ch, fg, bg);
        };
    }

    /// <summary>
    /// Cloud ASCII art rendered as computed cells that fade in over time.
    /// The cloud is centered on the surface and its opacity ramps from 0 to 1.
    /// </summary>
    public static CellCompute CloudFadeIn(double opacity, int surfaceWidth, int surfaceHeight)
    {
        var cloudLines = new[]
        {
            @"           .::::::.           ",
            @"        .::        ::.        ",
            @"      .:              :.      ",
            @"    .:                  :.    ",
            @"   ::    C L O U D       ::   ",
            @"   ::   T E R M I N A L  ::   ",
            @"   ::                    ::   ",
            @"    '::                ::'    ",
            @"      ':::::::::::::::'      ",
        };

        var cloudWidth = cloudLines.Max(l => l.Length);
        var cloudHeight = cloudLines.Length;
        var offsetX = (surfaceWidth - cloudWidth) / 2;
        var offsetY = (surfaceHeight - cloudHeight) / 2;

        return ctx =>
        {
            var localX = ctx.X - offsetX;
            var localY = ctx.Y - offsetY;

            if (localY >= 0 && localY < cloudHeight && localX >= 0 && localX < cloudLines[localY].Length)
            {
                var ch = cloudLines[localY][localX];
                if (ch != ' ')
                {
                    var below = ctx.GetBelow();
                    var white = Hex1bColor.FromRgb(
                        (byte)(255 * opacity),
                        (byte)(255 * opacity),
                        (byte)(255 * opacity));

                    // Blend cloud foreground over the background
                    var blendedBg = BlendColor(below.Background, white, opacity * 0.3);
                    return new SurfaceCell(
                        ch.ToString(),
                        white,
                        blendedBg);
                }
            }

            // Transparent — pass through the layer below
            return ctx.GetBelow();
        };
    }

    /// <summary>
    /// Dims the entire surface for fade-out transition effects.
    /// </summary>
    public static CellCompute FadeOut(double dimAmount)
    {
        return ctx =>
        {
            var below = ctx.GetBelow();
            var factor = 1.0 - Math.Clamp(dimAmount, 0, 1);
            var fg = DimColor(below.Foreground, factor);
            var bg = DimColor(below.Background, factor);
            return below with { Foreground = fg, Background = bg };
        };
    }

    private static Hex1bColor? BlendColor(Hex1bColor? baseColor, Hex1bColor overlay, double amount)
    {
        if (baseColor is null || baseColor.Value.IsDefault)
            return baseColor;

        var c = baseColor.Value;
        return Hex1bColor.FromRgb(
            (byte)(c.R * (1 - amount) + overlay.R * amount),
            (byte)(c.G * (1 - amount) + overlay.G * amount),
            (byte)(c.B * (1 - amount) + overlay.B * amount));
    }

    private static Hex1bColor? DimColor(Hex1bColor? color, double factor)
    {
        if (color is null || color.Value.IsDefault)
            return color;

        var c = color.Value;
        return Hex1bColor.FromRgb(
            (byte)(c.R * factor),
            (byte)(c.G * factor),
            (byte)(c.B * factor));
    }
}
