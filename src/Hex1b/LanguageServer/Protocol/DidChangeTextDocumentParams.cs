using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class DidChangeTextDocumentParams
{
    [JsonPropertyName("textDocument")]
    public VersionedTextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("contentChanges")]
    public TextDocumentContentChangeEvent[] ContentChanges { get; set; } = [];
}
