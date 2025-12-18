using System.Diagnostics.CodeAnalysis;

namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating SixelWidget.
/// </summary>
[Experimental("HEX1B_SIXEL", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/sixel.md")]
public static class SixelExtensions
{
    /// <summary>
    /// Creates a SixelWidget with the specified image data and fallback widget.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="imageData">The Sixel-encoded image data.</param>
    /// <param name="fallback">A widget to display if Sixel is not supported.</param>
    /// <param name="width">Optional width in character cells.</param>
    /// <param name="height">Optional height in character cells.</param>
    public static SixelWidget Sixel<TParent>(
        this WidgetContext<TParent> ctx,
        string imageData,
        Hex1bWidget fallback,
        int? width = null,
        int? height = null)
        where TParent : Hex1bWidget
        => new(imageData, fallback, width, height);

    /// <summary>
    /// Creates a SixelWidget with the specified image data and a text fallback.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="imageData">The Sixel-encoded image data.</param>
    /// <param name="fallbackText">Text to display if Sixel is not supported.</param>
    /// <param name="width">Optional width in character cells.</param>
    /// <param name="height">Optional height in character cells.</param>
    public static SixelWidget Sixel<TParent>(
        this WidgetContext<TParent> ctx,
        string imageData,
        string fallbackText,
        int? width = null,
        int? height = null)
        where TParent : Hex1bWidget
        => new(imageData, new TextBlockWidget(fallbackText), width, height);

    /// <summary>
    /// Creates a SixelWidget with image data and a fallback widget builder.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="imageData">The Sixel-encoded image data.</param>
    /// <param name="fallbackBuilder">Builder for the fallback widget.</param>
    /// <param name="width">Optional width in character cells.</param>
    /// <param name="height">Optional height in character cells.</param>
    public static SixelWidget Sixel<TParent>(
        this WidgetContext<TParent> ctx,
        string imageData,
        Func<WidgetContext<SixelWidget>, Hex1bWidget> fallbackBuilder,
        int? width = null,
        int? height = null)
        where TParent : Hex1bWidget
    {
        var fallbackCtx = new WidgetContext<SixelWidget>();
        return new SixelWidget(
            imageData,
            fallbackBuilder(fallbackCtx),
            width,
            height);
    }

    /// <summary>
    /// Creates a SixelWidget with image data and a VStack fallback.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="imageData">The Sixel-encoded image data.</param>
    /// <param name="fallbackBuilder">Builder for the fallback widgets (wrapped in VStack).</param>
    /// <param name="width">Optional width in character cells.</param>
    /// <param name="height">Optional height in character cells.</param>
    public static SixelWidget Sixel<TParent>(
        this WidgetContext<TParent> ctx,
        string imageData,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> fallbackBuilder,
        int? width = null,
        int? height = null)
        where TParent : Hex1bWidget
    {
        var fallbackCtx = new WidgetContext<VStackWidget>();
        return new SixelWidget(
            imageData,
            new VStackWidget(fallbackBuilder(fallbackCtx)),
            width,
            height);
    }
}
