using System.Buffers.Binary;

namespace Hex1b;

// A bounded, immutable wire checkpoint. Rows are detached from producer storage.
internal sealed record Hmp1ScrollbackState(int AvailableRows, IReadOnlyList<byte[]> Rows)
{
    internal const int Version = 1;
    internal const int MaxRows = 100_000;
    internal const int MaxChunkBytes = 1024 * 1024;
    internal const int MaxTotalBytes = 32 * 1024 * 1024;
    internal const int MaxCells = 2_000_000;
    internal static Hmp1ScrollbackState Unavailable { get; } = new(-1, []);

    internal async Task WriteAsync(Stream stream, int rowLimit, CancellationToken ct)
    {
        var first = Math.Max(0, Rows.Count - rowLimit);
        var header = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header, AvailableRows);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), Rows.Count - first);
        await Hmp1Protocol.WriteFrameAsync(stream, Hmp1FrameType.ScrollbackState, header, ct).ConfigureAwait(false);

        using var chunk = new MemoryStream();
        using var writer = new BinaryWriter(chunk);
        for (var i = first; i < Rows.Count; i++)
        {
            var row = Rows[i];
            if (chunk.Length + 4 + row.Length > MaxChunkBytes)
            {
                await FlushAsync().ConfigureAwait(false);
                chunk.SetLength(0);
                chunk.Position = 0;
            }
            writer.Write(row.Length);
            writer.Write(row);
        }
        if (chunk.Length > 0)
            await FlushAsync().ConfigureAwait(false);

        Task FlushAsync() => Hmp1Protocol.WriteFrameAsync(stream, Hmp1FrameType.ScrollbackRows,
            chunk.GetBuffer().AsMemory(0, (int)chunk.Length), ct).AsTask();
    }

    internal static async Task<Hmp1ScrollbackState> ReadAsync(Stream stream, int rowLimit, CancellationToken ct)
    {
        var header = await Hmp1Protocol.ReadFrameAsync(stream, ct).ConfigureAwait(false)
            ?? throw new InvalidDataException("Missing scrollback checkpoint.");
        if (header.Type != Hmp1FrameType.ScrollbackState || header.Payload.Length != 8)
            throw new InvalidDataException("Expected scrollback checkpoint header.");
        var available = BinaryPrimitives.ReadInt32LittleEndian(header.Payload.Span);
        var count = BinaryPrimitives.ReadInt32LittleEndian(header.Payload.Span[4..]);
        if (available < -1 || count < 0 || count > rowLimit || count > MaxRows ||
            (available == -1 ? count != 0 : count > available))
            throw new InvalidDataException("Invalid scrollback checkpoint row counts.");

        var rows = new List<byte[]>(count);
        var totalBytes = 0;
        var totalCells = 0;
        while (rows.Count < count)
        {
            var frame = await Hmp1Protocol.ReadFrameAsync(stream, ct).ConfigureAwait(false)
                ?? throw new InvalidDataException("Incomplete scrollback checkpoint.");
            if (frame.Type != Hmp1FrameType.ScrollbackRows || frame.Payload.IsEmpty)
                throw new InvalidDataException("Expected scrollback rows.");
            totalBytes = checked(totalBytes + frame.Payload.Length);
            if (totalBytes > MaxTotalBytes)
                throw new InvalidDataException("Scrollback checkpoint exceeds its byte limit.");
            var offset = 0;
            while (offset < frame.Payload.Length)
            {
                if (rows.Count == count || frame.Payload.Length - offset < 4)
                    throw new InvalidDataException("Unexpected scrollback row data.");
                var length = BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[offset..]);
                offset += 4;
                if (length <= 0 || length > frame.Payload.Length - offset)
                    throw new InvalidDataException("Invalid scrollback row length.");
                var bytes = frame.Payload.Slice(offset, length).ToArray();
                offset += length;
                // Validate every row before any part of the screen/history transaction is published.
                var (row, _) = Hmp1ScrollbackRowCodec.Decode(bytes);
                totalCells = checked(totalCells + row.Cells.Length);
                if (totalCells > MaxCells)
                    throw new InvalidDataException("Scrollback checkpoint exceeds its cell limit.");
                rows.Add(bytes);
            }
        }
        return new(available, rows);
    }
}
