using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class DocumentSymbolClientCapabilities
{
    [JsonPropertyName("dynamicRegistration")]
    public bool DynamicRegistration { get; set; }

    [JsonPropertyName("hierarchicalDocumentSymbolSupport")]
    public bool HierarchicalDocumentSymbolSupport { get; set; } = true;
}
