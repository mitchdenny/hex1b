using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class WorkspaceEdit
{
    [JsonPropertyName("changes")]
    public Dictionary<string, TextEdit[]>? Changes { get; set; }

    [JsonPropertyName("documentChanges")]
    public JsonElement? DocumentChanges { get; set; }
}
