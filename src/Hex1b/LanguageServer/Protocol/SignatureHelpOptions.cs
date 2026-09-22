using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class SignatureHelpOptions
{
    [JsonPropertyName("triggerCharacters")]
    public string[]? TriggerCharacters { get; set; }

    [JsonPropertyName("retriggerCharacters")]
    public string[]? RetriggerCharacters { get; set; }
}
