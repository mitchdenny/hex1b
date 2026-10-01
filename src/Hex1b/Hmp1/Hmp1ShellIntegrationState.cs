using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Automation;

namespace Hex1b;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record Hmp1ShellIntegrationState
{
    public required int Phase { get; init; }
    public required int? LastExitCode { get; init; }
}
