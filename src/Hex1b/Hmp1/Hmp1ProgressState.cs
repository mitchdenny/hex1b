using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Automation;

namespace Hex1b;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record Hmp1ProgressState
{
    public required int State { get; init; }
    public required int? Percentage { get; init; }
}
