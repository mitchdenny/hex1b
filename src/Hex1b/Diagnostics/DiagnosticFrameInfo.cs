using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Diagnostics;

/// <summary>
/// Frame-level performance metrics.
/// </summary>
internal sealed class DiagnosticFrameInfo
{
    [JsonPropertyName("buildMs")]
    public double BuildMs { get; set; }
    
    [JsonPropertyName("reconcileMs")]
    public double ReconcileMs { get; set; }
    
    [JsonPropertyName("renderMs")]
    public double RenderMs { get; set; }
    
    [JsonPropertyName("timingEnabled")]
    public bool TimingEnabled { get; set; }
}
