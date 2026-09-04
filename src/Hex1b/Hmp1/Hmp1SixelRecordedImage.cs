using Hex1b.Sixel;

namespace Hex1b;

/// <summary>
/// A decoded Sixel image record from a <see cref="Hmp1SixelRecording"/>.
/// </summary>
internal sealed class Hmp1SixelRecordedImage(
    byte[] contentHash,
    bool isGeometryOnly,
    int declaredPixelWidth,
    int declaredPixelHeight,
    int widthInCells,
    int heightInCells,
    SixelRasterStatus rasterStatus,
    string payload)
{
    /// <summary>The image's content hash, as captured from <see cref="SixelData.ContentHash"/>.</summary>
    public byte[] ContentHash { get; } = contentHash;

    /// <summary>Whether this image carries no decoded pixels.</summary>
    public bool IsGeometryOnly { get; } = isGeometryOnly;

    /// <summary>The declared pixel width, or zero when none was declared.</summary>
    public int DeclaredPixelWidth { get; } = declaredPixelWidth;

    /// <summary>The declared pixel height, or zero when none was declared.</summary>
    public int DeclaredPixelHeight { get; } = declaredPixelHeight;

    /// <summary>The image width in cells.</summary>
    public int WidthInCells { get; } = widthInCells;

    /// <summary>The image height in cells.</summary>
    public int HeightInCells { get; } = heightInCells;

    /// <summary>The raster outcome captured at recording time.</summary>
    public SixelRasterStatus RasterStatus { get; } = rasterStatus;

    /// <summary>
    /// The self-contained Sixel DCS payload for this image: a fresh
    /// <see cref="Sixel.SixelExactEncoder"/> re-encoding of the decoded pixels for
    /// rasterized images, or the original payload verbatim for geometry-only images.
    /// </summary>
    public string Payload { get; } = payload;
}
