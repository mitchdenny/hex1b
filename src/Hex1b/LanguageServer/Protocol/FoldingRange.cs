using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class FoldingRange
{
    [JsonPropertyName("startLine")]
    public int StartLine { get; set; }

    [JsonPropertyName("startCharacter")]
    public int? StartCharacter { get; set; }

    [JsonPropertyName("endLine")]
    public int EndLine { get; set; }

    [JsonPropertyName("endCharacter")]
    public int? EndCharacter { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }
}
