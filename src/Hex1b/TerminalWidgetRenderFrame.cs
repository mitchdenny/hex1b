using Hex1b.Automation;
using Hex1b.Kgp;
using Hex1b.Sixel;

namespace Hex1b;

/// <summary>
/// A retained embedded-terminal viewport with no disposable snapshot ownership.
/// </summary>
internal sealed class TerminalWidgetRenderFrame
{
    internal TerminalWidgetRenderFrame(Hex1bTerminalSnapshot snapshot, int scrollbackCount)
    {
        Width = snapshot.Width;
        Height = snapshot.Height - snapshot.ScrollbackLineCount;
        ScrollbackOffset = snapshot.ScrollbackLineCount;
        ScrollbackCount = scrollbackCount;
        Cursor = (snapshot.CursorX, snapshot.CursorY, (CursorShape)snapshot.CursorShape, snapshot.CursorVisible);
        CellPixelWidth = snapshot.CellPixelWidth;
        CellPixelHeight = snapshot.CellPixelHeight;
        KgpPlacements = snapshot.KgpPlacements;
        KgpImages = snapshot.KgpImages;
        SixelPlacements = snapshot.SixelPlacements;
        Cells = new TerminalCell[Height, Width];
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                // TerminalNode renders cell styling, not hyperlink metadata. Keep the
                // retained frame free of tracked owners after the capture is disposed.
                Cells[y, x] = snapshot.GetCell(x, y) with { TrackedHyperlink = null };
    }

    internal int Width { get; }
    internal int Height { get; }
    internal int ScrollbackOffset { get; }
    internal int ScrollbackCount { get; }
    internal TerminalCell[,] Cells { get; }
    internal (int X, int Y, CursorShape Shape, bool Visible) Cursor { get; }
    internal int CellPixelWidth { get; }
    internal int CellPixelHeight { get; }
    internal IReadOnlyList<KgpPlacement> KgpPlacements { get; }
    internal IReadOnlyDictionary<uint, KgpImageData> KgpImages { get; }
    internal IReadOnlyList<SixelPlacement> SixelPlacements { get; }

    private (int Width, int Height, SixelCellMetrics Metrics)? _sixelKey;
    private TerminalWidgetSixelFrame? _sixelFrame;

    internal TerminalWidgetSixelFrame? GetSixelFrame(int width, int height, SixelCellMetrics metrics)
    {
        var key = (width, height, metrics);
        if (_sixelKey != key)
        {
            _sixelFrame = TerminalWidgetSixelFrame.Create(SixelPlacements, width, height, metrics);
            _sixelKey = key;
        }
        return _sixelFrame;
    }
}
