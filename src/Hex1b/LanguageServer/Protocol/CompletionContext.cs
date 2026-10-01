using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CompletionContext
{
    /// <summary>1 = Invoked (Ctrl+Space), 2 = TriggerCharacter, 3 = TriggerForIncompleteCompletions.</summary>
    [JsonPropertyName("triggerKind")]
    public int TriggerKind { get; set; } = 1;

    [JsonPropertyName("triggerCharacter")]
    public string? TriggerCharacter { get; set; }
}
