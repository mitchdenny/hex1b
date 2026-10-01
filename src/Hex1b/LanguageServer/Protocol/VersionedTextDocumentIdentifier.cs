using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class VersionedTextDocumentIdentifier
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("version")]
    public int Version { get; set; }
}
