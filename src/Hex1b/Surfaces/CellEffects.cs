using Hex1b.Theming;

namespace Hex1b.Surfaces;

/// <summary>
/// Provides factory methods for common computed cell effects.
/// </summary>
public static class CellEffects
{
    /// <summary>
    /// Creates a drop shadow effect that darkens the cells below.
    /// </summary>
    /// <remarks>
    /// The shadow effect reads the cell from layers below and returns a darkened version.
    /// The shadow character is a space with a darkened background.
    /// </remarks>
    /// <param name="opacity">The shadow opacity from 0.0 (invisible) to 1.0 (fully opaque black). Default is 0.5.</param>
    /// <returns>A <see cref="CellCompute"/> delegate for the shadow effect.</returns>
    public static CellCompute DropShadow(float opacity = 0.5f)
    {
        opacity = Math.Clamp(opacity, 0f, 1f);

        return context =>
        {
            var below = context.GetBelow();
            var darkenedBg = DarkenColor(below.Background, opacity);
            return new SurfaceCell(" ", null, darkenedBg);
        };
    }

    /// <summary>
    /// Creates a tint overlay effect that applies a color tint to cells below.
    /// </summary>
    /// <param name="tintColor">The color to tint with.</param>
    /// <param name="opacity">The tint opacity from 0.0 (invisible) to 1.0 (full tint). Default is 0.3.</param>
    /// <returns>A <see cref="CellCompute"/> delegate for the tint effect.</returns>
    public static CellCompute Tint(Hex1bColor tintColor, float opacity = 0.3f)
    {
        opacity = Math.Clamp(opacity, 0f, 1f);

        return context =>
        {
            var below = context.GetBelow();
            var tintedBg = BlendColors(below.Background, tintColor, opacity);
            var tintedFg = BlendColors(below.Foreground, tintColor, opacity);
            return below with { Background = tintedBg, Foreground = tintedFg };
        };
    }

    /// <summary>
    /// Creates a dim/fade effect that reduces the intensity of cells below.
    /// </summary>
    /// <param name="amount">The dim amount from 0.0 (no change) to 1.0 (fully black). Default is 0.5.</param>
    /// <returns>A <see cref="CellCompute"/> delegate for the dim effect.</returns>
    public static CellCompute Dim(float amount = 0.5f)
    {
        amount = Math.Clamp(amount, 0f, 1f);

        return context =>
        {
            var below = context.GetBelow();
            var dimmedBg = DarkenColor(below.Background, amount);
            var dimmedFg = DarkenColor(below.Foreground, amount);
            return below with { Background = dimmedBg, Foreground = dimmedFg };
        };
    }

    /// <summary>
    /// Creates an invert effect that inverts the colors of cells below.
    /// </summary>
    /// <returns>A <see cref="CellCompute"/> delegate for the invert effect.</returns>
    public static CellCompute Invert()
    {
        return context =>
        {
            var below = context.GetBelow();
            var invertedBg = InvertColor(below.Background);
            var invertedFg = InvertColor(below.Foreground);
            return below with { Background = invertedBg, Foreground = invertedFg };
        };
    }

    /// <summary>
    /// Creates a blur approximation effect by averaging with adjacent cells.
    /// </summary>
    /// <remarks>
    /// This is a simple box blur that averages the background color with
    /// the 4 adjacent cells (up, down, left, right).
    /// </remarks>
    /// <returns>A <see cref="CellCompute"/> delegate for the blur effect.</returns>
    public static CellCompute BlurBackground()
    {
        return context =>
        {
            var center = context.GetBelow();
            var up = context.GetBelowAt(context.X, context.Y - 1);
            var down = context.GetBelowAt(context.X, context.Y + 1);
            var left = context.GetBelowAt(context.X - 1, context.Y);
            var right = context.GetBelowAt(context.X + 1, context.Y);

            var blurredBg = AverageColors(
                center.Background,
                up.Background,
                down.Background,
                left.Background,
                right.Background);

            return center with { Background = blurredBg };
        };
    }

    /// <summary>
    /// Creates a passthrough effect that simply returns the cell below unchanged.
    /// Useful for testing or as a base for conditional effects.
    /// </summary>
    /// <returns>A <see cref="CellCompute"/> delegate that passes through cells unchanged.</returns>
    public static CellCompute Passthrough()
    {
        return context => context.GetBelow();
    }

    /// <summary>
    /// Creates a conditional effect that applies one of two effects based on a predicate.
    /// </summary>
    /// <param name="predicate">A function that determines which effect to apply based on position.</param>
    /// <param name="whenTrue">The effect to apply when the predicate returns true.</param>
    /// <param name="whenFalse">The effect to apply when the predicate returns false.</param>
    /// <returns>A <see cref="CellCompute"/> delegate for the conditional effect.</returns>
    public static CellCompute Conditional(
        Func<int, int, bool> predicate,
        CellCompute whenTrue,
        CellCompute whenFalse)
    {
        return context =>
        {
            if (predicate(context.X, context.Y))
                return whenTrue(context);
            else
                return whenFalse(context);
        };
    }

    #region Color Helpers

    private static Hex1bColor? DarkenColor(Hex1bColor? color, float amount)
    {
        if (color is null)
            return Hex1bColor.FromRgb(0, 0, 0); // Darken transparent to black

        var c = color.Value;
        var factor = 1f - amount;
        return Hex1bColor.FromRgb(
            (byte)(c.R * factor),
            (byte)(c.G * factor),
            (byte)(c.B * factor));
    }

    private static Hex1bColor? BlendColors(Hex1bColor? baseColor, Hex1bColor overlay, float amount)
    {
        if (baseColor is null)
            return Hex1bColor.FromRgb(
                (byte)(overlay.R * amount),
                (byte)(overlay.G * amount),
                (byte)(overlay.B * amount));

        var c = baseColor.Value;
        var factor = 1f - amount;
        return Hex1bColor.FromRgb(
            (byte)(c.R * factor + overlay.R * amount),
            (byte)(c.G * factor + overlay.G * amount),
            (byte)(c.B * factor + overlay.B * amount));
    }

    private static Hex1bColor? InvertColor(Hex1bColor? color)
    {
        if (color is null)
            return Hex1bColor.White; // Invert transparent to white

        var c = color.Value;
        return Hex1bColor.FromRgb(
            (byte)(255 - c.R),
            (byte)(255 - c.G),
            (byte)(255 - c.B));
    }

    private static Hex1bColor? AverageColors(params Hex1bColor?[] colors)
    {
        int r = 0, g = 0, b = 0, count = 0;

        foreach (var color in colors)
        {
            if (color is not null)
            {
                r += color.Value.R;
                g += color.Value.G;
                b += color.Value.B;
                count++;
            }
        }

        if (count == 0)
            return null;

        return Hex1bColor.FromRgb(
            (byte)(r / count),
            (byte)(g / count),
            (byte)(b / count));
    }

    #endregion
}
