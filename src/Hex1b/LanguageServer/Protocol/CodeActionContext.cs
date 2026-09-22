using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CodeActionContext
{
    [JsonPropertyName("diagnostics")]
    public LspDiagnostic[] Diagnostics { get; set; } = [];

    [JsonPropertyName("only")]
    public string[]? Only { get; set; }
}
