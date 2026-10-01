using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

/// <summary>Params for the $/cancelRequest notification.</summary>
internal sealed class CancelParams
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
}
