using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class SignatureHelp
{
    [JsonPropertyName("signatures")]
    public SignatureInformation[] Signatures { get; set; } = [];

    [JsonPropertyName("activeSignature")]
    public int? ActiveSignature { get; set; }

    [JsonPropertyName("activeParameter")]
    public int? ActiveParameter { get; set; }
}
