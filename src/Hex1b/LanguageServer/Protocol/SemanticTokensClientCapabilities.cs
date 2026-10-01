using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class SemanticTokensClientCapabilities
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }

    [JsonPropertyName("requests")]
    public SemanticTokensRequests Requests { get; set; } = new();

    [JsonPropertyName("tokenTypes")]
    public string[] TokenTypes { get; set; } = SemanticTokenTypes.All;

    [JsonPropertyName("tokenModifiers")]
    public string[] TokenModifiers { get; set; } = [];

    [JsonPropertyName("formats")]
    public string[] Formats { get; set; } = ["relative"];
}
