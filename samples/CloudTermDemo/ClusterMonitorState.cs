using Hex1b.Theming;

namespace CloudTermDemo;

/// <summary>
/// Tracks live CPU metrics for AKS cluster nodes with rolling data windows.
/// Updates every second with simulated data.
/// </summary>
public sealed class ClusterMonitorState
{
    public record CpuSample(string Time, double Usage);

    private readonly List<(string NodeName, List<CpuSample> Samples)> _nodes = [];
    private DateTime _lastUpdate = DateTime.MinValue;

    public string ClusterName { get; }
    public IReadOnlyList<(string NodeName, List<CpuSample> Samples)> Nodes => _nodes;

    /// <summary>Colors for each node's chart line.</summary>
    public static readonly Hex1bColor[] NodeColors =
    [
        Hex1bColor.FromRgb(60, 180, 255),   // blue
        Hex1bColor.FromRgb(80, 220, 120),   // green
        Hex1bColor.FromRgb(255, 180, 60),   // orange
        Hex1bColor.FromRgb(220, 80, 180),   // pink
        Hex1bColor.FromRgb(180, 130, 255),  // purple
    ];

    public ClusterMonitorState(string clusterName, int nodeCount = 3)
    {
        ClusterName = clusterName;
        for (var i = 0; i < nodeCount; i++)
        {
            _nodes.Add(($"aks-nodepool1-{30000000 + i}", new List<CpuSample>()));
        }

        // Seed with initial data
        var now = DateTime.Now;
        for (var t = -30; t <= 0; t++)
        {
            var time = now.AddSeconds(t).ToString("HH:mm:ss");
            foreach (var (_, samples) in _nodes)
            {
                samples.Add(new CpuSample(time, GenerateBaseline(samples)));
            }
        }
    }

    /// <summary>
    /// Adds a new data point if at least 1 second has passed.
    /// Returns true if data was updated.
    /// </summary>
    public bool Update()
    {
        var now = DateTime.Now;
        if ((now - _lastUpdate).TotalMilliseconds < 1000)
            return false;

        _lastUpdate = now;
        var time = now.ToString("HH:mm:ss");

        foreach (var (_, samples) in _nodes)
        {
            samples.Add(new CpuSample(time, GenerateBaseline(samples)));
            if (samples.Count > 60)
                samples.RemoveAt(0);
        }

        return true;
    }

    private static double GenerateBaseline(List<CpuSample> history)
    {
        // Smooth random walk around a baseline
        var last = history.Count > 0 ? history[^1].Usage : 30 + Random.Shared.Next(0, 40);
        var delta = (Random.Shared.NextDouble() - 0.5) * 8;
        return Math.Clamp(last + delta, 5, 95);
    }
}
