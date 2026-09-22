using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

// ── Client Capability Types ─────────────────────────────────────

internal sealed class WorkspaceClientCapabilities
{
    [JsonPropertyName("applyEdit")]
    public bool ApplyEdit { get; set; }

    [JsonPropertyName("workspaceFolders")]
    public bool WorkspaceFolders { get; set; }
}
