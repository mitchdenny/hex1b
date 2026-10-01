using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CompletionClientCapabilities
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }

    [JsonPropertyName("completionItem")]
    public CompletionItemClientCapabilities? CompletionItem { get; set; } = new();

    [JsonPropertyName("completionItemKind")]
    public CompletionItemKindClientCapabilities? CompletionItemKind { get; set; } = new();
}
