using System.Diagnostics.CodeAnalysis;
using Hex1b.Nodes;
using Hex1b.Sixel;
using Hex1b.Surfaces;

namespace Hex1b.Widgets;

/// <summary>
/// Displays a Sixel image when the effective presentation supports Sixel,
/// otherwise displays a fallback widget.
/// </summary>
/// <remarks>
/// Structured <see cref="SixelPixelBuffer"/> input is the preferred path.
/// Explicit cell dimensions resample structured pixels to the corresponding
/// Sixel protocol raster.
/// Pre-encoded input is retained for compatibility and is validated and
/// normalized to a complete 7-bit DCS sequence, but cannot be resampled.
/// </remarks>
/// <example>
/// <code>
/// #pragma warning disable HEX1B_SIXEL
/// using Hex1b;
/// using Hex1b.Surfaces;
///
/// var pixels = new SixelPixelBuffer(80, 40);
/// for (var y = 0; y &lt; pixels.Height; y++)
/// {
///     for (var x = 0; x &lt; pixels.Width; x++)
///         pixels[x, y] = Rgba32.FromRgb((byte)(x * 3), (byte)(y * 6), 180);
/// }
///
/// await using var terminal = Hex1bTerminal.CreateBuilder()
///     .WithHex1bApp(ctx =&gt;
///         ctx.Sixel(pixels, fallback =&gt; fallback.Text("Sixel unavailable"))
///             .Width(16)
///             .Height(4))
///     .Build();
///
/// await terminal.RunAsync();
/// </code>
/// </example>
[Experimental("HEX1B_SIXEL", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/sixel.md")]
public sealed record SixelWidget : Hex1bWidget
{
    /// <summary>
    /// Creates a widget from structured RGBA pixels.
    /// </summary>
    /// <param name="pixels">The pixels to encode and display.</param>
    /// <param name="fallback">The widget displayed when Sixel is unavailable.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="pixels"/> or <paramref name="fallback"/> is <see langword="null"/>.
    /// </exception>
    public SixelWidget(SixelPixelBuffer pixels, Hex1bWidget fallback)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(fallback);
        Pixels = pixels;
        Fallback = fallback;
    }

    /// <summary>
    /// Creates a widget from validated pre-encoded Sixel data.
    /// </summary>
    /// <param name="imageData">
    /// A complete Sixel DCS sequence, or the Sixel body without its DCS framing.
    /// </param>
    /// <param name="fallback">The widget displayed when Sixel is unavailable.</param>
    /// <param name="width">
    /// Optional display width in terminal cells. When specified, it must match
    /// the payload's natural width under the active Sixel protocol metrics.
    /// </param>
    /// <param name="height">
    /// Optional display height in terminal cells. When specified, it must match
    /// the payload's natural height under the active Sixel protocol metrics.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="imageData"/> is malformed or incomplete.
    /// A dimension mismatch is reported when the widget renders.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="imageData"/> or <paramref name="fallback"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="width"/> or <paramref name="height"/> is not positive.
    /// </exception>
    public SixelWidget(string imageData, Hex1bWidget fallback, int? width = null, int? height = null)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        ImageData = SixelPayload.NormalizeAndValidate(imageData, nameof(imageData));
        Fallback = fallback;
        Width = ValidateDimension(width, nameof(width));
        Height = ValidateDimension(height, nameof(height));
    }

    /// <summary>
    /// Gets the validated, fully framed Sixel sequence for pre-encoded content.
    /// </summary>
    /// <remarks>
    /// This is <see langword="null"/> when the widget was created from
    /// <see cref="SixelPixelBuffer"/>.
    /// </remarks>
    public string? ImageData { get; }

    /// <summary>
    /// Gets the structured pixels supplied to the widget.
    /// </summary>
    /// <remarks>
    /// This is <see langword="null"/> when the widget was created from pre-encoded content.
    /// Do not mutate the buffer while the widget is in use.
    /// </remarks>
    public SixelPixelBuffer? Pixels { get; }

    /// <summary>
    /// Gets the widget displayed when Sixel is unavailable.
    /// </summary>
    public Hex1bWidget Fallback { get; init; }

    /// <summary>
    /// Gets the optional display width in terminal cells.
    /// </summary>
    /// <remarks>
    /// Structured pixels are resampled to this width. Pre-encoded content must
    /// already have this natural width under the active Sixel protocol metrics.
    /// </remarks>
    public int? Width { get; init; }

    /// <summary>
    /// Gets the optional display height in terminal cells.
    /// </summary>
    /// <remarks>
    /// Structured pixels are resampled to this height. Pre-encoded content must
    /// already have this natural height under the active Sixel protocol metrics.
    /// </remarks>
    public int? Height { get; init; }

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as SixelNode ?? new SixelNode();
        if (Pixels is not null)
        {
            node.SetPixels(Pixels);
        }
        else
        {
            node.SetEncodedImage(ImageData!);
        }
        node.RequestedWidth = Width;
        node.RequestedHeight = Height;
        node.Fallback = await context.ReconcileChildAsync(node.Fallback, Fallback, node);
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(SixelNode);

    private static int? ValidateDimension(int? value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Sixel dimensions must be greater than zero.");
        }

        return value;
    }
}
