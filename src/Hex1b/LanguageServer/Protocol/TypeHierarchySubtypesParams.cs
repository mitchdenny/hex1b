using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hex1b.LanguageServer.Protocol;

internal sealed class TypeHierarchySubtypesParams
{
    [JsonPropertyName("item")]
    public TypeHierarchyItem Item { get; set; } = new();
}
