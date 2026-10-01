using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CodeActionParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("context")]
    public CodeActionContext Context { get; set; } = new();
}
