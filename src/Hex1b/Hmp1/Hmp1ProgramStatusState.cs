using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b;

// Full replacement in least-recently-updated order, not a sequence of OSC reports.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record Hmp1ProgramStatusState
{
    internal const int Version = 1;
    internal const int MaxPayloadSize = 4 * 1024 * 1024;
    internal const int MaxRecords = 256;

    public required TerminalProgramStatusRecord[] Records { get; init; }

    internal static byte[] Encode(TerminalProgramStatusSnapshot snapshot)
    {
        var state = new Hmp1ProgramStatusState { Records = snapshot.Records.ToArray() };
        Validate(state);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, Hmp1JsonContext.Default.Hmp1ProgramStatusState);
        if (bytes.Length > MaxPayloadSize)
            throw new InvalidDataException("HMP program status checkpoint exceeds its byte limit.");
        return bytes;
    }

    internal static TerminalProgramStatusSnapshot Parse(ReadOnlyMemory<byte> payload)
    {
        if (payload.IsEmpty || payload.Length > MaxPayloadSize)
            throw new InvalidDataException("Invalid HMP program status checkpoint size.");
        try
        {
            using var document = JsonDocument.Parse(payload);
            ValidateProperties(document.RootElement);
            var state = JsonSerializer.Deserialize(payload.Span, Hmp1JsonContext.Default.Hmp1ProgramStatusState)
                ?? throw new InvalidDataException("Missing HMP program status checkpoint.");
            Validate(state);
            return new TerminalProgramStatusSnapshot(state.Records);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Malformed HMP program status checkpoint.", exception);
        }
    }

    internal static ValueTask WriteAsync(Stream stream, TerminalProgramStatusSnapshot snapshot, CancellationToken ct)
        => Hmp1Protocol.WriteFrameAsync(stream, Hmp1FrameType.ProgramStatusState, Encode(snapshot), ct);

    internal static async ValueTask<TerminalProgramStatusSnapshot> ReadAsync(Stream stream, CancellationToken ct)
    {
        var frame = await Hmp1Protocol.ReadFrameAsync(stream, ct).ConfigureAwait(false)
            ?? throw new InvalidDataException("Missing HMP program status checkpoint.");
        if (frame.Type != Hmp1FrameType.ProgramStatusState)
            throw new InvalidDataException($"Expected ProgramStatusState checkpoint, got {frame.Type}.");
        return Parse(frame.Payload);
    }

    private static void Validate(Hmp1ProgramStatusState state)
    {
        if (state.Records is null || state.Records.Length > MaxRecords)
            throw new InvalidDataException("Invalid HMP program status record count.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in state.Records)
        {
            if (record is null || record.Id is null || !ids.Add(record.Id) ||
                !Enum.IsDefined(record.State) || (record.Kind is { } kind && !Enum.IsDefined(kind)))
                throw new InvalidDataException("Invalid HMP program status record.");
            try
            {
                _ = TerminalProgramStatusCodec.Serialize(record);
                if (record.EffectiveApp is not null)
                    _ = TerminalProgramStatusCodec.Serialize(record with { App = record.EffectiveApp });
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Invalid HMP program status record.", exception);
            }
        }
    }

    private static void ValidateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                ValidateProperties(item);
            return;
        }
        if (element.ValueKind != JsonValueKind.Object)
            return;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Add(property.Name))
                throw new InvalidDataException("Duplicate HMP program status checkpoint property.");
            if (property.Name is not ("records" or "id" or "state" or "kind" or "progress" or
                "app" or "title" or "message" or "effectiveApp"))
                throw new InvalidDataException("Unknown HMP program status checkpoint property.");
            ValidateProperties(property.Value);
        }
        if (names.Contains("records") ? names.Count != 1 : !names.Contains("id") || !names.Contains("state"))
            throw new InvalidDataException("Incomplete HMP program status checkpoint.");
    }
}
