using System.Runtime.CompilerServices;
using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Deterministic retained-byte accounting for one Sixel image resource.
/// </summary>
/// <remarks>
/// This intentionally counts retained protocol and pixel content rather than
/// runtime object headers or allocator overhead, whose sizes vary by runtime.
/// </remarks>
internal static class SixelRetainedSize
{
    private const int BooleanBytes = sizeof(byte);
    private const int Rgba32Bytes = 4;
    private const int ArrayHeaderBytes = 24;
    private const int StringHeaderBytes = 24;

    internal static long GetInitialBytes(SixelData image)
    {
        var total = 0L;
        Add(ref total, GetStringBytes(image.Payload));
        Add(ref total, GetArrayBytes(image.ContentHash.Length, sizeof(byte)));
        Add(ref total, GetParseResultBytes(image.ParseResult));
        Add(ref total, GetRasterPreparationBytes(image.RasterPreparation));
        Add(ref total, 4L * sizeof(int));
        Add(ref total, 2L * sizeof(double) + 2L * sizeof(int));
        Add(ref total, BooleanBytes);
        return total;
    }

    internal static long GetRasterBytes(SixelRasterResult raster)
    {
        var total = 0L;
        Add(ref total, Unsafe.SizeOf<SixelRasterResultContent>());
        Add(ref total, GetArrayBytes(raster.Identity.Length, sizeof(byte)));
        Add(ref total, GetRasterDiagnosticsBytes(raster.Diagnostics));
        Add(ref total, raster.Image?.RetainedTileBytes ?? 0);
        return total;
    }

    internal static long GetDensePixelBytes(SixelPixelBuffer pixels) =>
        GetArrayBytes((long)pixels.Width * pixels.Height, Rgba32Bytes);

    private static long GetParseResultBytes(SixelParseResult parse)
    {
        var total = 0L;
        Add(ref total, Unsafe.SizeOf<SixelParseResultContent>());
        Add(ref total, GetPaletteCommandsBytes(parse.PaletteMutations));
        Add(ref total, GetPaletteCommandsBytes(parse.FinalPaletteDefinitions));
        Add(
            ref total,
            GetCollectionBytes(
                parse.Commands,
                Unsafe.SizeOf<SixelCommand>()));
        Add(ref total, GetDiagnosticsBytes(parse.Diagnostics));
        return total;
    }

    private static long GetRasterPreparationBytes(SixelRasterPreparation? preparation)
    {
        if (preparation is null)
            return 0;

        var total = 0L;
        Add(ref total, GetArrayBytes(preparation.Identity.Length, sizeof(byte)));
        Add(ref total, Unsafe.SizeOf<SixelRasterPreparationContent>());
        Add(
            ref total,
            GetArrayBytes(preparation.Environment.Registers.Count, Rgba32Bytes));
        return total;
    }

    private static long GetPaletteCommandsBytes(IReadOnlyList<SixelPaletteCommand> commands)
        => GetCollectionBytes(commands, Unsafe.SizeOf<SixelPaletteCommand>());

    private static long GetDiagnosticsBytes(IReadOnlyList<SixelDiagnostic> diagnostics)
    {
        var total = GetCollectionBytes(
            diagnostics,
            Unsafe.SizeOf<SixelDiagnostic>());
        foreach (var diagnostic in diagnostics)
            Add(ref total, GetStringBytes(diagnostic.Message));
        return total;
    }

    private static long GetRasterDiagnosticsBytes(
        IReadOnlyList<SixelRasterDiagnostic> diagnostics)
    {
        var total = GetCollectionBytes(
            diagnostics,
            Unsafe.SizeOf<SixelRasterDiagnostic>());
        foreach (var diagnostic in diagnostics)
            Add(ref total, GetStringBytes(diagnostic.Message));
        return total;
    }

    private static long GetStringBytes(string value) =>
        AlignToEight(SaturatingAdd(
            StringHeaderBytes,
            SaturatingMultiply((long)value.Length + 1, sizeof(char))));

    private static long GetArrayBytes(long length, int elementSize) =>
        AlignToEight(
            SaturatingAdd(ArrayHeaderBytes, SaturatingMultiply(length, elementSize)));

    private static long GetCollectionBytes<T>(
        IReadOnlyList<T> values,
        int elementSize)
    {
        var capacity = values is List<T> list ? list.Capacity : values.Count;
        return GetArrayBytes(capacity, elementSize);
    }

    private static long SaturatingMultiply(long left, long right)
    {
        if (left == 0 || right == 0)
            return 0;
        return left > long.MaxValue / right ? long.MaxValue : left * right;
    }

    private static long SaturatingAdd(long left, long right) =>
        right > long.MaxValue - left ? long.MaxValue : left + right;

    private static long AlignToEight(long value) =>
        value > long.MaxValue - 7 ? long.MaxValue : (value + 7) & ~7L;

    private static void Add(ref long total, long value)
    {
        total = value > long.MaxValue - total ? long.MaxValue : total + value;
    }

    private readonly record struct SixelParseResultContent(
        SixelHeader Header,
        SixelRasterAttributes? RasterAttributes,
        SixelPoint GraphicsCursor,
        SixelPoint MaximumCommandOrDataPosition,
        SixelExtent DeclaredExtent,
        SixelExtent DataExtent,
        SixelBounds PaintedBounds,
        SixelExtent LogicalCanvasExtent,
        SixelExtent UnscaledDataExtent,
        SixelBounds UnscaledPaintedBounds,
        SixelExtent UnscaledLogicalCanvasExtent,
        int SelectedColorRegister,
        nint PaletteMutations,
        nint FinalPaletteDefinitions,
        nint Commands,
        bool CommandsComplete,
        SixelParseOutcome Outcome,
        nint Diagnostics);

    private readonly record struct SixelRasterPreparationContent(
        Rgba32 Background,
        nint Registers,
        nint Policy,
        nint Identity);

    private readonly record struct SixelRasterResultContent(
        SixelRasterStatus Status,
        SixelRasterExtents Extents,
        nint Image,
        SixelBackgroundMode BackgroundMode,
        Rgba32 UnpaintedPixel,
        nint Identity,
        nint Diagnostics);
}
