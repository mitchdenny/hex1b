using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CallHierarchyIncomingCallsParams
{
    [JsonPropertyName("item")]
    public CallHierarchyItem Item { get; set; } = new();
}
