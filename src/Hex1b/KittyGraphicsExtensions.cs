using Hex1b.Kgp;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating <see cref="KittyGraphicsWidget"/> instances using the fluent API.
/// </summary>
/// <remarks>
/// <para>
/// These methods enable image display within widget builder callbacks using the Kitty Graphics Protocol.
/// The terminal must support KGP (<see cref="TerminalCapabilities.SupportsKgp"/>) for images to render.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ctx.VStack(v =&gt; [
///     v.Text("Image Demo"),
///     v.KittyGraphics(pixelData, 64, 64),
///     v.Text("Caption")
/// ])
/// </code>
/// </example>
/// <seealso cref="KittyGraphicsWidget"/>
public static class KittyGraphicsExtensions
{
    /// <summary>
    /// Creates a <see cref="KittyGraphicsWidget"/> with the specified pixel data and dimensions.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type in the current context.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="pixelData">Raw pixel data in RGBA32 format (4 bytes per pixel).</param>
    /// <param name="pixelWidth">Width of the image in pixels.</param>
    /// <param name="pixelHeight">Height of the image in pixels.</param>
    /// <returns>A new <see cref="KittyGraphicsWidget"/>.</returns>
    public static KittyGraphicsWidget KittyGraphics<TParent>(
        this WidgetContext<TParent> ctx,
        byte[] pixelData,
        uint pixelWidth,
        uint pixelHeight)
        where TParent : Hex1bWidget
        => new(pixelData, pixelWidth, pixelHeight);

    /// <summary>
    /// Creates a <see cref="KittyGraphicsWidget"/> with the specified pixel data, dimensions, and format.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type in the current context.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="pixelData">Raw pixel data in the specified format.</param>
    /// <param name="pixelWidth">Width of the image in pixels.</param>
    /// <param name="pixelHeight">Height of the image in pixels.</param>
    /// <param name="format">Pixel format (RGB24 or RGBA32).</param>
    /// <returns>A new <see cref="KittyGraphicsWidget"/>.</returns>
    public static KittyGraphicsWidget KittyGraphics<TParent>(
        this WidgetContext<TParent> ctx,
        byte[] pixelData,
        uint pixelWidth,
        uint pixelHeight,
        KgpFormat format)
        where TParent : Hex1bWidget
        => new(pixelData, pixelWidth, pixelHeight, format);

    /// <summary>
    /// Sets the display size in terminal cells.
    /// </summary>
    /// <param name="widget">The widget to configure.</param>
    /// <param name="columns">Number of terminal columns.</param>
    /// <param name="rows">Number of terminal rows.</param>
    /// <returns>A new widget with updated display size.</returns>
    public static KittyGraphicsWidget WithDisplaySize(this KittyGraphicsWidget widget, uint columns, uint rows)
        => widget with { DisplayColumns = columns, DisplayRows = rows };
}
