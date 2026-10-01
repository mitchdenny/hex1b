using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Diagnostics;

/// <summary>
/// A focusable entry in the focus ring.
/// </summary>
internal sealed class DiagnosticFocusableEntry
{
    [JsonPropertyName("index")]
    public int Index { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
    
    [JsonPropertyName("bounds")]
    public DiagnosticRect Bounds { get; set; } = new();
    
    [JsonPropertyName("hitTestBounds")]
    public DiagnosticRect HitTestBounds { get; set; } = new();
    
    [JsonPropertyName("isFocused")]
    public bool IsFocused { get; set; }
}
