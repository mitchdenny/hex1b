using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class LspDiagnostic
{
    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("severity")]
    public int? Severity { get; set; } // 1=Error, 2=Warning, 3=Info, 4=Hint

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("code")]
    public System.Text.Json.JsonElement? Code { get; set; }
}
