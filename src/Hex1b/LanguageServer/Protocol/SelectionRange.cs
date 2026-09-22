using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class SelectionRange
{
    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("parent")]
    public SelectionRange? Parent { get; set; }
}
