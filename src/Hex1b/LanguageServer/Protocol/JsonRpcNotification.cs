using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

/// <summary>JSON-RPC 2.0 notification (no id).</summary>
internal sealed class JsonRpcNotification
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}
