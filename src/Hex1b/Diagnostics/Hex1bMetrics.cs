using System.Diagnostics.Metrics;

namespace Hex1b.Diagnostics;

/// <summary>
/// Provides OpenTelemetry-compatible metrics instrumentation for Hex1b.
/// </summary>
/// <remarks>
/// <para>
/// All instruments live under the <c>Hex1b</c> meter and use the <c>hex1b.*</c> namespace.
/// Metrics are zero-cost when no <see cref="MeterListener"/> or OTLP exporter is attached.
/// </para>
/// <para>
/// Use <see cref="Default"/> for production. Create a new instance per test for isolation —
/// <see cref="MeterListener"/> can filter by <see cref="Meter"/> reference to avoid crosstalk.
/// </para>
/// </remarks>
public sealed class Hex1bMetrics : IDisposable
{
    /// <summary>
    /// Shared singleton for production use. Always-on, zero config.
    /// </summary>
    public static Hex1bMetrics Default { get; } = new();

    /// <summary>
    /// The underlying meter. Use this reference in <see cref="MeterListener.InstrumentPublished"/>
    /// to filter by instance for test isolation.
    /// </summary>
    public Meter Meter { get; }

    // --- Render loop (workload side) ---

    /// <summary>Total frame duration (build + reconcile + render).</summary>
    public Histogram<double> FrameDuration { get; }

    /// <summary>Widget tree build phase duration.</summary>
    public Histogram<double> FrameBuildDuration { get; }

    /// <summary>Widget→Node reconciliation duration.</summary>
    public Histogram<double> FrameReconcileDuration { get; }

    /// <summary>Surface render + diff + serialize duration.</summary>
    public Histogram<double> FrameRenderDuration { get; }

    /// <summary>Total frames rendered.</summary>
    public Counter<long> FrameCount { get; }

    // --- Workload output ---

    /// <summary>Cells changed per surface diff.</summary>
    public Histogram<int> OutputCellsChanged { get; }

    /// <summary>ANSI tokens produced by diff serialization.</summary>
    public Histogram<int> OutputTokens { get; }

    /// <summary>Bytes written to workload output channel per frame.</summary>
    public Histogram<int> OutputBytes { get; }

    // --- Input processing (workload side) ---

    /// <summary>Input events processed (tagged by <c>type</c>: key, mouse, resize).</summary>
    public Counter<long> InputCount { get; }

    /// <summary>Time to process a single input event.</summary>
    public Histogram<double> InputDuration { get; }

    // --- Terminal output pump ---

    /// <summary>Bytes written to presentation adapter per write.</summary>
    public Histogram<int> TerminalOutputBytes { get; }

    /// <summary>ANSI tokens parsed from workload output per pump cycle.</summary>
    public Histogram<int> TerminalOutputTokens { get; }

    /// <summary>Current workload output channel queue depth.</summary>
    public ObservableGauge<int> TerminalOutputQueueDepth { get; }

    // --- Terminal input pump ---

    /// <summary>Raw bytes read from presentation adapter per read.</summary>
    public Histogram<int> TerminalInputBytes { get; }

    /// <summary>ANSI tokens parsed from raw input per read.</summary>
    public Histogram<int> TerminalInputTokens { get; }

    /// <summary>Structured events dispatched to workload (tagged by <c>type</c>: key, mouse, resize).</summary>
    public Counter<long> TerminalInputEvents { get; }

    // --- Per-node timing (opt-in) ---

    /// <summary>Per-node measure phase duration (tagged by <c>node</c> path).</summary>
    public Histogram<double>? NodeMeasureDuration { get; }

    /// <summary>Per-node arrange phase duration (tagged by <c>node</c> path).</summary>
    public Histogram<double>? NodeArrangeDuration { get; }

    /// <summary>Per-node render phase duration (tagged by <c>node</c> path).</summary>
    public Histogram<double>? NodeRenderDuration { get; }

    /// <summary>Per-node reconcile phase duration (tagged by <c>node</c> path).</summary>
    public Histogram<double>? NodeReconcileDuration { get; }

    /// <summary>
    /// Whether per-node metrics are enabled. When <see langword="true"/>, per-node
    /// timing histograms are recorded for measure, arrange, render, and reconcile.
    /// </summary>
    public bool PerNodeMetricsEnabled { get; }

    private Func<int>? _queueDepthCallback;

    /// <summary>
    /// Sets the callback used by the <c>hex1b.terminal.output.queue_depth</c> observable gauge.
    /// Call this after construction when the adapter becomes available.
    /// </summary>
    internal void SetQueueDepthCallback(Func<int> callback) => _queueDepthCallback = callback;

    /// <summary>
    /// Creates a new <see cref="Hex1bMetrics"/> instance with its own <see cref="System.Diagnostics.Metrics.Meter"/>.
    /// </summary>
    /// <param name="queueDepthCallback">Optional callback to observe output queue depth.</param>
    /// <param name="options">Optional metrics options to control per-node metrics.</param>
    public Hex1bMetrics(Func<int>? queueDepthCallback = null, Hex1bMetricsOptions? options = null)
    {
        _queueDepthCallback = queueDepthCallback;
        PerNodeMetricsEnabled = options?.EnablePerNodeMetrics ?? false;
        Meter = new Meter("Hex1b");

        // Render loop
        FrameDuration = Meter.CreateHistogram<double>("hex1b.frame.duration", "ms", "Total frame duration");
        FrameBuildDuration = Meter.CreateHistogram<double>("hex1b.frame.build.duration", "ms", "Widget tree build phase");
        FrameReconcileDuration = Meter.CreateHistogram<double>("hex1b.frame.reconcile.duration", "ms", "Widget-to-node reconciliation");
        FrameRenderDuration = Meter.CreateHistogram<double>("hex1b.frame.render.duration", "ms", "Surface render + diff + serialize");
        FrameCount = Meter.CreateCounter<long>("hex1b.frame.count", "{frame}", "Total frames rendered");

        // Workload output
        OutputCellsChanged = Meter.CreateHistogram<int>("hex1b.output.cells_changed", "{cell}", "Cells changed per diff");
        OutputTokens = Meter.CreateHistogram<int>("hex1b.output.tokens", "{token}", "ANSI tokens produced per frame");
        OutputBytes = Meter.CreateHistogram<int>("hex1b.output.bytes", "By", "Bytes written to output channel per frame");

        // Input processing
        InputCount = Meter.CreateCounter<long>("hex1b.input.count", "{event}", "Input events processed");
        InputDuration = Meter.CreateHistogram<double>("hex1b.input.duration", "ms", "Input event processing duration");

        // Terminal output pump
        TerminalOutputBytes = Meter.CreateHistogram<int>("hex1b.terminal.output.bytes", "By", "Bytes written to presentation per write");
        TerminalOutputTokens = Meter.CreateHistogram<int>("hex1b.terminal.output.tokens", "{token}", "ANSI tokens from workload output per pump cycle");
        TerminalOutputQueueDepth = Meter.CreateObservableGauge("hex1b.terminal.output.queue_depth", ObserveQueueDepth, "{item}", "Output channel queue depth");

        // Terminal input pump
        TerminalInputBytes = Meter.CreateHistogram<int>("hex1b.terminal.input.bytes", "By", "Raw bytes from presentation per read");
        TerminalInputTokens = Meter.CreateHistogram<int>("hex1b.terminal.input.tokens", "{token}", "ANSI tokens from raw input per read");
        TerminalInputEvents = Meter.CreateCounter<long>("hex1b.terminal.input.events", "{event}", "Events dispatched to workload");

        // Per-node timing (only created when enabled to avoid instrument registration overhead)
        if (PerNodeMetricsEnabled)
        {
            NodeMeasureDuration = Meter.CreateHistogram<double>("hex1b.node.measure.duration", "ms", "Per-node measure phase duration");
            NodeArrangeDuration = Meter.CreateHistogram<double>("hex1b.node.arrange.duration", "ms", "Per-node arrange phase duration");
            NodeRenderDuration = Meter.CreateHistogram<double>("hex1b.node.render.duration", "ms", "Per-node render phase duration");
            NodeReconcileDuration = Meter.CreateHistogram<double>("hex1b.node.reconcile.duration", "ms", "Per-node reconcile phase duration");
        }
    }

    private int ObserveQueueDepth() => _queueDepthCallback?.Invoke() ?? 0;

    /// <inheritdoc/>
    public void Dispose() => Meter.Dispose();
}
