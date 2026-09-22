using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class SignatureHelpContext
{
    /// <summary>1=Invoked, 2=TriggerCharacter, 3=ContentChange.</summary>
    [JsonPropertyName("triggerKind")]
    public int TriggerKind { get; set; } = 1;

    [JsonPropertyName("triggerCharacter")]
    public string? TriggerCharacter { get; set; }

    [JsonPropertyName("isRetrigger")]
    public bool IsRetrigger { get; set; }

    [JsonPropertyName("activeSignatureHelp")]
    public SignatureHelp? ActiveSignatureHelp { get; set; }
}
