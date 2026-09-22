using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Diagnostics;

/// <summary>
/// Per-node performance timing information.
/// </summary>
internal sealed class DiagnosticTiming
{
    /// <summary>
    /// Time spent reconciling this node, in milliseconds.
    /// </summary>
    [JsonPropertyName("reconcileMs")]
    public double ReconcileMs { get; set; }
    
    /// <summary>
    /// Time spent rendering this node, in milliseconds.
    /// </summary>
    [JsonPropertyName("renderMs")]
    public double RenderMs { get; set; }
    
    /// <summary>
    /// Milliseconds since this node was last rendered.
    /// </summary>
    [JsonPropertyName("lastRenderedMsAgo")]
    public double LastRenderedMsAgo { get; set; }
    
    internal static DiagnosticTiming FromNode(Hex1bNode node, long now)
    {
        var freq = (double)Stopwatch.Frequency;
        return new DiagnosticTiming
        {
            ReconcileMs = node.DiagReconcileTicks * 1000.0 / freq,
            RenderMs = node.DiagRenderTicks * 1000.0 / freq,
            LastRenderedMsAgo = node.DiagLastRenderedTimestamp > 0
                ? (now - node.DiagLastRenderedTimestamp) * 1000.0 / freq
                : -1
        };
    }
    
    public override string ToString()
    {
        var parts = new List<string>(3);
        if (ReconcileMs > 0) parts.Add($"reconcile={ReconcileMs:F2}ms");
        if (RenderMs > 0) parts.Add($"render={RenderMs:F2}ms");
        if (LastRenderedMsAgo >= 0) parts.Add($"last={LastRenderedMsAgo:F0}ms ago");
        return string.Join(" ", parts);
    }
}
