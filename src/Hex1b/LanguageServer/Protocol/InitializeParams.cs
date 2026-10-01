using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

// ── Initialize ───────────────────────────────────────────────

internal sealed class InitializeParams
{
    [JsonPropertyName("processId")]
    public int? ProcessId { get; set; }

    [JsonPropertyName("rootUri")]
    public string? RootUri { get; set; }

    [JsonPropertyName("workspaceFolders")]
    public WorkspaceFolder[]? WorkspaceFolders { get; set; }

    [JsonPropertyName("capabilities")]
    public ClientCapabilities Capabilities { get; set; } = new();
}
