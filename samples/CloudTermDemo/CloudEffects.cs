using Hex1b.Surfaces;
using Hex1b.Theming;

namespace CloudTermDemo;

/// <summary>
/// Surface cell-compute effects for the splash screen — fluid blue sky background,
/// half-block cloud rendering, and text overlays.
/// </summary>
internal static class CloudEffects
{
    // Complementary color to sky blue — warm amber/gold for text backgrounds
    private static readonly Hex1bColor TextBgColor = Hex1bColor.FromRgb(180, 140, 60);
    private static readonly Hex1bColor TextFgColor = Hex1bColor.FromRgb(255, 255, 255);

    /// <summary>
    /// Subtle fluid blue sky background. Uses layered noise-like functions to create
    /// gentle movement that reads as "sky" — mostly calm blues with slight variation.
    /// </summary>
    public static CellCompute FluidSkyBackground(double elapsedSeconds, int surfaceHeight)
    {
        var t = elapsedSeconds;

        return ctx =>
        {
            var x = ctx.X;
            var y = ctx.Y;

            // Base sky gradient: darker at top, lighter toward bottom
            var verticalGradient = (double)y / Math.Max(surfaceHeight - 1, 1);
            var baseR = 20 + verticalGradient * 30;
            var baseG = 50 + verticalGradient * 60;
            var baseB = 140 + verticalGradient * 80;

            // Layer 1: slow, large-scale drift
            var n1 = Math.Sin(x * 0.04 + t * 0.15) * Math.Cos(y * 0.06 + t * 0.12);

            // Layer 2: medium ripple
            var n2 = Math.Sin(x * 0.09 - y * 0.07 + t * 0.25) * 0.6;

            // Layer 3: fine detail — very subtle
            var n3 = Math.Sin(x * 0.18 + y * 0.14 + t * 0.4)
                    * Math.Cos(x * 0.12 - y * 0.09 + t * 0.35) * 0.3;

            var noise = (n1 + n2 + n3) * 0.33;

            // Apply noise as subtle color variation
            var r = (byte)Math.Clamp(baseR + noise * 15, 0, 255);
            var g = (byte)Math.Clamp(baseG + noise * 25, 0, 255);
            var b = (byte)Math.Clamp(baseB + noise * 35, 0, 255);

            return new SurfaceCell(" ", null, Hex1bColor.FromRgb(r, g, b));
        };
    }

    /// <summary>
    /// Renders a fluffy cloud using half-block characters (▀▄█) from a greyscale bitmap.
    /// The cloud is centered on the surface and fades in with the given opacity.
    /// </summary>
    public static CellCompute HalfBlockCloud(double opacity, int surfaceWidth, int surfaceHeight)
    {
        var bitmap = CloudBitmap.Pixels;
        var bitmapWidth = CloudBitmap.Width;
        var bitmapHeight = CloudBitmap.Height;

        // Half-blocks: each terminal row renders 2 bitmap rows
        var charHeight = (bitmapHeight + 1) / 2;
        var charWidth = bitmapWidth;

        var offsetX = (surfaceWidth - charWidth) / 2;
        var offsetY = (surfaceHeight - charHeight) / 2;

        return ctx =>
        {
            var localX = ctx.X - offsetX;
            var localY = ctx.Y - offsetY;

            if (localX < 0 || localX >= charWidth || localY < 0 || localY >= charHeight)
                return ctx.GetBelow();

            // Two bitmap rows per terminal row
            var topRow = localY * 2;
            var botRow = topRow + 1;

            var topAlpha = topRow < bitmapHeight ? bitmap[topRow, localX] / 255.0 * opacity : 0.0;
            var botAlpha = botRow < bitmapHeight ? bitmap[botRow, localX] / 255.0 * opacity : 0.0;

            // Skip fully transparent cells
            if (topAlpha < 0.01 && botAlpha < 0.01)
                return ctx.GetBelow();

            var below = ctx.GetBelow();
            var belowBg = below.Background ?? Hex1bColor.FromRgb(0, 0, 0);

            // Cloud is white — blend with background
            var topColor = BlendToWhite(belowBg, topAlpha);
            var botColor = BlendToWhite(belowBg, botAlpha);

            if (topAlpha > 0.01 && botAlpha > 0.01)
            {
                // Both halves visible: use ▀ with top=fg, bottom=bg
                return new SurfaceCell("▀", topColor, botColor);
            }
            else if (topAlpha > 0.01)
            {
                // Only top half
                return new SurfaceCell("▀", topColor, belowBg);
            }
            else
            {
                // Only bottom half
                return new SurfaceCell("▄", botColor, belowBg);
            }
        };
    }

    /// <summary>
    /// Renders "CLOUD TERM DEMO" text centered horizontally below the cloud,
    /// and version text in the bottom-right corner.
    /// </summary>
    public static CellCompute TextOverlay(
        double opacity,
        int surfaceWidth,
        int surfaceHeight,
        string version)
    {
        const string title = " CLOUD TERM DEMO ";
        var titleX = (surfaceWidth - title.Length) / 2;

        // Position title below the cloud (cloud center + half cloud char height + gap)
        var cloudCharHeight = (CloudBitmap.Height + 1) / 2;
        var cloudCenterY = surfaceHeight / 2;
        var titleY = cloudCenterY + cloudCharHeight / 2 + 2;

        var versionText = $" v{version} ";
        var versionX = surfaceWidth - versionText.Length - 1;
        var versionY = surfaceHeight - 1;

        var fgAlpha = Math.Clamp(opacity, 0, 1);

        return ctx =>
        {
            // Title text
            if (ctx.Y == titleY && ctx.X >= titleX && ctx.X < titleX + title.Length)
            {
                var ch = title[ctx.X - titleX];
                var below = ctx.GetBelow();
                var bg = BlendColor(below.Background ?? Hex1bColor.Black, TextBgColor, fgAlpha * 0.85);
                var fg = Hex1bColor.FromRgb(
                    (byte)(TextFgColor.R * fgAlpha),
                    (byte)(TextFgColor.G * fgAlpha),
                    (byte)(TextFgColor.B * fgAlpha));
                return new SurfaceCell(ch.ToString(), fg, bg);
            }

            // Version text
            if (ctx.Y == versionY && ctx.X >= versionX && ctx.X < versionX + versionText.Length)
            {
                var ch = versionText[ctx.X - versionX];
                var below = ctx.GetBelow();
                var bg = BlendColor(below.Background ?? Hex1bColor.Black, TextBgColor, fgAlpha * 0.6);
                var fg = Hex1bColor.FromRgb(
                    (byte)(TextFgColor.R * fgAlpha),
                    (byte)(TextFgColor.G * fgAlpha),
                    (byte)(TextFgColor.B * fgAlpha));
                return new SurfaceCell(ch.ToString(), fg, bg);
            }

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

    private static Hex1bColor BlendToWhite(Hex1bColor baseColor, double alpha)
    {
        return Hex1bColor.FromRgb(
            (byte)(baseColor.R + (255 - baseColor.R) * alpha),
            (byte)(baseColor.G + (255 - baseColor.G) * alpha),
            (byte)(baseColor.B + (255 - baseColor.B) * alpha));
    }

    private static Hex1bColor BlendColor(Hex1bColor baseColor, Hex1bColor overlay, double amount)
    {
        return Hex1bColor.FromRgb(
            (byte)(baseColor.R * (1 - amount) + overlay.R * amount),
            (byte)(baseColor.G * (1 - amount) + overlay.G * amount),
            (byte)(baseColor.B * (1 - amount) + overlay.B * amount));
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
