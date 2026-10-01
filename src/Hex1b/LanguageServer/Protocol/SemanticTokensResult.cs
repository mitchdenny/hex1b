using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class SemanticTokensResult
{
    [JsonPropertyName("data")]
    public int[] Data { get; set; } = [];
}
