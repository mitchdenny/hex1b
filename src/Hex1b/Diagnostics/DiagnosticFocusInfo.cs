using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Diagnostics;

/// <summary>
/// Diagnostic information about the focus ring.
/// </summary>
internal sealed class DiagnosticFocusInfo
{
    [JsonPropertyName("focusableCount")]
    public int FocusableCount { get; set; }
    
    [JsonPropertyName("currentFocusIndex")]
    public int CurrentFocusIndex { get; set; }
    
    [JsonPropertyName("focusedNodeType")]
    public string? FocusedNodeType { get; set; }
    
    [JsonPropertyName("focusables")]
    public List<DiagnosticFocusableEntry>? Focusables { get; set; }
    
    [JsonPropertyName("lastHitTestDebug")]
    public string? LastHitTestDebug { get; set; }
}
