using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Diagnostics;

/// <summary>
/// Diagnostic information about a popup entry.
/// </summary>
internal sealed class DiagnosticPopupEntry
{
    [JsonPropertyName("index")]
    public int Index { get; set; }
    
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = "";
    
    [JsonPropertyName("hasBackdrop")]
    public bool HasBackdrop { get; set; }
    
    [JsonPropertyName("isAnchored")]
    public bool IsAnchored { get; set; }
    
    [JsonPropertyName("isBarrier")]
    public bool IsBarrier { get; set; }
    
    [JsonPropertyName("contentBounds")]
    public DiagnosticRect? ContentBounds { get; set; }
    
    [JsonPropertyName("anchorInfo")]
    public DiagnosticAnchorInfo? AnchorInfo { get; set; }
    
    [JsonPropertyName("focusRestoreNodeType")]
    public string? FocusRestoreNodeType { get; set; }
}
