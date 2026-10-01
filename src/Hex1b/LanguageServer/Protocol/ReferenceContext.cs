using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class ReferenceContext
{
    [JsonPropertyName("includeDeclaration")]
    public bool IncludeDeclaration { get; set; }
}
