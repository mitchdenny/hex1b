using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CompletionItemKindClientCapabilities
{
    [JsonPropertyName("valueSet")]
    public int[] ValueSet { get; set; } = Enumerable.Range(1, 25).ToArray();
}
