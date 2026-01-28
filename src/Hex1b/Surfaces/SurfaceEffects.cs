using Hex1b.Theming;

namespace Hex1b.Surfaces;

/// <summary>
/// Provides common computed cell effects for use with <see cref="CompositeSurface"/>
/// and <see cref="Hex1b.Widgets.SurfaceWidget"/>.
/// </summary>
/// <remarks>
/// <para>
/// These effects return <see cref="CellCompute"/> delegates that can be used with
/// computed layers to create visual effects like fog of war, tinting, and vignettes.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ctx.Surface(s => [
///     s.Layer(terrainSurface),
///     s.Layer(SurfaceEffects.FogOfWar(mouseX, mouseY, radius: 5)),
/// ])
/// </code>
/// </example>
public static class SurfaceEffects
{
    /// <summary>
    /// Creates a fog of war effect that reveals content within a radius of a center point.
    /// </summary>
    /// <param name="centerX">The X coordinate of the reveal center.</param>
    /// <param name="centerY">The Y coordinate of the reveal center.</param>
    /// <param name="radius">The radius of full visibility.</param>
    /// <param name="fadeWidth">The width of the fade transition (default 2).</param>
    /// <returns>A <see cref="CellCompute"/> delegate for the fog effect.</returns>
    /// <remarks>
    /// <para>
    /// Cells within <paramref name="radius"/> of the center are fully visible.
    /// Cells within <paramref name="radius"/> + <paramref name="fadeWidth"/> are partially dimmed.
    /// Cells beyond are fully hidden (replaced with dark background).
    /// </para>
    /// </remarks>
    public static CellCompute FogOfWar(int centerX, int centerY, int radius, int fadeWidth = 2)
    {
        return ctx =>
        {
            var distance = Math.Sqrt((ctx.X - centerX) * (ctx.X - centerX) + (ctx.Y - centerY) * (ctx.Y - centerY));
            
            if (distance <= radius)
            {
                // Fully visible - pass through
                return ctx.GetBelow();
            }
            
            if (distance <= radius + fadeWidth)
            {
                // Partial visibility - dim the cell
                var below = ctx.GetBelow();
                var factor = 1.0 - (distance - radius) / fadeWidth;
                return DimCell(below, factor);
            }
            
            // Fully hidden - show fog
            return new SurfaceCell(" ", null, Hex1bColor.FromRgb(20, 20, 20));
        };
    }

    /// <summary>
    /// Creates a circular reveal effect (inverse of fog of war).
    /// </summary>
    /// <param name="centerX">The X coordinate of the reveal center.</param>
    /// <param name="centerY">The Y coordinate of the reveal center.</param>
    /// <param name="radius">The radius of the reveal circle.</param>
    /// <returns>A <see cref="CellCompute"/> delegate for the reveal effect.</returns>
    public static CellCompute CircularReveal(int centerX, int centerY, int radius)
    {
        return ctx =>
        {
            var distance = Math.Sqrt((ctx.X - centerX) * (ctx.X - centerX) + (ctx.Y - centerY) * (ctx.Y - centerY));
            
            if (distance <= radius)
            {
                return ctx.GetBelow();
            }
            
            // Outside radius - hide
            return new SurfaceCell(" ", null, null);
        };
    }

    /// <summary>
    /// Creates a vignette effect that darkens edges of the surface.
    /// </summary>
    /// <param name="width">The width of the surface.</param>
    /// <param name="height">The height of the surface.</param>
    /// <param name="strength">The strength of the vignette (0.0 to 1.0, default 0.5).</param>
    /// <returns>A <see cref="CellCompute"/> delegate for the vignette effect.</returns>
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

    /// <summary>
    /// Creates a color tint effect that applies a color overlay.
    /// </summary>
    /// <param name="color">The tint color.</param>
    /// <param name="opacity">The opacity of the tint (0.0 to 1.0).</param>
    /// <returns>A <see cref="CellCompute"/> delegate for the tint effect.</returns>
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

    /// <summary>
    /// Creates a dim effect that reduces brightness uniformly.
    /// </summary>
    /// <param name="amount">The amount to dim (0.0 = no dim, 1.0 = fully dark).</param>
    /// <returns>A <see cref="CellCompute"/> delegate for the dim effect.</returns>
    public static CellCompute Dim(double amount = 0.5)
    {
        return ctx =>
        {
            var below = ctx.GetBelow();
            return DimCell(below, 1.0 - amount);
        };
    }

    /// <summary>
    /// Creates an effect that inverts foreground and background colors.
    /// </summary>
    /// <returns>A <see cref="CellCompute"/> delegate for the invert effect.</returns>
    public static CellCompute Invert()
    {
        return ctx =>
        {
            var below = ctx.GetBelow();
            return below with { Foreground = below.Background, Background = below.Foreground };
        };
    }

    /// <summary>
    /// Creates a passthrough effect that returns cells unchanged.
    /// </summary>
    /// <returns>A <see cref="CellCompute"/> delegate that passes through unchanged.</returns>
    public static CellCompute Passthrough()
    {
        return ctx => ctx.GetBelow();
    }

    /// <summary>
    /// Creates a conditional effect that applies different effects based on position.
    /// </summary>
    /// <param name="predicate">The condition to check for each cell position.</param>
    /// <param name="whenTrue">The effect to apply when predicate is true.</param>
    /// <param name="whenFalse">The effect to apply when predicate is false.</param>
    /// <returns>A <see cref="CellCompute"/> delegate for the conditional effect.</returns>
    public static CellCompute Conditional(
        Func<int, int, bool> predicate,
        CellCompute whenTrue,
        CellCompute whenFalse)
    {
        return ctx =>
        {
            if (predicate(ctx.X, ctx.Y))
                return whenTrue(ctx);
            return whenFalse(ctx);
        };
    }

    /// <summary>
    /// Creates a scanline effect that alternates row visibility/dimming.
    /// </summary>
    /// <param name="dimAmount">How much to dim alternate rows (0.0 to 1.0).</param>
    /// <returns>A <see cref="CellCompute"/> delegate for the scanline effect.</returns>
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

    /// <summary>
    /// Creates a border highlight effect that highlights cells at the edges.
    /// </summary>
    /// <param name="width">The width of the surface.</param>
    /// <param name="height">The height of the surface.</param>
    /// <param name="color">The highlight color.</param>
    /// <param name="thickness">The border thickness (default 1).</param>
    /// <returns>A <see cref="CellCompute"/> delegate for the border effect.</returns>
    public static CellCompute BorderHighlight(int width, int height, Hex1bColor color, int thickness = 1)
    {
        return ctx =>
        {
            var below = ctx.GetBelow();
            
            var isOnBorder = ctx.X < thickness || 
                             ctx.X >= width - thickness || 
                             ctx.Y < thickness || 
                             ctx.Y >= height - thickness;
            
            if (isOnBorder)
                return below with { Background = color };
            
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
            (byte)(c.B * factor)
        );
    }

    private static Hex1bColor? BlendColors(Hex1bColor? baseColor, Hex1bColor tint, double opacity)
    {
        if (baseColor is null || baseColor.Value.IsDefault)
            return baseColor;

        var c = baseColor.Value;
        return Hex1bColor.FromRgb(
            (byte)(c.R * (1 - opacity) + tint.R * opacity),
            (byte)(c.G * (1 - opacity) + tint.G * opacity),
            (byte)(c.B * (1 - opacity) + tint.B * opacity)
        );
    }
}
