using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class ClientCapabilities
{
    [JsonPropertyName("textDocument")]
    public TextDocumentClientCapabilities? TextDocument { get; set; } = new();

    [JsonPropertyName("workspace")]
    public WorkspaceClientCapabilities? Workspace { get; set; }
}
