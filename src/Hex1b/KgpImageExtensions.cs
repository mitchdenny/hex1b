namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating KgpImageWidget.
/// </summary>
public static class KgpImageExtensions
{
    /// <summary>
    /// Creates a KgpImageWidget with the specified RGBA32 pixel data and fallback widget.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="imageData">Raw RGBA32 pixel data.</param>
    /// <param name="pixelWidth">Width of the image in pixels.</param>
    /// <param name="pixelHeight">Height of the image in pixels.</param>
    /// <param name="fallback">A widget to display if KGP is not supported.</param>
    /// <param name="width">Optional width in character cells.</param>
    /// <param name="height">Optional height in character cells.</param>
    public static KgpImageWidget KgpImage<TParent>(
        this WidgetContext<TParent> ctx,
        byte[] imageData,
        int pixelWidth,
        int pixelHeight,
        Hex1bWidget fallback,
        int? width = null,
        int? height = null)
        where TParent : Hex1bWidget
        => new(imageData, pixelWidth, pixelHeight, fallback, width, height);

    /// <summary>
    /// Creates a KgpImageWidget with the specified RGBA32 pixel data and a text fallback.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="imageData">Raw RGBA32 pixel data.</param>
    /// <param name="pixelWidth">Width of the image in pixels.</param>
    /// <param name="pixelHeight">Height of the image in pixels.</param>
    /// <param name="fallbackText">Text to display if KGP is not supported.</param>
    /// <param name="width">Optional width in character cells.</param>
    /// <param name="height">Optional height in character cells.</param>
    public static KgpImageWidget KgpImage<TParent>(
        this WidgetContext<TParent> ctx,
        byte[] imageData,
        int pixelWidth,
        int pixelHeight,
        string fallbackText,
        int? width = null,
        int? height = null)
        where TParent : Hex1bWidget
        => new(imageData, pixelWidth, pixelHeight, new TextBlockWidget(fallbackText), width, height);

    /// <summary>
    /// Sets the z-ordering to above text (image renders on top of text).
    /// </summary>
    public static KgpImageWidget AboveText(this KgpImageWidget widget)
        => widget with { ZOrder = KgpZOrder.AboveText };

    /// <summary>
    /// Sets the z-ordering to below text (image renders behind text).
    /// </summary>
    public static KgpImageWidget BelowText(this KgpImageWidget widget)
        => widget with { ZOrder = KgpZOrder.BelowText };

    /// <summary>
    /// Sets the display width in character cells.
    /// </summary>
    public static KgpImageWidget WithWidth(this KgpImageWidget widget, int width)
        => widget with { Width = width };

    /// <summary>
    /// Sets the display height in character cells.
    /// </summary>
    public static KgpImageWidget WithHeight(this KgpImageWidget widget, int height)
        => widget with { Height = height };
}
