using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CompletionItemClientCapabilities
{
    [JsonPropertyName("snippetSupport")]
    public bool SnippetSupport { get; set; }

    [JsonPropertyName("commitCharactersSupport")]
    public bool CommitCharactersSupport { get; set; } = true;

    [JsonPropertyName("documentationFormat")]
    public string[] DocumentationFormat { get; set; } = ["plaintext"];

    [JsonPropertyName("deprecatedSupport")]
    public bool DeprecatedSupport { get; set; } = true;

    [JsonPropertyName("preselectSupport")]
    public bool PreselectSupport { get; set; } = true;

    [JsonPropertyName("insertReplaceSupport")]
    public bool InsertReplaceSupport { get; set; }

    [JsonPropertyName("resolveSupport")]
    public CompletionItemResolveSupportCapabilities? ResolveSupport { get; set; } = new();
}
