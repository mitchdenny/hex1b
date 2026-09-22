using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class SignatureInformation
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("documentation")]
    public JsonElement? Documentation { get; set; }

    [JsonPropertyName("parameters")]
    public ParameterInformation[]? Parameters { get; set; }

    [JsonPropertyName("activeParameter")]
    public int? ActiveParameter { get; set; }
}
