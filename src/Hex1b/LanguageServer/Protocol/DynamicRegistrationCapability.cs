using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class DynamicRegistrationCapability
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }
}
