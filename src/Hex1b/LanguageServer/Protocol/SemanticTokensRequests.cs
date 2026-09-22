using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class SemanticTokensRequests
{
    [JsonPropertyName("full")]
    public bool Full { get; set; } = true;
}
