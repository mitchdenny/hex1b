using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class FoldingRangeParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();
}
