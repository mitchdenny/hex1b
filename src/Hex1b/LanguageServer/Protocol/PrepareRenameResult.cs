using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class PrepareRenameResult
{
    [JsonPropertyName("range")]
    public LspRange Range { get; set; } = new();

    [JsonPropertyName("placeholder")]
    public string Placeholder { get; set; } = "";
}
