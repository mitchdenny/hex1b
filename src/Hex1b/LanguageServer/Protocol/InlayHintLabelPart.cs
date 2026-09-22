using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class InlayHintLabelPart
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("tooltip")]
    public JsonElement? Tooltip { get; set; }

    [JsonPropertyName("location")]
    public Location? Location { get; set; }

    [JsonPropertyName("command")]
    public Command? Command { get; set; }
}
