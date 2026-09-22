using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class ParameterInformation
{
    /// <summary>String or [uint, uint] offset pair.</summary>
    [JsonPropertyName("label")]
    public JsonElement Label { get; set; }

    [JsonPropertyName("documentation")]
    public JsonElement? Documentation { get; set; }
}
