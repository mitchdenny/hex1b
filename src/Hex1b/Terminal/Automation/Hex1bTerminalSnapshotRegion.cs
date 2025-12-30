using Hex1b.Layout;

namespace Hex1b.Terminal.Automation;

/// <summary>
/// A lightweight view into a terminal snapshot that provides localized coordinates.
/// The region translates local (0,0)-based coordinates to the absolute position in the snapshot.
/// </summary>
/// <remarks>
/// This is useful for comparing regions of the screen regardless of their absolute position,
/// such as comparing a rendered widget to a known baseline.
/// </remarks>
public sealed class Hex1bTerminalSnapshotRegion : IHex1bTerminalRegion
{
    private readonly IHex1bTerminalRegion _parent;
    private readonly Rect _bounds;

    internal Hex1bTerminalSnapshotRegion(IHex1bTerminalRegion parent, Rect bounds)
    {
        _parent = parent;
        _bounds = bounds;
    }

    /// <inheritdoc />
    public int Width => _bounds.Width;

    /// <inheritdoc />
    public int Height => _bounds.Height;

    /// <summary>
    /// The absolute bounds of this region within the parent.
    /// </summary>
    public Rect Bounds => _bounds;

    /// <inheritdoc />
    public TerminalCell GetCell(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return TerminalCell.Empty;
        
        return _parent.GetCell(_bounds.X + x, _bounds.Y + y);
    }

    /// <inheritdoc />
    public Hex1bTerminalSnapshotRegion GetRegion(Rect bounds)
    {
        // Translate the bounds relative to our position
        var translatedBounds = new Rect(
            _bounds.X + bounds.X,
            _bounds.Y + bounds.Y,
            bounds.Width,
            bounds.Height
        );
        return new Hex1bTerminalSnapshotRegion(_parent, translatedBounds);
    }
}
