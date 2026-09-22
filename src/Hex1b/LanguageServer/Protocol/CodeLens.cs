using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CodeLens
{
    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("command")]
    public Command? Command { get; set; }

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}
