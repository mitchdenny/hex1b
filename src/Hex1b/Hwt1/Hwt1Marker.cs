using System.Text.Json.Serialization;

namespace Hex1b;

internal sealed record Hwt1Marker(string Id, string Source, string Buffer, int? Row, int Column,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Phase = null,
    int? ExitCode = null);
