using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

// ── LSP basic types ──────────────────────────────────────────

internal sealed class LspPosition
{
    [JsonPropertyName("line")]
    public int Line { get; set; } // 0-based

    [JsonPropertyName("character")]
    public int Character { get; set; } // 0-based
}
