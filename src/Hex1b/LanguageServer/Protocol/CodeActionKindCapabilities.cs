using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class CodeActionKindCapabilities
{
    [JsonPropertyName("valueSet")]
    public string[] ValueSet { get; set; } =
    [
        "quickfix",
        "refactor",
        "refactor.extract",
        "refactor.inline",
        "refactor.rewrite",
        "source",
        "source.organizeImports",
    ];
}
