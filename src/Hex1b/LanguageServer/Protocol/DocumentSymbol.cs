using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class DocumentSymbol
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("kind")]
    public int Kind { get; set; }

    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("selectionRange")]
    public LspRange SelectionRange { get; set; } = new();

    [JsonPropertyName("children")]
    public DocumentSymbol[]? Children { get; set; }
}
