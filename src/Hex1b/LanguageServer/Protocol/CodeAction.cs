using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CodeAction
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("diagnostics")]
    public LspDiagnostic[]? Diagnostics { get; set; }

    [JsonPropertyName("isPreferred")]
    public bool? IsPreferred { get; set; }

    [JsonPropertyName("edit")]
    public WorkspaceEdit? Edit { get; set; }

    [JsonPropertyName("command")]
    public Command? Command { get; set; }
}
