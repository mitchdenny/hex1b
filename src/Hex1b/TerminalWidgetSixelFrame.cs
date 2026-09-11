using Hex1b.Layout;
using Hex1b.Sixel;
using Hex1b.Surfaces;

namespace Hex1b;

internal sealed record TerminalWidgetSixelFrame(SixelData Image, Rect Bounds)
{
    internal static TerminalWidgetSixelFrame? Create(
        IReadOnlyList<SixelPlacement> placements, int width, int height, SixelCellMetrics metrics)
    {
        var visible = new List<SixelPlacement>();
        var left = width;
        var top = height;
        var right = 0;
        var bottom = 0;
        foreach (var placement in placements)
        {
            var clipped = placement.ClipToCellRectangle(0, height, 0, width);
            if (clipped is null || !clipped.HasVisiblePaintedCells || clipped.Image.Raster.Image is null)
                continue;
            visible.Add(clipped);
            left = Math.Min(left, clipped.PaintedLeft);
            top = Math.Min(top, clipped.PaintedTop);
            right = Math.Max(right, clipped.PaintedRight + 1);
            bottom = Math.Max(bottom, clipped.PaintedBottom + 1);
        }
        if (visible.Count == 0)
            return null;

        var bounds = new Rect(left, top, right - left, bottom - top);
        var pixelWidth = metrics.GetPixelWidthForColumns(bounds.Width);
        var pixelHeight = metrics.GetPixelHeightForRows(bounds.Height);
        if ((long)pixelWidth * pixelHeight > SixelCompatibilityPolicy.Default.MaximumRasterPixels)
            throw new InvalidOperationException("The embedded terminal's Sixel viewport exceeds the raster pixel limit.");
        var pixels = new SixelPixelBuffer(pixelWidth, pixelHeight);

        // A Surface has one Sixel anchor per cell, but several transparent child
        // placements can share an anchor. Flatten their decoded pixels in paint
        // order, not their payloads (which may depend on earlier palette writes).
        foreach (var placement in visible.OrderBy(p => p.Sequence))
        {
            var source = placement.Image.Raster.Image!;
            var sourceMetrics = placement.Image.CellMetrics;
            var sourceX = Math.Min(source.Width, sourceMetrics.GetPixelForColumnBoundary(placement.PaintedColumnOffset));
            var sourceY = Math.Min(source.Height, sourceMetrics.GetPixelForRowBoundary(placement.PaintedRowOffset));
            var sourceRight = (int)Math.Min(source.Width, Math.Ceiling(
                (placement.PaintedColumnOffset + placement.PaintedColumnCount) * sourceMetrics.SafeWidth));
            var sourceBottom = (int)Math.Min(source.Height, Math.Ceiling(
                (placement.PaintedRowOffset + placement.PaintedRowCount) * sourceMetrics.SafeHeight));
            var destinationX = metrics.GetPixelForColumnBoundary(placement.PaintedLeft - left);
            var destinationY = metrics.GetPixelForRowBoundary(placement.PaintedTop - top);
            var copyWidth = Math.Min(sourceRight - sourceX, pixelWidth - destinationX);
            var copyHeight = Math.Min(sourceBottom - sourceY, pixelHeight - destinationY);
            for (var y = 0; y < copyHeight; y++)
            {
                for (var x = 0; x < copyWidth; x++)
                {
                    var pixel = source[sourceX + x, sourceY + y];
                    if (pixel.A != 0 && !placement.IsPixelDamaged(sourceX + x, sourceY + y))
                        pixels[destinationX + x, destinationY + y] = pixel;
                }
            }
        }

        return new TerminalWidgetSixelFrame(
            SixelData.FromExactPixels(pixels, bounds.Width, bounds.Height, metrics), bounds);
    }
}
