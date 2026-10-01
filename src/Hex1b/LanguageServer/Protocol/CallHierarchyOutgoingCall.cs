using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CallHierarchyOutgoingCall
{
    [JsonPropertyName("to")]
    public CallHierarchyItem To { get; set; } = new();

    [JsonPropertyName("fromRanges")]
    public LspRange[] FromRanges { get; set; } = [];
}
