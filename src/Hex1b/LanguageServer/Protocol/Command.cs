using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class Command
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("command")]
    public string CommandIdentifier { get; set; } = "";

    [JsonPropertyName("arguments")]
    public JsonElement[]? Arguments { get; set; }
}
