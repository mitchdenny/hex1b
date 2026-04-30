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
    /// Animated flowing sky background with two gentle vortices that traverse the screen.
    /// Each cell traces backwards through the velocity field to create the sensation of
    /// particles/air flowing between the vortices. Vortices wrap around horizontally.
    /// </summary>
    public static CellCompute FluidSkyBackground(double elapsedSeconds, int surfaceWidth, int surfaceHeight)
    {
        var t = elapsedSeconds;

        // Two vortices that move horizontally across the screen, wrapping around
        var v1X = Wrap(t * 0.06 + 0.2);   // slow rightward drift
        var v1Y = 0.4 + Math.Sin(t * 0.3) * 0.08;
        var v2X = Wrap(-t * 0.05 + 0.8);  // slow leftward drift
        var v2Y = 0.6 + Math.Cos(t * 0.25) * 0.08;

        return ctx =>
        {
            // Normalize to 0..1
            var px = (double)ctx.X / Math.Max(surfaceWidth - 1, 1);
            var py = (double)ctx.Y / Math.Max(surfaceHeight - 1, 1);

            // Trace this cell backward through the flow field (advection)
            // Multiple small steps for smooth particle trails
            const int steps = 6;
            const double dt = -0.15;  // backward in time
            var ax = px;
            var ay = py;

            for (var i = 0; i < steps; i++)
            {
                // Velocity from both vortices (tangential flow)
                var (vx, vy) = GetFlowVelocity(ax, ay, v1X, v1Y, v2X, v2Y);
                ax += vx * dt;
                ay += vy * dt;
            }

            // Add a slow global drift so everything feels like it's moving
            ax += t * 0.03;
            ay += t * 0.01;

            // Layered noise on the advected position — this creates flowing streaks
            var n1 = Math.Sin(ax * 8.0 + ay * 5.0);
            var n2 = Math.Sin(ax * 14.0 - ay * 10.0) * 0.5;
            var n3 = Math.Sin(ax * 22.0 + ay * 18.0) * Math.Cos(ax * 12.0 - ay * 16.0) * 0.25;
            var noise = (n1 + n2 + n3) / 1.75;

            // Base gradient: brighter blue
            var baseR = 25 + py * 8;
            var baseG = 50 + py * 20;
            var baseB = 130 + py * 30;

            // Tight clamp — narrow band of medium-bright blues
            var r = (byte)Math.Clamp(baseR + noise * 8, 20, 45);
            var g = (byte)Math.Clamp(baseG + noise * 12, 40, 80);
            var b = (byte)Math.Clamp(baseB + noise * 25, 115, 185);

            return new SurfaceCell(" ", null, Hex1bColor.FromRgb(r, g, b));
        };
    }

    /// <summary>
    /// Computes the flow velocity at a point from two vortices. Returns tangential
    /// velocity (perpendicular to the radial direction) with smooth falloff.
    /// Handles horizontal wrapping so vortices that exit one side appear on the other.
    /// </summary>
    private static (double vx, double vy) GetFlowVelocity(
        double px, double py,
        double v1X, double v1Y,
        double v2X, double v2Y)
    {
        var (vx1, vy1) = VortexVelocity(px, py, v1X, v1Y, 0.4, 1.0);
        var (vx2, vy2) = VortexVelocity(px, py, v2X, v2Y, 0.35, -0.8);
        return (vx1 + vx2, vy1 + vy2);
    }

    /// <summary>
    /// Tangential velocity from a single vortex. Points near the vortex center rotate
    /// around it; the velocity falls off smoothly with distance.
    /// </summary>
    private static (double vx, double vy) VortexVelocity(
        double px, double py,
        double cx, double cy,
        double radius, double strength)
    {
        // Handle horizontal wrapping — pick the closest instance
        var dx = px - cx;
        if (dx > 0.5) dx -= 1.0;
        if (dx < -0.5) dx += 1.0;
        var dy = py - cy;

        var dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 0.001 || dist > radius)
            return (0, 0);

        // Smooth falloff: peaks at ~40% of radius, zero at center and edge
        var normalized = dist / radius;
        var falloff = normalized * Math.Pow(1.0 - normalized, 2) * 4.0;

        // Tangential direction (perpendicular to radial)
        var tx = -dy / dist;
        var ty = dx / dist;

        return (tx * falloff * strength, ty * falloff * strength);
    }

    /// <summary>Wraps a value to the 0..1 range.</summary>
    private static double Wrap(double v)
    {
        v %= 1.0;
        return v < 0 ? v + 1.0 : v;
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
