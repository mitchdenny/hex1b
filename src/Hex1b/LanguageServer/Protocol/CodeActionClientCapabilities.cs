using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CodeActionClientCapabilities
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }

    [JsonPropertyName("codeActionLiteralSupport")]
    public CodeActionLiteralSupportCapabilities? CodeActionLiteralSupport { get; set; } = new();
}
