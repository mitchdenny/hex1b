using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class SignatureHelpClientCapabilities
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }

    [JsonPropertyName("signatureInformation")]
    public SignatureInformationClientCapabilities? SignatureInformation { get; set; } = new();
}
