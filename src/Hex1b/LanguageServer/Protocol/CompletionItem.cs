using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CompletionItem
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("kind")]
    public int? Kind { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("insertText")]
    public string? InsertText { get; set; }

    [JsonPropertyName("filterText")]
    public string? FilterText { get; set; }

    [JsonPropertyName("sortText")]
    public string? SortText { get; set; }
}
