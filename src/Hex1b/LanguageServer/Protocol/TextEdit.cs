using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

// ── Editing ─────────────────────────────────────────────────

internal sealed class TextEdit
{
    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("newText")]
    public string NewText { get; set; } = "";
}
