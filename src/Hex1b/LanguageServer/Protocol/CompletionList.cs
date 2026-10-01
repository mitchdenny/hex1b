using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CompletionList
{
    [JsonPropertyName("isIncomplete")]
    public bool IsIncomplete { get; set; }

    [JsonPropertyName("items")]
    public CompletionItem[] Items { get; set; } = [];
}
