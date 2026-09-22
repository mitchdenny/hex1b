using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class DocumentLink
{
    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("target")]
    public string? Target { get; set; }

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}
