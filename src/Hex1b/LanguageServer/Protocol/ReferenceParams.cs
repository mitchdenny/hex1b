using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class ReferenceParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("position")]
    public LspPosition Position { get; set; } = new();

    [JsonPropertyName("context")]
    public ReferenceContext Context { get; set; } = new();
}
