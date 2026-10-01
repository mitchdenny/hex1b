using System.Text.Json.Serialization;

namespace Hex1b;

internal sealed record Hwt1MarkerResult(long RequestId, bool Success,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Error = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MarkerId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Hwt1MarkerDetails? Details = null);
