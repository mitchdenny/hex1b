namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating and configuring <see cref="KgpImageWidget"/>.
/// </summary>
public static class KgpImageExtensions
{
    /// <summary>
    /// Creates a KGP image backed by full RGBA32 animation frames.
    /// </summary>
    /// <param name="context">The widget context.</param>
    /// <param name="frames">The full animation frames in display order.</param>
    /// <param name="pixelWidth">Width of every frame in pixels.</param>
    /// <param name="pixelHeight">Height of every frame in pixels.</param>
    /// <param name="builder">Builds the widget displayed when KGP is not supported.</param>
    /// <returns>A KGP image configured for terminal-native animation playback.</returns>
    public static KgpImageWidget KgpAnimation<TParent>(
        this WidgetContext<TParent> context,
        IReadOnlyList<KgpAnimationFrame> frames,
        int pixelWidth,
        int pixelHeight,
        Func<WidgetContext<KgpImageWidget>, Hex1bWidget> builder)
        where TParent : Hex1bWidget
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count < 2)
            throw new ArgumentException("A KGP animation requires at least two frames.", nameof(frames));
        if (pixelWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        if (pixelHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(pixelHeight));

        var expectedLength = checked(pixelWidth * pixelHeight * 4);
        foreach (var frame in frames)
        {
            if (frame.Data.Length != expectedLength)
            {
                throw new ArgumentException(
                    $"Each animation frame must contain exactly {expectedLength} RGBA32 bytes.",
                    nameof(frames));
            }
        }

        var fallbackContext = new WidgetContext<KgpImageWidget>();
        return new(frames[0].Data, pixelWidth, pixelHeight, builder(fallbackContext))
        {
            AnimationFrames = frames
        };
    }

    /// <summary>
    /// Creates a <see cref="KgpImageWidget"/> with the specified RGBA32 pixel data and fallback builder.
    /// </summary>
    /// <param name="context">The widget context.</param>
    /// <param name="imageData">Raw RGBA32 pixel data.</param>
    /// <param name="pixelWidth">Width of the image in pixels.</param>
    /// <param name="pixelHeight">Height of the image in pixels.</param>
    /// <param name="builder">Builds the widget displayed when KGP is not supported.</param>
    /// <param name="width">Optional width in character cells.</param>
    /// <param name="height">Optional height in character cells.</param>
    /// <returns>A new <see cref="KgpImageWidget"/>.</returns>
    public static KgpImageWidget KgpImage<TParent>(
        this WidgetContext<TParent> context,
        byte[] imageData,
        int pixelWidth,
        int pixelHeight,
        Func<WidgetContext<KgpImageWidget>, Hex1bWidget> builder,
        int? width = null,
        int? height = null)
        where TParent : Hex1bWidget
    {
        var fallbackCtx = new WidgetContext<KgpImageWidget>();
        return new(imageData, pixelWidth, pixelHeight, builder(fallbackCtx), width, height);
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
    public static KgpImageWidget Width(this KgpImageWidget widget, int width)
        => widget with { Width = width };

    /// <summary>
    /// Sets the display height in character cells.
    /// </summary>
    public static KgpImageWidget Height(this KgpImageWidget widget, int height)
        => widget with { Height = height };

    /// <summary>
    /// Sets the image stretch mode.
    /// </summary>
    public static KgpImageWidget Stretch(this KgpImageWidget widget, KgpImageStretch stretch)
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

    /// <summary>
    /// Starts or stops terminal-native playback for a KGP animation.
    /// Stopping playback resets the animation to its first frame.
    /// </summary>
    /// <param name="widget">The KGP image widget.</param>
    /// <param name="playing">Whether the animation should play.</param>
    /// <returns>The configured widget.</returns>
    public static KgpImageWidget Playing(this KgpImageWidget widget, bool playing = true)
        => widget with { IsAnimationPlaying = playing };
}
