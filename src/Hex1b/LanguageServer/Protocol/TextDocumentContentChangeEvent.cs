using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class TextDocumentContentChangeEvent
{
    /// <summary>Range of the document that changed. Null for full document replacement.</summary>
    [JsonPropertyName("range")]
    public LspRange? Range { get; set; }

    /// <summary>Length of the range that got replaced (deprecated but some servers use it).</summary>
    [JsonPropertyName("rangeLength")]
    public int? RangeLength { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}
