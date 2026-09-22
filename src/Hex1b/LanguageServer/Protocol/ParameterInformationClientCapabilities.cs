using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class ParameterInformationClientCapabilities
{
    [JsonPropertyName("labelOffsetSupport")]
    public bool LabelOffsetSupport { get; set; } = true;
}
