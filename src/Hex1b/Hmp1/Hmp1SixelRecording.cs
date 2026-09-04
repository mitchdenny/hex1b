using System.Text;
using Hex1b.Sixel;

namespace Hex1b;

/// <summary>
/// Serializes and deserializes a versioned, explicit binary representation of
/// viewport Sixel placements — independent of the plain-bytes escape sequences
/// <see cref="Hmp1SixelStateReplay"/> writes directly to a live HMP1 peer's stream.
/// </summary>
/// <remarks>
/// <para>
/// This format exists to satisfy record/serialize/replay/compare scenarios that
/// need explicit, testable failure modes (unsupported version, truncation,
/// missing image references, invalid geometry, resource-limit violations) —
/// none of which apply to <see cref="Hmp1SixelStateReplay"/>, which only ever
/// emits plain terminal bytes interpreted by the existing, already-robust live
/// Sixel parser.
/// </para>
/// <para>
/// Images are content-addressed and deduplicated by <see cref="SixelData.ContentHash"/>:
/// multiple placements sharing a raster reference the same image table entry, so
/// pixel payloads are never repeated within a single recording (unlike the live
/// wire replay, where the Sixel protocol has no "reuse an existing image at a new
/// position" primitive).
/// </para>
/// </remarks>
internal static class Hmp1SixelRecording
{
    private static readonly byte[] Magic = "SXRC"u8.ToArray();

    internal const int CurrentVersion = 1;
    internal const int MaxPlacementCount = 4096;
    internal const int MaxImageCount = 4096;
    internal const int MaxPayloadLength = 64 * 1024 * 1024;
    internal const int MaxDamagedCellCount = 1 << 20;

    /// <summary>
    /// Serializes the given viewport placements into a versioned recording.
    /// </summary>
    internal static byte[] Serialize(IReadOnlyList<SixelPlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);

        if (placements.Count > MaxPlacementCount)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                $"Placement count {placements.Count} exceeds the limit of {MaxPlacementCount}.");
        }

        var imageIndexByHash = new Dictionary<byte[], int>(SixelContentHashComparer.Instance);
        var images = new List<SixelData>();
        foreach (var placement in placements)
        {
            if (!imageIndexByHash.ContainsKey(placement.Image.ContentHash))
            {
                imageIndexByHash[placement.Image.ContentHash] = images.Count;
                images.Add(placement.Image);
            }
        }

        if (images.Count > MaxImageCount)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                $"Image count {images.Count} exceeds the limit of {MaxImageCount}.");
        }

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(CurrentVersion);
            writer.Write(placements.Count);
            writer.Write(images.Count);

            foreach (var image in images)
            {
                WriteImage(writer, image);
            }

            foreach (var placement in placements)
            {
                WritePlacement(writer, placement, imageIndexByHash[placement.Image.ContentHash]);
            }
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Deserializes and strictly validates a recording produced by
    /// <see cref="Serialize"/>. Throws <see cref="Hmp1SixelRecordingException"/> for
    /// every failure mode rather than returning a success-shaped partial result.
    /// </summary>
    internal static Hmp1SixelRecordingSnapshot Deserialize(ReadOnlyMemory<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var magic = ReadExact(reader, Magic.Length);
        if (!magic.AsSpan().SequenceEqual(Magic))
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.Malformed,
                "Recording is missing the expected format marker.");
        }

        var version = ReadInt32(reader);
        if (version != CurrentVersion)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.UnsupportedVersion,
                $"Recording version {version} is not supported. Supported version: {CurrentVersion}.");
        }

        var placementCount = ReadInt32(reader);
        var imageCount = ReadInt32(reader);
        ValidateCount(placementCount, MaxPlacementCount, "placement");
        ValidateCount(imageCount, MaxImageCount, "image");

        var images = new List<Hmp1SixelRecordedImage>(imageCount);
        for (var i = 0; i < imageCount; i++)
        {
            images.Add(ReadImage(reader));
        }

        var placements = new List<Hmp1SixelRecordedPlacement>(placementCount);
        for (var i = 0; i < placementCount; i++)
        {
            placements.Add(ReadPlacement(reader, imageCount));
        }

        return new Hmp1SixelRecordingSnapshot(version, images, placements);
    }

    private static void WriteImage(BinaryWriter writer, SixelData image)
    {
        var payload = image.RasterStatus == SixelRasterStatus.GeometryOnly
            ? image.Payload
            : EncodeRasterizedPayload(image);

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        if (payloadBytes.Length > MaxPayloadLength)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                $"Image payload length {payloadBytes.Length} exceeds the limit of {MaxPayloadLength}.");
        }

        writer.Write(image.ContentHash);
        writer.Write(image.RasterStatus == SixelRasterStatus.GeometryOnly);
        writer.Write(image.PixelWidth);
        writer.Write(image.PixelHeight);
        writer.Write(image.WidthInCells);
        writer.Write(image.HeightInCells);
        writer.Write((byte)image.RasterStatus);
        writer.Write(payloadBytes.Length);
        writer.Write(payloadBytes);
    }

    private static string EncodeRasterizedPayload(SixelData image)
    {
        var pixels = image.GetPixels();

        // A non-geometry-only image should always have decoded pixels. Fall back to
        // the original payload defensively rather than dropping the image.
        return pixels is null
            ? image.Payload
            : SixelExactEncoder.Encode(pixels) ?? image.Payload;
    }

    private static void WritePlacement(BinaryWriter writer, SixelPlacement placement, int imageIndex)
    {
        writer.Write(imageIndex);
        writer.Write(placement.Row);
        writer.Write(placement.Column);
        writer.Write(placement.WidthInCells);
        writer.Write(placement.HeightInCells);
        writer.Write(placement.PaintedRowOffset);
        writer.Write(placement.PaintedRowCount);
        writer.Write(placement.PaintedColumnOffset);
        writer.Write(placement.PaintedColumnCount);
        writer.Write(placement.Sequence);
        writer.Write(placement.CreatedAt.ToUnixTimeMilliseconds());

        var damagedCells = new List<(int Row, int Column)>();
        for (var row = 0; row < placement.PaintedRowCount; row++)
        {
            for (var col = 0; col < placement.PaintedColumnCount; col++)
            {
                var absoluteRow = placement.PaintedTop + row;
                var absoluteColumn = placement.PaintedLeft + col;
                if (placement.IsCellDamaged(absoluteRow, absoluteColumn))
                {
                    // Store anchor-relative offsets, matching SixelPlacement's own
                    // damaged-cell key convention, so damage stays consistent if the
                    // placement is later shifted (e.g. replayed at a different anchor).
                    damagedCells.Add((absoluteRow - placement.Row, absoluteColumn - placement.Column));
                }
            }
        }

        if (damagedCells.Count > MaxDamagedCellCount)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                $"Damaged cell count {damagedCells.Count} exceeds the limit of {MaxDamagedCellCount}.");
        }

        writer.Write(damagedCells.Count);
        foreach (var (row, col) in damagedCells)
        {
            writer.Write(row);
            writer.Write(col);
        }
    }

    private static Hmp1SixelRecordedImage ReadImage(BinaryReader reader)
    {
        var contentHash = ReadExact(reader, 32);
        var isGeometryOnly = ReadBool(reader);
        var declaredPixelWidth = ReadInt32(reader);
        var declaredPixelHeight = ReadInt32(reader);
        var widthInCells = ReadInt32(reader);
        var heightInCells = ReadInt32(reader);
        var rasterStatusByte = ReadByte(reader);
        var payloadLength = ReadInt32(reader);

        if (payloadLength < 0 || payloadLength > MaxPayloadLength)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                $"Image payload length {payloadLength} is invalid or exceeds the limit of {MaxPayloadLength}.");
        }

        var payloadBytes = ReadExact(reader, payloadLength);

        if (declaredPixelWidth < 0 || declaredPixelHeight < 0 || widthInCells <= 0 || heightInCells <= 0)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.InvalidGeometry,
                $"Image declares invalid geometry (pixels {declaredPixelWidth}x{declaredPixelHeight}, cells {widthInCells}x{heightInCells}).");
        }

        if (!Enum.IsDefined(typeof(SixelRasterStatus), (int)rasterStatusByte))
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.Malformed,
                $"Image declares an unrecognized raster status value {rasterStatusByte}.");
        }

        return new Hmp1SixelRecordedImage(
            contentHash,
            isGeometryOnly,
            declaredPixelWidth,
            declaredPixelHeight,
            widthInCells,
            heightInCells,
            (SixelRasterStatus)rasterStatusByte,
            Encoding.UTF8.GetString(payloadBytes));
    }

    private static Hmp1SixelRecordedPlacement ReadPlacement(BinaryReader reader, int imageCount)
    {
        var imageIndex = ReadInt32(reader);
        var row = ReadInt32(reader);
        var column = ReadInt32(reader);
        var widthInCells = ReadInt32(reader);
        var heightInCells = ReadInt32(reader);
        var paintedRowOffset = ReadInt32(reader);
        var paintedRowCount = ReadInt32(reader);
        var paintedColumnOffset = ReadInt32(reader);
        var paintedColumnCount = ReadInt32(reader);
        var sequence = ReadInt64(reader);
        var createdAtUnixMs = ReadInt64(reader);
        var damagedCellCount = ReadInt32(reader);

        if (imageIndex < 0 || imageIndex >= imageCount)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.MissingImageReference,
                $"Placement references image index {imageIndex}, but the recording only has {imageCount} image(s).");
        }

        if (widthInCells <= 0 || heightInCells <= 0 || paintedRowCount < 0 || paintedColumnCount < 0)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.InvalidGeometry,
                $"Placement declares invalid geometry (cells {widthInCells}x{heightInCells}, painted {paintedRowCount}x{paintedColumnCount}).");
        }

        if (damagedCellCount < 0 || damagedCellCount > MaxDamagedCellCount)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                $"Damaged cell count {damagedCellCount} is invalid or exceeds the limit of {MaxDamagedCellCount}.");
        }

        var damagedCells = new List<(int Row, int Column)>(damagedCellCount);
        for (var i = 0; i < damagedCellCount; i++)
        {
            var damagedRow = ReadInt32(reader);
            var damagedCol = ReadInt32(reader);
            damagedCells.Add((damagedRow, damagedCol));
        }

        return new Hmp1SixelRecordedPlacement(
            imageIndex,
            row,
            column,
            widthInCells,
            heightInCells,
            paintedRowOffset,
            paintedRowCount,
            paintedColumnOffset,
            paintedColumnCount,
            sequence,
            DateTimeOffset.FromUnixTimeMilliseconds(createdAtUnixMs),
            damagedCells);
    }

    private static void ValidateCount(int count, int max, string what)
    {
        if (count < 0)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.Malformed,
                $"Recording declares a negative {what} count ({count}).");
        }

        if (count > max)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                $"Recording declares {count} {what}(s), exceeding the limit of {max}.");
        }
    }

    private static byte[] ReadExact(BinaryReader reader, int count)
    {
        try
        {
            var bytes = reader.ReadBytes(count);
            if (bytes.Length != count)
            {
                throw new Hmp1SixelRecordingException(
                    Hmp1SixelRecordingFailureReason.Truncated,
                    $"Expected {count} byte(s) but only {bytes.Length} remained.");
            }

            return bytes;
        }
        catch (EndOfStreamException)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.Truncated,
                "Recording ended before all declared data could be read.");
        }
    }

    private static int ReadInt32(BinaryReader reader) => ReadPrimitive(reader, r => r.ReadInt32());
    private static long ReadInt64(BinaryReader reader) => ReadPrimitive(reader, r => r.ReadInt64());
    private static byte ReadByte(BinaryReader reader) => ReadPrimitive(reader, r => r.ReadByte());
    private static bool ReadBool(BinaryReader reader) => ReadPrimitive(reader, r => r.ReadBoolean());

    private static T ReadPrimitive<T>(BinaryReader reader, Func<BinaryReader, T> read)
    {
        try
        {
            return read(reader);
        }
        catch (EndOfStreamException)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.Truncated,
                "Recording ended before all declared data could be read.");
        }
    }
}
