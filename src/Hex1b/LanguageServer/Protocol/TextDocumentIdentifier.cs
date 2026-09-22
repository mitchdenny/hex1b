using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class TextDocumentIdentifier
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";
}
