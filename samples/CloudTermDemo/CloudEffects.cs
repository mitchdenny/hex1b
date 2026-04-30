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
    /// Starry Night-inspired swirling blue background. Creates visible swirl patterns
    /// using flow-field distortion with tightly clamped blue tones, evoking Van Gogh's
    /// brushstroke spirals rendered in deep blues and indigos.
    /// </summary>
    public static CellCompute FluidSkyBackground(double elapsedSeconds, int surfaceWidth, int surfaceHeight)
    {
        var t = elapsedSeconds;

        return ctx =>
        {
            var x = ctx.X;
            var y = ctx.Y;

            // Normalize coordinates to 0..1 range
            var nx = (double)x / Math.Max(surfaceWidth - 1, 1);
            var ny = (double)y / Math.Max(surfaceHeight - 1, 1);

            // === Flow field: warp coordinates through swirling vortices ===

            // Several swirl centers that drift slowly
            var swirl1X = 0.3 + Math.Sin(t * 0.07) * 0.1;
            var swirl1Y = 0.4 + Math.Cos(t * 0.09) * 0.1;
            var swirl2X = 0.7 + Math.Cos(t * 0.06) * 0.12;
            var swirl2Y = 0.3 + Math.Sin(t * 0.08) * 0.08;
            var swirl3X = 0.5 + Math.Sin(t * 0.05 + 1.0) * 0.15;
            var swirl3Y = 0.7 + Math.Cos(t * 0.07 + 2.0) * 0.1;

            // Accumulate angular displacement from each vortex
            var warpX = nx;
            var warpY = ny;

            ApplySwirl(ref warpX, ref warpY, swirl1X, swirl1Y, 0.25, t * 0.4, 1.0);
            ApplySwirl(ref warpX, ref warpY, swirl2X, swirl2Y, 0.20, t * -0.35, 0.8);
            ApplySwirl(ref warpX, ref warpY, swirl3X, swirl3Y, 0.30, t * 0.3, 0.6);

            // === Layered noise on warped coordinates for brushstroke texture ===

            // Large swirling bands
            var n1 = Math.Sin(warpX * 12.0 + warpY * 8.0 + t * 0.2);

            // Medium detail — cross-hatched swirls
            var n2 = Math.Sin(warpX * 20.0 - warpY * 14.0 + t * 0.15) * 0.5;

            // Fine brushstroke grain
            var n3 = Math.Sin(warpX * 35.0 + warpY * 25.0 - t * 0.3)
                   * Math.Cos(warpX * 18.0 - warpY * 30.0 + t * 0.25) * 0.3;

            var noise = (n1 + n2 + n3) / 1.8;  // -1..1 range

            // === Color mapping: tightly clamped blues/indigos ===
            // Base: deep navy to indigo gradient
            var baseR = 15 + ny * 10;   // 15-25
            var baseG = 20 + ny * 25;   // 20-45
            var baseB = 70 + ny * 50;   // 70-120

            // Noise modulates within a narrow band of blues
            var r = (byte)Math.Clamp(baseR + noise * 12, 8, 40);
            var g = (byte)Math.Clamp(baseG + noise * 20, 15, 65);
            var b = (byte)Math.Clamp(baseB + noise * 40, 55, 160);

            return new SurfaceCell(" ", null, Hex1bColor.FromRgb(r, g, b));
        };
    }

    /// <summary>
    /// Applies a swirl distortion centered at (cx, cy) with the given radius and rotation.
    /// Points closer to the center rotate more, creating a vortex-like warp.
    /// </summary>
    private static void ApplySwirl(
        ref double x, ref double y,
        double cx, double cy,
        double radius, double angle, double strength)
    {
        var dx = x - cx;
        var dy = y - cy;
        var dist = Math.Sqrt(dx * dx + dy * dy);

        if (dist < radius && dist > 0.001)
        {
            // Falloff: strongest at center, zero at edge
            var falloff = 1.0 - (dist / radius);
            falloff = falloff * falloff; // quadratic falloff for tighter spirals
            var theta = falloff * angle * strength;

            var cosT = Math.Cos(theta);
            var sinT = Math.Sin(theta);
            x = cx + dx * cosT - dy * sinT;
            y = cy + dx * sinT + dy * cosT;
        }
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
