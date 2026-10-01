using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class FormattingOptions
{
    [JsonPropertyName("tabSize")]
    public int TabSize { get; set; } = 4;

    [JsonPropertyName("insertSpaces")]
    public bool InsertSpaces { get; set; } = true;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
