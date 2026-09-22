using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CodeLensOptions
{
    [JsonPropertyName("resolveProvider")]
    public bool ResolveProvider { get; set; }
}
