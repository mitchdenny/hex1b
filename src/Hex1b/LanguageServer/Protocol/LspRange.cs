using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class LspRange
{
    [JsonPropertyName("start")]
    public LspPosition Start { get; set; } = new();

    [JsonPropertyName("end")]
    public LspPosition End { get; set; } = new();
}
