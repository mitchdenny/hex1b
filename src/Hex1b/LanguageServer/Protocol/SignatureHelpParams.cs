using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

// ── Signature Help / Info / Structure ───────────────────────

internal sealed class SignatureHelpParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; set; } = new();

    [JsonPropertyName("position")]
    public LspPosition Position { get; set; } = new();

    [JsonPropertyName("context")]
    public SignatureHelpContext? Context { get; set; }
}
