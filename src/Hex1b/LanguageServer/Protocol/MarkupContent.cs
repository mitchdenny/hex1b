using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class MarkupContent
{
    /// <summary>"plaintext" or "markdown".</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "plaintext";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}
