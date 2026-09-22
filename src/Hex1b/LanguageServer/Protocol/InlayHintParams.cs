using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

// ── Advanced ────────────────────────────────────────────────

internal sealed class InlayHintParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();
}
