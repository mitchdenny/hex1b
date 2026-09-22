using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class DocumentHighlight
{
    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    /// <summary>1=Text, 2=Read, 3=Write.</summary>
    [JsonPropertyName("kind")]
    public int? Kind { get; set; }
}
