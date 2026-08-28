using System.Text;
using Hex1b.Theming;

namespace Hex1b;

internal static class KgpUnicodePlaceholder
{
    internal const int CodePoint = 0x10EEEE;

    private readonly record struct IncompletePlacement(
        uint ImageIdLow,
        uint PlacementId,
        int? Row,
        int? Column,
        byte? ImageIdHigh);

    private struct PlacementRun
    {
        internal uint ImageIdLow;
        internal uint PlacementId;
        internal int Row;
        internal int Column;
        internal byte ImageIdHigh;
        internal int ScreenColumn;
        internal int Width;
    }

    internal static bool IsPlaceholder(string? character)
    {
        if (string.IsNullOrEmpty(character))
            return false;

        var status = Rune.DecodeFromUtf16(
            character.AsSpan(),
            out var rune,
            out _);
        return status == System.Buffers.OperationStatus.Done &&
            rune.Value == CodePoint;
    }

    internal static void MaterializeRow(
        ReadOnlySpan<TerminalCell> cells,
        int snapshotRow,
        IReadOnlyList<KgpVirtualPlacement> prototypes,
        IReadOnlyDictionary<uint, KgpImageData> images,
        int cellPixelWidth,
        int cellPixelHeight,
        List<KgpPlacement> destination)
    {
        PlacementRun? current = null;
        for (var column = 0; column < cells.Length; column++)
        {
            if (!TryDecode(cells[column], out var decoded))
            {
                FinalizeRun(
                    current,
                    snapshotRow,
                    prototypes,
                    images,
                    cellPixelWidth,
                    cellPixelHeight,
                    destination);
                current = null;
                continue;
            }

            if (current is { } run && CanAppend(run, decoded))
            {
                run.Width++;
                current = run;
                continue;
            }

            FinalizeRun(
                current,
                snapshotRow,
                prototypes,
                images,
                cellPixelWidth,
                cellPixelHeight,
                destination);
            current = new PlacementRun
            {
                ImageIdLow = decoded.ImageIdLow,
                PlacementId = decoded.PlacementId,
                Row = decoded.Row ?? 0,
                Column = decoded.Column ?? 0,
                ImageIdHigh = decoded.ImageIdHigh ?? 0,
                ScreenColumn = column,
                Width = 1,
            };
        }

        FinalizeRun(
            current,
            snapshotRow,
            prototypes,
            images,
            cellPixelWidth,
            cellPixelHeight,
            destination);
    }

    internal static void CollectOrigins(
        ReadOnlySpan<TerminalCell> cells,
        int absoluteRow,
        IReadOnlyList<KgpVirtualPlacement> prototypes,
        IReadOnlyDictionary<uint, KgpImageData> images,
        int cellPixelWidth,
        int cellPixelHeight,
        Dictionary<long, (int Row, int Column)> origins)
    {
        PlacementRun? current = null;
        for (var column = 0; column < cells.Length; column++)
        {
            if (!TryDecode(cells[column], out var decoded))
            {
                FinalizeOrigin(
                    current,
                    absoluteRow,
                    prototypes,
                    images,
                    cellPixelWidth,
                    cellPixelHeight,
                    origins);
                current = null;
                continue;
            }

            if (current is { } run && CanAppend(run, decoded))
            {
                run.Width++;
                current = run;
                continue;
            }

            FinalizeOrigin(
                current,
                absoluteRow,
                prototypes,
                images,
                cellPixelWidth,
                cellPixelHeight,
                origins);
            current = new PlacementRun
            {
                ImageIdLow = decoded.ImageIdLow,
                PlacementId = decoded.PlacementId,
                Row = decoded.Row ?? 0,
                Column = decoded.Column ?? 0,
                ImageIdHigh = decoded.ImageIdHigh ?? 0,
                ScreenColumn = column,
                Width = 1,
            };
        }

        FinalizeOrigin(
            current,
            absoluteRow,
            prototypes,
            images,
            cellPixelWidth,
            cellPixelHeight,
            origins);
    }

    private static bool TryDecode(
        TerminalCell cell,
        out IncompletePlacement placement)
    {
        placement = default;
        if (!IsPlaceholder(cell.Character))
            return false;

        int? row = null;
        int? column = null;
        byte? imageIdHigh = null;
        var runes = cell.Character.EnumerateRunes().GetEnumerator();
        _ = runes.MoveNext();
        for (var position = 0; position < 3 && runes.MoveNext(); position++)
        {
            if (!KgpUnicodePlaceholderDiacritics.TryGetIndex(
                    runes.Current,
                    out var index))
            {
                continue;
            }

            switch (position)
            {
                case 0:
                    row = index;
                    break;
                case 1:
                    column = index;
                    break;
                case 2 when index <= byte.MaxValue:
                    imageIdHigh = (byte)index;
                    break;
            }
        }

        var placementId = ColorToId(cell.UnderlineColor);
        placement = new IncompletePlacement(
            ColorToId(cell.Foreground),
            placementId,
            row,
            column,
            imageIdHigh);
        return true;
    }

    private static bool CanAppend(
        PlacementRun current,
        IncompletePlacement next)
        => current.ImageIdLow == next.ImageIdLow &&
           current.PlacementId == next.PlacementId &&
           (next.Row is null || next.Row.Value == current.Row) &&
           (next.Column is null ||
            next.Column.Value == checked(current.Column + current.Width)) &&
           (next.ImageIdHigh is null ||
            next.ImageIdHigh.Value == current.ImageIdHigh);

    private static uint ColorToId(Hex1bColor? color)
    {
        if (color is not { IsDefault: false } value)
            return 0;

        return value.Kind switch
        {
            Hex1bColorKind.Standard => value.AnsiIndex,
            Hex1bColorKind.Bright => checked((uint)value.AnsiIndex + 8),
            Hex1bColorKind.Indexed => value.AnsiIndex,
            _ => (uint)(value.R << 16 | value.G << 8 | value.B),
        };
    }

    private static void FinalizeRun(
        PlacementRun? run,
        int snapshotRow,
        IReadOnlyList<KgpVirtualPlacement> prototypes,
        IReadOnlyDictionary<uint, KgpImageData> images,
        int cellPixelWidth,
        int cellPixelHeight,
        List<KgpPlacement> destination)
    {
        if (run is not { } value)
            return;

        if (!TryResolveRun(
                value,
                prototypes,
                images,
                cellPixelWidth,
                cellPixelHeight,
                out var prototype,
                out var image,
                out var columns,
                out var rows,
                out var visibleRunWidth))
            return;

        var placement = CreateFragment(
            value,
            snapshotRow,
            prototype,
            image,
            columns,
            rows,
            visibleRunWidth,
            cellPixelWidth,
            cellPixelHeight);
        if (placement is not null)
            destination.Add(placement);
    }

    private static void FinalizeOrigin(
        PlacementRun? run,
        int absoluteRow,
        IReadOnlyList<KgpVirtualPlacement> prototypes,
        IReadOnlyDictionary<uint, KgpImageData> images,
        int cellPixelWidth,
        int cellPixelHeight,
        Dictionary<long, (int Row, int Column)> origins)
    {
        if (run is not { } value ||
            !TryResolveRun(
                value,
                prototypes,
                images,
                cellPixelWidth,
                cellPixelHeight,
                out var prototype,
                out var image,
                out var columns,
                out var rows,
                out var visibleRunWidth) ||
            CreateFragment(
                value,
                absoluteRow,
                prototype,
                image,
                columns,
                rows,
                visibleRunWidth,
                cellPixelWidth,
                cellPixelHeight) is not { } fragment)
        {
            return;
        }

        var geometry = fragment.RenderGeometry!.Value;
        var realizedRow = checked(
            fragment.Row + (int)Math.Floor(geometry.ClipOffsetYInCells));
        var realizedColumn = checked(
            fragment.Column + (int)Math.Floor(geometry.ClipOffsetXInCells));
        if (origins.TryGetValue(prototype.GraphId, out var origin))
        {
            origins[prototype.GraphId] = (
                Math.Min(origin.Row, realizedRow),
                Math.Min(origin.Column, realizedColumn));
        }
        else
        {
            origins.Add(prototype.GraphId, (realizedRow, realizedColumn));
        }
    }

    private static KgpVirtualPlacement? FindPrototype(
        IReadOnlyList<KgpVirtualPlacement> prototypes,
        uint imageId,
        uint placementId)
    {
        KgpVirtualPlacement? selected = null;
        foreach (var prototype in prototypes)
        {
            if (prototype.ImageId != imageId ||
                (placementId > 0 && prototype.PlacementId != placementId))
            {
                continue;
            }

            if (selected is null ||
                prototype.CreationOrdinal < selected.CreationOrdinal)
            {
                selected = prototype;
            }
        }

        return selected;
    }

    private static KgpPlacement? CreateFragment(
        PlacementRun run,
        int snapshotRow,
        KgpVirtualPlacement prototype,
        KgpImageData image,
        uint columns,
        uint rows,
        int visibleRunWidth,
        int cellPixelWidth,
        int cellPixelHeight)
    {
        var boxWidth = (double)columns * cellPixelWidth;
        var boxHeight = (double)rows * cellPixelHeight;
        var scale = Math.Min(boxWidth / image.Width, boxHeight / image.Height);
        if (!double.IsFinite(scale) || scale <= 0)
            return null;

        var scaledWidth = image.Width * scale;
        var scaledHeight = image.Height * scale;
        var imageLeft = (boxWidth - scaledWidth) / 2;
        var imageTop = (boxHeight - scaledHeight) / 2;
        var imageRight = imageLeft + scaledWidth;
        var imageBottom = imageTop + scaledHeight;

        var runLeft = (double)run.Column * cellPixelWidth;
        var runTop = (double)run.Row * cellPixelHeight;
        var runRight = runLeft + (double)visibleRunWidth * cellPixelWidth;
        var runBottom = runTop + cellPixelHeight;
        var visibleLeft = Math.Max(runLeft, imageLeft);
        var visibleTop = Math.Max(runTop, imageTop);
        var visibleRight = Math.Min(runRight, imageRight);
        var visibleBottom = Math.Min(runBottom, imageBottom);
        if (visibleLeft >= visibleRight || visibleTop >= visibleBottom)
            return null;

        var sourceLeft = ClampToUInt(
            Math.Floor((visibleLeft - imageLeft) / scale),
            image.Width);
        var sourceTop = ClampToUInt(
            Math.Floor((visibleTop - imageTop) / scale),
            image.Height);
        var sourceRight = ClampToUInt(
            Math.Ceiling((visibleRight - imageLeft) / scale),
            image.Width);
        var sourceBottom = ClampToUInt(
            Math.Ceiling((visibleBottom - imageTop) / scale),
            image.Height);
        if (sourceLeft >= sourceRight || sourceTop >= sourceBottom)
            return null;

        var geometry = new KgpPlacementRenderGeometry(
            (visibleLeft - runLeft) / cellPixelWidth,
            (visibleTop - runTop) / cellPixelHeight,
            (visibleRight - visibleLeft) / cellPixelWidth,
            (visibleBottom - visibleTop) / cellPixelHeight,
            (imageLeft - runLeft) / cellPixelWidth,
            (imageTop - runTop) / cellPixelHeight,
            scaledWidth / cellPixelWidth,
            scaledHeight / cellPixelHeight);
        return new KgpPlacement(
            image.ImageId,
            prototype.PlacementId,
            snapshotRow,
            run.ScreenColumn,
            checked((uint)visibleRunWidth),
            1,
            sourceLeft,
            sourceTop,
            sourceRight - sourceLeft,
            sourceBottom - sourceTop,
            zIndex: -1,
            cellOffsetX: 0,
            cellOffsetY: 0,
            geometry,
            graphId: prototype.GraphId);
    }

    private static bool TryResolveRun(
        PlacementRun run,
        IReadOnlyList<KgpVirtualPlacement> prototypes,
        IReadOnlyDictionary<uint, KgpImageData> images,
        int cellPixelWidth,
        int cellPixelHeight,
        out KgpVirtualPlacement prototype,
        out KgpImageData image,
        out uint columns,
        out uint rows,
        out int visibleRunWidth)
    {
        prototype = null!;
        image = null!;
        columns = 0;
        rows = 0;
        visibleRunWidth = 0;

        var imageId = run.ImageIdLow | ((uint)run.ImageIdHigh << 24);
        if (imageId == 0 ||
            !images.TryGetValue(imageId, out var foundImage) ||
            FindPrototype(prototypes, imageId, run.PlacementId) is not { } found ||
            foundImage.Width == 0 ||
            foundImage.Height == 0 ||
            cellPixelWidth <= 0 ||
            cellPixelHeight <= 0)
        {
            return false;
        }

        prototype = found;
        image = foundImage;
        columns = prototype.Columns > 0
            ? prototype.Columns
            : DivideCeiling(image.Width, checked((uint)cellPixelWidth));
        rows = prototype.Rows > 0
            ? prototype.Rows
            : DivideCeiling(image.Height, checked((uint)cellPixelHeight));
        if (columns == 0 ||
            rows == 0 ||
            run.Row < 0 ||
            run.Column < 0 ||
            (uint)run.Row >= rows ||
            (uint)run.Column >= columns)
        {
            return false;
        }

        var availableColumns = (ulong)columns - (uint)run.Column;
        visibleRunWidth = checked((int)Math.Min((ulong)run.Width, availableColumns));
        return visibleRunWidth > 0;
    }

    private static uint ClampToUInt(double value, uint maximum)
        => checked((uint)Math.Clamp(value, 0, maximum));

    private static uint DivideCeiling(uint value, uint divisor)
        => checked((uint)(((ulong)value + divisor - 1) / divisor));
}
