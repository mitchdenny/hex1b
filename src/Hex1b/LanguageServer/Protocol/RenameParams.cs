using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class RenameParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("position")]
    public LspPosition Position { get; set; } = new();

    [JsonPropertyName("newName")]
    public string NewName { get; set; } = "";
}
