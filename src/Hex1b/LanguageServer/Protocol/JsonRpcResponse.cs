using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

/// <summary>JSON-RPC 2.0 response message.</summary>
internal sealed class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("result")]
    public System.Text.Json.JsonElement? Result { get; set; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("params")]
    public System.Text.Json.JsonElement? Params { get; set; }

    /// <summary>True if this is a notification (has method, no id).</summary>
    public bool IsNotification => Method != null && Id == null;
}
