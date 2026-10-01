using System.Text;
using static Hex1b.Hmp1BinaryEncoding;

namespace Hex1b;

// Positions address the accompanying transferred history and active screen, never producer row IDs.
internal sealed record Hmp1CommandMarkState(
    bool Available, int HistoryRows, int Width, int Height, bool Alternate,
    long LastId, int AvailableMarks, IReadOnlyList<Hmp1CommandMark> Marks)
{
    internal const int Version = 1;
    internal const int MaxMarks = 10_000;
    internal const int MaxPayloadSize = 8 * 1024 * 1024;
    internal static Hmp1CommandMarkState Unavailable { get; } = new(false, 0, 0, 0, false, 0, 0, []);

    internal Hmp1CommandMarkState ForHistoryRows(int historyRows)
    {
        if (!Available || HistoryRows == historyRows)
            return this;
        if (historyRows < 0 || historyRows > HistoryRows)
            throw new InvalidDataException("Cannot expand command checkpoint history.");
        var discarded = HistoryRows - historyRows;
        return this with
        {
            HistoryRows = historyRows,
            Marks = Marks.Where(mark => mark.Alternate || mark.Row >= discarded)
                .Select(mark => mark.Alternate ? mark : mark with { Row = mark.Row - discarded }).ToArray()
        };
    }

    internal byte[] Serialize()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Utf8);
        writer.Write(Available);
        if (Available)
        {
            writer.Write(HistoryRows);
            writer.Write(Width);
            writer.Write(Height);
            writer.Write(Alternate);
            writer.Write(LastId);
            writer.Write(AvailableMarks);
            writer.Write(Marks.Count);
            foreach (var mark in Marks)
            {
                writer.Write(mark.Id);
                writer.Write(mark.Alternate);
                writer.Write(mark.Row);
                writer.Write(mark.Column);
                writer.Write((byte)mark.Phase);
                writer.Write(mark.ExitCode.HasValue);
                if (mark.ExitCode is { } exitCode)
                    writer.Write(exitCode);
                writer.Write(mark.RawParameters is not null);
                if (mark.RawParameters is { } parameters)
                    WriteString(writer, parameters);
                if (stream.Length > MaxPayloadSize)
                    throw new InvalidDataException("Command mark checkpoint exceeds its byte limit.");
            }
        }
        return stream.ToArray();
    }

    internal Task WriteAsync(Stream stream, int historyRows, CancellationToken ct)
        => Hmp1Protocol.WriteFrameAsync(stream, Hmp1FrameType.CommandMarkState,
            ForHistoryRows(historyRows).Serialize(), ct).AsTask();

    internal static async Task<Hmp1CommandMarkState> ReadAsync(
        Stream stream, Hmp1TerminalState terminal, Hmp1ScrollbackState? history, CancellationToken ct)
    {
        var frame = await Hmp1Protocol.ReadFrameAsync(stream, ct).ConfigureAwait(false)
            ?? throw new InvalidDataException("Missing command mark checkpoint.");
        if (frame.Type != Hmp1FrameType.CommandMarkState)
            throw new InvalidDataException("Expected command mark checkpoint.");
        return Parse(frame.Payload, terminal, history);
    }

    internal static Hmp1CommandMarkState Parse(
        ReadOnlyMemory<byte> payload, Hmp1TerminalState terminal, Hmp1ScrollbackState? history)
    {
        if (payload.IsEmpty || payload.Length > MaxPayloadSize)
            throw new InvalidDataException("Invalid command mark checkpoint size.");
        try
        {
            using var stream = new MemoryStream(payload.ToArray(), writable: false);
            using var reader = new BinaryReader(stream, Utf8);
            if (!ReadFlag(reader))
            {
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("Trailing unavailable command checkpoint data.");
                return Unavailable;
            }
            var historyRows = reader.ReadInt32();
            var width = reader.ReadInt32();
            var height = reader.ReadInt32();
            var alternate = ReadFlag(reader);
            var lastId = reader.ReadInt64();
            var available = reader.ReadInt32();
            var count = reader.ReadInt32();
            if (historyRows != (history?.Rows.Count ?? 0) ||
                width != terminal.Width || height != terminal.Height ||
                width is < 1 or > 16_384 || height is < 1 or > 16_384 ||
                lastId < 0 || available < 0 || count < 0 || count > MaxMarks || count > available)
                throw new InvalidDataException("Invalid command checkpoint geometry or counts.");
            var marks = new List<Hmp1CommandMark>(count);
            var ids = new HashSet<long>();
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadInt64();
                var markAlternate = ReadFlag(reader);
                var row = reader.ReadInt32();
                var column = reader.ReadInt32();
                var phase = (TerminalShellIntegrationPhase)reader.ReadByte();
                int? exitCode = ReadFlag(reader) ? reader.ReadInt32() : null;
                var parameters = ReadFlag(reader) ? ReadString(reader) : null;
                var maxRow = markAlternate ? height : historyRows + (alternate ? 0 : height);
                if (id <= 0 || id > lastId || !ids.Add(id) ||
                    (markAlternate && !alternate) || row < 0 || row >= maxRow ||
                    column < 0 || column > 16_384 ||
                    phase is < TerminalShellIntegrationPhase.Prompt or > TerminalShellIntegrationPhase.Finished ||
                    (phase != TerminalShellIntegrationPhase.Finished && exitCode is not null))
                    throw new InvalidDataException("Invalid retained command mark.");
                var rowWidth = !markAlternate && row < historyRows
                    ? System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(history!.Rows[row].AsSpan(12))
                    : width;
                if (column > rowWidth)
                    throw new InvalidDataException("Command mark lies outside its backing row.");
                marks.Add(new(id, markAlternate, row, column, phase, exitCode, parameters));
            }
            if (stream.Position != stream.Length)
                throw new InvalidDataException("Trailing command mark checkpoint data.");
            return new(true, historyRows, width, height, alternate, lastId, available, marks);
        }
        catch (Exception error) when (error is EndOfStreamException or DecoderFallbackException)
        {
            throw new InvalidDataException("Malformed command mark checkpoint.", error);
        }
    }

    private static bool ReadFlag(BinaryReader reader) => reader.ReadByte() switch
    {
        0 => false,
        1 => true,
        _ => throw new InvalidDataException("Invalid command checkpoint flag.")
    };
}
