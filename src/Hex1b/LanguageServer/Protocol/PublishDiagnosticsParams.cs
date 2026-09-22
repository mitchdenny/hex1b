using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

// ── Diagnostics ──────────────────────────────────────────────

internal sealed class PublishDiagnosticsParams
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("diagnostics")]
    public LspDiagnostic[] Diagnostics { get; set; } = [];
}
