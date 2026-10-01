using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class LocationLink
{
    [JsonPropertyName("originSelectionRange")]
    public LspRange? OriginSelectionRange { get; set; }

    [JsonPropertyName("targetUri")]
    public string TargetUri { get; set; } = "";

    [JsonPropertyName("targetRange")]
    public LspRange TargetRange { get; set; } = new();

    [JsonPropertyName("targetSelectionRange")]
    public LspRange TargetSelectionRange { get; set; } = new();
}
