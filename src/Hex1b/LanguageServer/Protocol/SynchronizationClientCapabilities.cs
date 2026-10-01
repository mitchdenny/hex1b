using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class SynchronizationClientCapabilities
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }

    [JsonPropertyName("willSave")]
    public bool WillSave { get; set; }

    [JsonPropertyName("didSave")]
    public bool DidSave { get; set; } = true;
}
