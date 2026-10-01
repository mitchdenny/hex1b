using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class HoverClientCapabilities
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }

    [JsonPropertyName("contentFormat")]
    public string[] ContentFormat { get; set; } = ["plaintext"];
}
