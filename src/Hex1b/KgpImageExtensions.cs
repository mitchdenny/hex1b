namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating and configuring <see cref="KgpImageWidget"/>.
/// </summary>
public static class KgpImageExtensions
{
    /// <summary>
    /// Creates a <see cref="KgpImageWidget"/> with the specified RGBA32 pixel data and fallback builder.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="imageData">Raw RGBA32 pixel data.</param>
    /// <param name="pixelWidth">Width of the image in pixels.</param>
    /// <param name="pixelHeight">Height of the image in pixels.</param>
    /// <param name="fallbackBuilder">Builds the widget displayed when KGP is not supported.</param>
    /// <param name="width">Optional width in character cells.</param>
    /// <param name="height">Optional height in character cells.</param>
    /// <returns>A new <see cref="KgpImageWidget"/>.</returns>
    public static KgpImageWidget KgpImage<TParent>(
        this WidgetContext<TParent> ctx,
        byte[] imageData,
        int pixelWidth,
        int pixelHeight,
        Func<WidgetContext<KgpImageWidget>, Hex1bWidget> fallbackBuilder,
        int? width = null,
        int? height = null)
        where TParent : Hex1bWidget
    {
        var fallbackCtx = new WidgetContext<KgpImageWidget>();
        return new(imageData, pixelWidth, pixelHeight, fallbackBuilder(fallbackCtx), width, height);
    }

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

    /// <summary>
    /// Sets the image stretch mode.
    /// </summary>
    public static KgpImageWidget WithStretch(this KgpImageWidget widget, KgpImageStretch stretch)
        => widget with { Stretch = stretch };

    /// <summary>
    /// Scales the image to fit within the allocated area while preserving the aspect ratio,
    /// maximizing one dimension. The image may be smaller than the available space in one
    /// dimension. Wrap in <see cref="AlignWidget"/> to control positioning.
    /// </summary>
    public static KgpImageWidget Fit(this KgpImageWidget widget)
        => widget with { Stretch = KgpImageStretch.Fit };

    /// <summary>
    /// Scales the image to completely fill the allocated area while preserving the aspect
    /// ratio. Excess portions of the source image are cropped.
    /// </summary>
    public static KgpImageWidget Fill(this KgpImageWidget widget)
        => widget with { Stretch = KgpImageStretch.Fill };

    /// <summary>
    /// Stretches the image to fill the allocated area. Aspect ratio is not preserved.
    /// This is the default behavior.
    /// </summary>
    public static KgpImageWidget Stretched(this KgpImageWidget widget)
        => widget with { Stretch = KgpImageStretch.Stretch };

    /// <summary>
    /// Displays the image at its natural pixel-to-cell dimensions without scaling.
    /// </summary>
    public static KgpImageWidget NaturalSize(this KgpImageWidget widget)
        => widget with { Stretch = KgpImageStretch.None };
}
