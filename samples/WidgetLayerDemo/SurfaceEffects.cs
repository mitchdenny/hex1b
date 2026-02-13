using Hex1b.Surfaces;
using Hex1b.Theming;

namespace WidgetLayerDemo;

/// <summary>
/// Common computed cell effects for use with <see cref="CompositeSurface"/>
/// and <see cref="Hex1b.Widgets.SurfaceWidget"/>.
/// </summary>
static class SurfaceEffects
{
    public static CellCompute FogOfWar(int centerX, int centerY, int radius, int fadeWidth = 2)
    {
        return ctx =>
        {
            var distance = Math.Sqrt((ctx.X - centerX) * (ctx.X - centerX) + (ctx.Y - centerY) * (ctx.Y - centerY));

            if (distance <= radius)
                return ctx.GetBelow();

            if (distance <= radius + fadeWidth)
            {
                var below = ctx.GetBelow();
                var factor = 1.0 - (distance - radius) / fadeWidth;
                return DimCell(below, factor);
            }

            return new SurfaceCell(" ", null, Hex1bColor.FromRgb(20, 20, 20));
        };
    }

    public static CellCompute CircularReveal(int centerX, int centerY, int radius)
    {
        return ctx =>
        {
            var distance = Math.Sqrt((ctx.X - centerX) * (ctx.X - centerX) + (ctx.Y - centerY) * (ctx.Y - centerY));
            if (distance <= radius)
                return ctx.GetBelow();
            return new SurfaceCell(" ", null, null);
        };
    }

    public static CellCompute Vignette(int width, int height, double strength = 0.5)
    {
        var centerX = width / 2.0;
        var centerY = height / 2.0;
        var maxDistance = Math.Sqrt(centerX * centerX + centerY * centerY);

        return ctx =>
        {
            var dx = ctx.X - centerX;
            var dy = ctx.Y - centerY;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            var normalizedDistance = distance / maxDistance;
            var below = ctx.GetBelow();
            var factor = 1.0 - (normalizedDistance * strength);
            return DimCell(below, Math.Max(0, factor));
        };
    }

    public static CellCompute Tint(Hex1bColor color, double opacity = 0.5)
    {
        return ctx =>
        {
            var below = ctx.GetBelow();
            var tintedFg = BlendColors(below.Foreground, color, opacity);
            var tintedBg = BlendColors(below.Background, color, opacity);
            return below with { Foreground = tintedFg, Background = tintedBg };
        };
    }

    public static CellCompute Dim(double amount = 0.5)
    {
        return ctx =>
        {
            var below = ctx.GetBelow();
            return DimCell(below, 1.0 - amount);
        };
    }

    public static CellCompute Passthrough()
    {
        return ctx => ctx.GetBelow();
    }

    public static CellCompute Scanlines(double dimAmount = 0.3)
    {
        return ctx =>
        {
            var below = ctx.GetBelow();
            if (ctx.Y % 2 == 0)
                return DimCell(below, 1.0 - dimAmount);
            return below;
        };
    }

    private static SurfaceCell DimCell(SurfaceCell cell, double factor)
    {
        var dimmedFg = DimColor(cell.Foreground, factor);
        var dimmedBg = DimColor(cell.Background, factor);
        return cell with { Foreground = dimmedFg, Background = dimmedBg };
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

    private static Hex1bColor? BlendColors(Hex1bColor? baseColor, Hex1bColor tint, double opacity)
    {
        if (baseColor is null || baseColor.Value.IsDefault)
            return baseColor;
        var c = baseColor.Value;
        return Hex1bColor.FromRgb(
            (byte)(c.R * (1 - opacity) + tint.R * opacity),
            (byte)(c.G * (1 - opacity) + tint.G * opacity),
            (byte)(c.B * (1 - opacity) + tint.B * opacity));
    }
}
