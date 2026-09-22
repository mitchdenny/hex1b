using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class TypeHierarchyItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("kind")]
    public int Kind { get; set; }

    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("selectionRange")]
    public LspRange SelectionRange { get; set; } = new();

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}
