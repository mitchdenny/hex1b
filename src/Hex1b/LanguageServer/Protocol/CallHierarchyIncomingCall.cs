using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CallHierarchyIncomingCall
{
    [JsonPropertyName("from")]
    public CallHierarchyItem From { get; set; } = new();

    [JsonPropertyName("fromRanges")]
    public LspRange[] FromRanges { get; set; } = [];
}
