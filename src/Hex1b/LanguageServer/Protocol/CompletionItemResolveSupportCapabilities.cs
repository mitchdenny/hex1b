using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CompletionItemResolveSupportCapabilities
{
    [JsonPropertyName("properties")]
    public string[] Properties { get; set; } = ["documentation", "detail"];
}
