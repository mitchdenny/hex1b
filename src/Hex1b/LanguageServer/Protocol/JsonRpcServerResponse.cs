using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

/// <summary>JSON-RPC server-to-client response (concrete type for AOT serialization).</summary>
internal sealed class JsonRpcServerResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; set; }
}
