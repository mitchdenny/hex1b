using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class HoverResult
{
    [JsonPropertyName("contents")]
    public MarkupContent Contents { get; set; } = new();

    [JsonPropertyName("range")]
    public LspRange? Range { get; set; }
}
