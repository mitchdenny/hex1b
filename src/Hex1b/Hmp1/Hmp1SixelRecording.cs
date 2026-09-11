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
/// Images are identity-addressed and deduplicated by
/// <see cref="SixelData.ContentHash"/>. Multiple placements share an image table
/// entry only when their raster state, protocol metrics, and cell span are all
/// compatible, so a recording never conflates placements whose pixels must be
/// clipped or damaged on different protocol grids.
/// </para>
/// <para>
/// Version 2 persists each image's exact protocol-metric bit patterns, source,
/// and reliability. Version 1 remains readable for compatibility, but replays
/// with the target terminal's current metrics because that version did not
/// record per-image metric context.
/// </para>
/// </remarks>
internal static class Hmp1SixelRecording
{
    private static readonly byte[] Magic = "SXRC"u8.ToArray();

    internal const int CurrentVersion = 2;
    internal const int MinimumSupportedVersion = 1;
    internal const int MaxPlacementCount = Hmp1SixelLimits.MaximumPlacementCount;
    internal const int MaxImageCount = Hmp1SixelLimits.MaximumImageCount;
    internal const int MaxPayloadLength = Hmp1SixelLimits.MaximumSequenceBytes;
    internal const int MaxTotalPayloadLength = Hmp1SixelLimits.MaximumTotalPayloadBytes;
    internal const int MaxRecordingLength = Hmp1SixelLimits.MaximumRecordingBytes;
    internal const int MaxDamagedCellCount = Hmp1SixelLimits.MaximumDamagedCellCount;

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

        long totalPayloadLength = 0;
        var encodedPayloads = new Dictionary<byte[], byte[]>(SixelContentHashComparer.Instance);
        foreach (var image in images)
        {
            if (Hmp1SixelStateReplay.HasIncompletePayload(image))
            {
                throw new Hmp1SixelRecordingException(
                    Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                    "A retention-limited Sixel image cannot be serialized because its complete replay payload is unavailable.");
            }

            var remainingPayloadBytes = (int)Math.Min(
                MaxPayloadLength,
                MaxTotalPayloadLength - totalPayloadLength);
            if (!TryEncodePayload(image, remainingPayloadBytes, out var payload))
            {
                throw new Hmp1SixelRecordingException(
                    Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                    "Image payload exceeds the remaining recording payload budget.");
            }
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            if (payloadBytes.Length > MaxPayloadLength ||
                payloadBytes.Length > MaxTotalPayloadLength - totalPayloadLength)
            {
                throw new Hmp1SixelRecordingException(
                    Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                    $"Image payload bytes exceed the per-image or aggregate recording limit ({MaxPayloadLength}/{MaxTotalPayloadLength}).");
            }

            totalPayloadLength += payloadBytes.Length;
            encodedPayloads[image.ContentHash] = payloadBytes;
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
                WriteImage(writer, image, encodedPayloads[image.ContentHash]);
            }

            var totalDamagedCells = 0;
            foreach (var placement in placements)
            {
                totalDamagedCells = WritePlacement(
                    writer,
                    placement,
                    imageIndexByHash[placement.Image.ContentHash],
                    totalDamagedCells);
            }
        }

        if (stream.Length > MaxRecordingLength)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                $"Recording length {stream.Length} exceeds the limit of {MaxRecordingLength}.");
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
        if (data.Length > MaxRecordingLength)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                $"Recording length {data.Length} exceeds the limit of {MaxRecordingLength}.");
        }

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
        if (version < MinimumSupportedVersion || version > CurrentVersion)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.UnsupportedVersion,
                $"Recording version {version} is not supported. Supported versions: {MinimumSupportedVersion}-{CurrentVersion}.");
        }

        var placementCount = ReadInt32(reader);
        var imageCount = ReadInt32(reader);
        ValidateCount(placementCount, MaxPlacementCount, "placement");
        ValidateCount(imageCount, MaxImageCount, "image");

        var images = new List<Hmp1SixelRecordedImage>(imageCount);
        long totalPayloadLength = 0;
        for (var i = 0; i < imageCount; i++)
        {
            var image = ReadImage(reader, version, ref totalPayloadLength);
            images.Add(image);
        }

        var placements = new List<Hmp1SixelRecordedPlacement>(placementCount);
        var totalDamagedCells = 0;
        for (var i = 0; i < placementCount; i++)
        {
            placements.Add(ReadPlacement(reader, images, ref totalDamagedCells));
        }

        if (stream.Position != stream.Length)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.Malformed,
                "Recording contains trailing bytes after the declared images and placements.");
        }

        return new Hmp1SixelRecordingSnapshot(version, images, placements);
    }

    private static void WriteImage(BinaryWriter writer, SixelData image, byte[] payloadBytes)
    {
        writer.Write(image.ContentHash);
        writer.Write(image.RasterStatus == SixelRasterStatus.GeometryOnly);
        writer.Write(image.PixelWidth);
        writer.Write(image.PixelHeight);
        writer.Write(image.WidthInCells);
        writer.Write(image.HeightInCells);
        writer.Write(BitConverter.DoubleToInt64Bits(image.CellMetrics.Width));
        writer.Write(BitConverter.DoubleToInt64Bits(image.CellMetrics.Height));
        writer.Write((byte)image.CellMetrics.Source);
        writer.Write((byte)image.CellMetrics.Reliability);
        writer.Write((byte)image.RasterStatus);
        writer.Write(payloadBytes.Length);
        writer.Write(payloadBytes);
    }

    private static bool TryEncodePayload(SixelData image, int maximumBytes, out string payload)
    {
        if (image.RasterStatus == SixelRasterStatus.GeometryOnly)
        {
            payload = image.Payload;
            return Encoding.UTF8.GetByteCount(payload) <= maximumBytes;
        }

        var pixels = image.GetPixels();
        if (pixels is not null)
        {
            var encoded = SixelExactEncoder.EncodeBounded(
                pixels,
                maximumBytes,
                CancellationToken.None);
            if (encoded.Outcome == SixelExactEncoder.EncodingOutcome.Complete)
            {
                payload = encoded.Payload!;
                return true;
            }
            if (encoded.Outcome == SixelExactEncoder.EncodingOutcome.ByteLimitExceeded)
            {
                payload = "";
                return false;
            }
        }

        payload = image.Payload;
        return Encoding.UTF8.GetByteCount(payload) <= maximumBytes;
    }

    private static int WritePlacement(
        BinaryWriter writer,
        SixelPlacement placement,
        int imageIndex,
        int totalDamagedCells)
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
        if (damagedCells.Count > MaxDamagedCellCount - totalDamagedCells)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                $"Aggregate damaged cell count exceeds the limit of {MaxDamagedCellCount}.");
        }

        writer.Write(damagedCells.Count);
        foreach (var (row, col) in damagedCells)
        {
            writer.Write(row);
            writer.Write(col);
        }

        return totalDamagedCells + damagedCells.Count;
    }

    private static Hmp1SixelRecordedImage ReadImage(
        BinaryReader reader,
        int version,
        ref long totalPayloadLength)
    {
        var contentHash = ReadExact(reader, 32);
        var isGeometryOnly = ReadBool(reader);
        var declaredPixelWidth = ReadInt32(reader);
        var declaredPixelHeight = ReadInt32(reader);
        var widthInCells = ReadInt32(reader);
        var heightInCells = ReadInt32(reader);
        SixelCellMetrics? cellMetrics = null;
        if (version >= 2)
        {
            var widthBits = ReadInt64(reader);
            var heightBits = ReadInt64(reader);
            var sourceByte = ReadByte(reader);
            var reliabilityByte = ReadByte(reader);
            if (!Enum.IsDefined(typeof(SixelCellMetricsSource), (int)sourceByte) ||
                !Enum.IsDefined(typeof(SixelCellMetricsReliability), (int)reliabilityByte))
            {
                throw new Hmp1SixelRecordingException(
                    Hmp1SixelRecordingFailureReason.Malformed,
                    $"Image declares unrecognized Sixel metric metadata ({sourceByte}/{reliabilityByte}).");
            }

            cellMetrics = new SixelCellMetrics(
                BitConverter.Int64BitsToDouble(widthBits),
                BitConverter.Int64BitsToDouble(heightBits),
                (SixelCellMetricsSource)sourceByte,
                (SixelCellMetricsReliability)reliabilityByte);
        }
        var rasterStatusByte = ReadByte(reader);
        var payloadLength = ReadInt32(reader);

        if (payloadLength < 0 || payloadLength > MaxPayloadLength)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                $"Image payload length {payloadLength} is invalid or exceeds the limit of {MaxPayloadLength}.");
        }
        if (payloadLength > MaxTotalPayloadLength - totalPayloadLength)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                $"Aggregate image payload bytes exceed the limit of {MaxTotalPayloadLength}.");
        }

        var payloadBytes = ReadExact(reader, payloadLength);
        totalPayloadLength += payloadLength;

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
            cellMetrics,
            (SixelRasterStatus)rasterStatusByte,
            Encoding.UTF8.GetString(payloadBytes));
    }

    private static Hmp1SixelRecordedPlacement ReadPlacement(
        BinaryReader reader,
        IReadOnlyList<Hmp1SixelRecordedImage> images,
        ref int totalDamagedCells)
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

        if (imageIndex < 0 || imageIndex >= images.Count)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.MissingImageReference,
                $"Placement references image index {imageIndex}, but the recording only has {images.Count} image(s).");
        }

        var image = images[imageIndex];
        if (widthInCells <= 0 ||
            heightInCells <= 0 ||
            widthInCells != image.WidthInCells ||
            heightInCells != image.HeightInCells ||
            paintedRowOffset < 0 ||
            paintedRowCount < 0 ||
            paintedColumnOffset < 0 ||
            paintedColumnCount < 0 ||
            paintedRowOffset > heightInCells - paintedRowCount ||
            paintedColumnOffset > widthInCells - paintedColumnCount)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.InvalidGeometry,
                $"Placement declares invalid geometry (cells {widthInCells}x{heightInCells}, painted rows {paintedRowOffset}+{paintedRowCount}, painted columns {paintedColumnOffset}+{paintedColumnCount}).");
        }

        if (damagedCellCount < 0 || damagedCellCount > MaxDamagedCellCount)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                $"Damaged cell count {damagedCellCount} is invalid or exceeds the limit of {MaxDamagedCellCount}.");
        }
        if (damagedCellCount > MaxDamagedCellCount - totalDamagedCells)
        {
            throw new Hmp1SixelRecordingException(
                Hmp1SixelRecordingFailureReason.ResourceLimitExceeded,
                $"Aggregate damaged cell count exceeds the limit of {MaxDamagedCellCount}.");
        }

        var damagedCells = new List<(int Row, int Column)>(damagedCellCount);
        for (var i = 0; i < damagedCellCount; i++)
        {
            var damagedRow = ReadInt32(reader);
            var damagedCol = ReadInt32(reader);
            if (damagedRow < paintedRowOffset ||
                damagedRow >= paintedRowOffset + paintedRowCount ||
                damagedCol < paintedColumnOffset ||
                damagedCol >= paintedColumnOffset + paintedColumnCount)
            {
                throw new Hmp1SixelRecordingException(
                    Hmp1SixelRecordingFailureReason.InvalidGeometry,
                    $"Damage cell ({damagedRow},{damagedCol}) lies outside the placement's painted crop.");
            }
            damagedCells.Add((damagedRow, damagedCol));
        }
        totalDamagedCells += damagedCellCount;

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
