using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class InlayHint
{
    [JsonPropertyName("position")]
    public LspPosition Position { get; set; } = new();

    /// <summary>String or InlayHintLabelPart[].</summary>
    [JsonPropertyName("label")]
    public JsonElement Label { get; set; }

    [JsonPropertyName("kind")]
    public int? Kind { get; set; }

    [JsonPropertyName("paddingLeft")]
    public bool? PaddingLeft { get; set; }

    [JsonPropertyName("paddingRight")]
    public bool? PaddingRight { get; set; }

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}
