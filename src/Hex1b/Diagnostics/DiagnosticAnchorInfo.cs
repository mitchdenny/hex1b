using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Diagnostics;

/// <summary>
/// Diagnostic information about an anchor.
/// </summary>
internal sealed class DiagnosticAnchorInfo
{
    [JsonPropertyName("anchorNodeType")]
    public string? AnchorNodeType { get; set; }
    
    [JsonPropertyName("anchorBounds")]
    public DiagnosticRect? AnchorBounds { get; set; }
    
    [JsonPropertyName("isStale")]
    public bool IsStale { get; set; }
    
    [JsonPropertyName("position")]
    public string? Position { get; set; }
}
