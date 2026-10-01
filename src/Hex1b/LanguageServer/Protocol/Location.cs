using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class Location
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();
}
