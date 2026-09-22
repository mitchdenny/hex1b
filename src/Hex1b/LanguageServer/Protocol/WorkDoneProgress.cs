using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

// ── Progress ─────────────────────────────────────────────────

internal sealed class WorkDoneProgress
{
    [JsonPropertyName("token")]
    public JsonElement Token { get; set; }

    [JsonPropertyName("value")]
    public WorkDoneProgressValue? Value { get; set; }
}
