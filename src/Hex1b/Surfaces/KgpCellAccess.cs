namespace Hex1b.Surfaces;

/// <summary>
/// Provides read-only access to KGP image metadata at a specific cell position.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="SixelPixelAccess"/>, KGP images don't store pixel data in-memory —
/// the pixel data is transmitted to the terminal. This type provides metadata about the
/// KGP image covering a cell position, including the image ID, source dimensions, clip
/// region, and the cell's offset within the image.
/// </para>
/// <para>
/// This enables computed layers to make decisions based on KGP image presence and position
/// (e.g., tinting text that overlaps a KGP background, or adjusting opacity).
/// </para>
/// </remarks>
public readonly struct KgpCellAccess
{
    private readonly KgpCellData? _data;

    /// <summary>
    /// Gets whether this accessor has valid KGP data.
    /// </summary>
    public bool IsValid => _data is not null;

    /// <summary>
    /// Gets the KGP image ID.
    /// </summary>
    public uint ImageId => _data?.ImageId ?? 0;

    /// <summary>
    /// Gets the source image width in pixels.
    /// </summary>
    public uint SourcePixelWidth => _data?.SourcePixelWidth ?? 0;

    /// <summary>
    /// Gets the source image height in pixels.
    /// </summary>
    public uint SourcePixelHeight => _data?.SourcePixelHeight ?? 0;

    /// <summary>
    /// Gets the width of the KGP image in terminal columns.
    /// </summary>
    public int WidthInCells => _data?.WidthInCells ?? 0;

    /// <summary>
    /// Gets the height of the KGP image in terminal rows.
    /// </summary>
    public int HeightInCells => _data?.HeightInCells ?? 0;

    /// <summary>
    /// Gets the z-index of the KGP placement (negative = below text, positive = above text).
    /// </summary>
    public int ZIndex => _data?.ZIndex ?? 0;

    /// <summary>
    /// Gets the cell X offset within the KGP image (0 = leftmost cell of the image).
    /// </summary>
    public int CellOffsetX { get; }

    /// <summary>
    /// Gets the cell Y offset within the KGP image (0 = topmost cell of the image).
    /// </summary>
    public int CellOffsetY { get; }

    /// <summary>
    /// Gets whether this cell is at the anchor position (top-left) of the KGP image.
    /// </summary>
    public bool IsAnchor => CellOffsetX == 0 && CellOffsetY == 0;

    /// <summary>
    /// Gets the underlying KGP cell data, or null if this accessor is invalid.
    /// </summary>
    public KgpCellData? Data => _data;

    internal KgpCellAccess(KgpCellData data, int cellOffsetX, int cellOffsetY)
    {
        _data = data;
        CellOffsetX = cellOffsetX;
        CellOffsetY = cellOffsetY;
    }
}
