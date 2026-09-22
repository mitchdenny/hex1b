using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class TextDocumentEdit
{
    [JsonPropertyName("textDocument")]
    public VersionedTextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("edits")]
    public TextEdit[] Edits { get; set; } = [];
}
