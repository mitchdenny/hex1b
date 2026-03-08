namespace Hex1b.Kgp;

/// <summary>
/// Represents a visible rectangular portion of a KGP image after occlusion computation.
/// Each fragment maps to a single KGP placement command with source-rect cropping.
/// </summary>
/// <param name="ImageId">The KGP image ID (shared across all fragments of the same image).</param>
/// <param name="AbsoluteX">X position on the terminal (0-based column).</param>
/// <param name="AbsoluteY">Y position on the terminal (0-based row).</param>
/// <param name="CellWidth">Width of this fragment in terminal columns.</param>
/// <param name="CellHeight">Height of this fragment in terminal rows.</param>
/// <param name="ClipX">Pixel X offset into the source image.</param>
/// <param name="ClipY">Pixel Y offset into the source image.</param>
/// <param name="ClipW">Pixel width of the visible source region.</param>
/// <param name="ClipH">Pixel height of the visible source region.</param>
/// <param name="Data">Reference to the original KGP cell data (for transmit payload, z-index, etc.).</param>
public readonly record struct KgpFragment(
    uint ImageId,
    int AbsoluteX,
    int AbsoluteY,
    int CellWidth,
    int CellHeight,
    int ClipX,
    int ClipY,
    int ClipW,
    int ClipH,
    KgpCellData Data);
