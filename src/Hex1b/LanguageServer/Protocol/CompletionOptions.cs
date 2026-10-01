using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

// ── Server Option Types ─────────────────────────────────────────

internal sealed class CompletionOptions
{
    [JsonPropertyName("triggerCharacters")]
    public string[]? TriggerCharacters { get; set; }

    [JsonPropertyName("resolveProvider")]
    public bool ResolveProvider { get; set; }
}
