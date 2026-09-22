using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class SignatureInformationClientCapabilities
{
    [JsonPropertyName("documentationFormat")]
    public string[] DocumentationFormat { get; set; } = ["plaintext"];

    [JsonPropertyName("parameterInformation")]
    public ParameterInformationClientCapabilities? ParameterInformation { get; set; } = new();
}
