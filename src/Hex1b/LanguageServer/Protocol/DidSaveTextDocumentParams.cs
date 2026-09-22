using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

// ── Document Save ────────────────────────────────────────────

internal sealed class DidSaveTextDocumentParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
