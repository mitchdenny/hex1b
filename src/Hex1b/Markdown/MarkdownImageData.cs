namespace Hex1b.Markdown;

/// <summary>
/// Decoded image data returned by a <see cref="MarkdownImageLoader"/> callback.
/// Contains raw RGBA32 pixel data and dimensions, matching what
/// <see cref="Hex1b.Widgets.KgpImageWidget"/> requires.
/// </summary>
public sealed record MarkdownImageData(byte[] ImageData, int PixelWidth, int PixelHeight);
