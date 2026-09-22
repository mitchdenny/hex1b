using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

// ── Document Sync ────────────────────────────────────────────

internal sealed class DidOpenTextDocumentParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentItem TextDocument { get; set; } = new();
}
