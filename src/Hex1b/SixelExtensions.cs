using System.Diagnostics.CodeAnalysis;
using Hex1b.Surfaces;

namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating SixelWidget.
/// </summary>
[Experimental("HEX1B_SIXEL", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/sixel.md")]
public static class SixelExtensions
{
    /// <summary>
    /// Creates a Sixel widget from structured RGBA pixels.
    /// </summary>
    /// <param name="context">The widget context.</param>
    /// <param name="pixels">The pixels to encode and display.</param>
    /// <param name="fallback">The widget displayed when Sixel is unavailable.</param>
    /// <returns>A new Sixel widget at its natural pixel-to-cell size.</returns>
    public static SixelWidget Sixel<TParent>(
        this WidgetContext<TParent> context,
        SixelPixelBuffer pixels,
        Hex1bWidget fallback)
        where TParent : Hex1bWidget
        => new(pixels, fallback);

    /// <summary>
    /// Creates a Sixel widget from structured RGBA pixels.
    /// </summary>
    /// <param name="context">The widget context.</param>
    /// <param name="pixels">The pixels to encode and display.</param>
    /// <param name="builder">Builds the widget displayed when Sixel is unavailable.</param>
    /// <returns>A new Sixel widget at its natural pixel-to-cell size.</returns>
    public static SixelWidget Sixel<TParent>(
        this WidgetContext<TParent> context,
        SixelPixelBuffer pixels,
        Func<WidgetContext<SixelWidget>, Hex1bWidget> builder)
        where TParent : Hex1bWidget
    {
        ArgumentNullException.ThrowIfNull(builder);
        var fallbackContext = new WidgetContext<SixelWidget>();
        return new SixelWidget(pixels, builder(fallbackContext));
    }

    /// <summary>
    /// Creates a SixelWidget with the specified image data and fallback widget.
    /// </summary>
    /// <param name="context">The widget context.</param>
    /// <param name="imageData">The Sixel-encoded image data.</param>
    /// <param name="fallback">A widget to display if Sixel is not supported.</param>
    /// <param name="width">Optional width in character cells.</param>
    /// <param name="height">Optional height in character cells.</param>
    public static SixelWidget Sixel<TParent>(
        this WidgetContext<TParent> context,
        string imageData,
        Hex1bWidget fallback,
        int? width = null,
        int? height = null)
        where TParent : Hex1bWidget
        => new(imageData, fallback, width, height);

    /// <summary>
    /// Creates a SixelWidget with the specified image data and a text fallback.
    /// </summary>
    /// <param name="context">The widget context.</param>
    /// <param name="imageData">The Sixel-encoded image data.</param>
    /// <param name="fallbackText">Text to display if Sixel is not supported.</param>
    /// <param name="width">Optional width in character cells.</param>
    /// <param name="height">Optional height in character cells.</param>
    public static SixelWidget Sixel<TParent>(
        this WidgetContext<TParent> context,
        string imageData,
        string fallbackText,
        int? width = null,
        int? height = null)
        where TParent : Hex1bWidget
        => new(imageData, new TextBlockWidget(fallbackText), width, height);

    /// <summary>
    /// Creates a SixelWidget with image data and a fallback widget builder.
    /// </summary>
    /// <param name="context">The widget context.</param>
    /// <param name="imageData">The Sixel-encoded image data.</param>
    /// <param name="builder">Builder for the fallback widget.</param>
    /// <param name="width">Optional width in character cells.</param>
    /// <param name="height">Optional height in character cells.</param>
    public static SixelWidget Sixel<TParent>(
        this WidgetContext<TParent> context,
        string imageData,
        Func<WidgetContext<SixelWidget>, Hex1bWidget> builder,
        int? width = null,
        int? height = null)
        where TParent : Hex1bWidget
    {
        var fallbackCtx = new WidgetContext<SixelWidget>();
        return new SixelWidget(
            imageData,
            builder(fallbackCtx),
            width,
            height);
    }

    /// <summary>
    /// Creates a SixelWidget with image data and a VStack fallback.
    /// </summary>
    /// <param name="context">The widget context.</param>
    /// <param name="imageData">The Sixel-encoded image data.</param>
    /// <param name="builder">Builder for the fallback widgets (wrapped in VStack).</param>
    /// <param name="width">Optional width in character cells.</param>
    /// <param name="height">Optional height in character cells.</param>
    public static SixelWidget Sixel<TParent>(
        this WidgetContext<TParent> context,
        string imageData,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> builder,
        int? width = null,
        int? height = null)
        where TParent : Hex1bWidget
    {
        var fallbackCtx = new WidgetContext<VStackWidget>();
        return new SixelWidget(
            imageData,
            new VStackWidget(builder(fallbackCtx)),
            width,
            height);
    }

    /// <summary>
    /// Sets the display width in terminal cells.
    /// </summary>
    /// <remarks>
    /// Structured pixels are resampled to this width. Pre-encoded content must
    /// already have this natural width under the active Sixel protocol metrics.
    /// </remarks>
    /// <param name="widget">The Sixel widget to configure.</param>
    /// <param name="width">The display width in terminal cells.</param>
    /// <returns>A new widget with the requested width.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="width"/> is not positive.
    /// </exception>
    public static SixelWidget Width(this SixelWidget widget, int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        return widget with { Width = width };
    }

    /// <summary>
    /// Sets the display height in terminal cells.
    /// </summary>
    /// <remarks>
    /// Structured pixels are resampled to this height. Pre-encoded content must
    /// already have this natural height under the active Sixel protocol metrics.
    /// </remarks>
    /// <param name="widget">The Sixel widget to configure.</param>
    /// <param name="height">The display height in terminal cells.</param>
    /// <returns>A new widget with the requested height.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="height"/> is not positive.
    /// </exception>
    public static SixelWidget Height(this SixelWidget widget, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return widget with { Height = height };
    }
}
