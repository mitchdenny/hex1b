using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class DocumentFormattingParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("options")]
    public FormattingOptions Options { get; set; } = new();
}
