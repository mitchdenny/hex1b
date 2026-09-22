using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class InitializeResult
{
    [JsonPropertyName("capabilities")]
    public ServerCapabilities Capabilities { get; set; } = new();
}
