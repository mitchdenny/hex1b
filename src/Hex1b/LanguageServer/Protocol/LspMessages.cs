using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

// ── JSON-RPC 2.0 base types ──────────────────────────────────

/// <summary>JSON-RPC 2.0 request message.</summary>
internal sealed class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}

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

/// <summary>JSON-RPC 2.0 error object.</summary>
internal sealed class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

/// <summary>Params for the $/cancelRequest notification.</summary>
internal sealed class CancelParams
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

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
