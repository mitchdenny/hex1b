---
title: Performance & Metrics
---

# Performance & Metrics

Hex1b includes built-in [OpenTelemetry-compatible](https://opentelemetry.io/) metrics instrumentation via `System.Diagnostics.Metrics`. These metrics let you understand render performance, output throughput, and input rates — both at the frame level and per-widget level.

## Setting Up Metrics

### With .NET Aspire

If your application uses Aspire, metrics are automatically exported when you register the `Hex1b` meter in your service defaults:

```csharp
// In your ServiceDefaults project
builder.Services.ConfigureOpenTelemetryMeterProvider(meter =>
    meter.AddMeter("Hex1b"));
```

All `hex1b.*` metrics will appear in the Aspire dashboard under the Metrics tab.

### With a MeterListener

For standalone applications or unit tests, attach a `MeterListener` to observe metrics in-process:

```csharp
using System.Diagnostics.Metrics;

var listener = new MeterListener();
listener.InstrumentPublished = (instrument, listener) =>
{
    if (instrument.Meter.Name == "Hex1b")
        listener.EnableMeasurementEvents(instrument);
};
listener.SetMeasurementEventCallback<double>((inst, value, tags, state) =>
{
    Console.WriteLine($"{inst.Name}: {value:F2}ms");
});
listener.Start();
```

## Frame-Level Metrics

These instruments are always active and have near-zero overhead when no listener is attached.

| Instrument | Type | Unit | Description |
|---|---|---|---|
| `hex1b.frame.duration` | Histogram | ms | Total frame duration (build + reconcile + render) |
| `hex1b.frame.build.duration` | Histogram | ms | Widget tree build phase |
| `hex1b.frame.reconcile.duration` | Histogram | ms | Widget→Node reconciliation |
| `hex1b.frame.render.duration` | Histogram | ms | Surface render + diff + serialize |
| `hex1b.frame.count` | Counter | frame | Total frames rendered |

### Output Metrics

| Instrument | Type | Unit | Description |
|---|---|---|---|
| `hex1b.output.cells_changed` | Histogram | cell | Cells changed per surface diff |
| `hex1b.output.tokens` | Histogram | token | ANSI tokens produced per frame |
| `hex1b.output.bytes` | Histogram | byte | Bytes written per frame |

### Input Metrics

| Instrument | Type | Unit | Description |
|---|---|---|---|
| `hex1b.input.count` | Counter | event | Events processed (tag: `type`=key\|mouse\|resize) |
| `hex1b.input.duration` | Histogram | ms | Time to process a single input event |

### Terminal I/O Metrics

| Instrument | Type | Unit | Description |
|---|---|---|---|
| `hex1b.terminal.output.bytes` | Histogram | byte | Bytes written to presentation per write |
| `hex1b.terminal.output.tokens` | Histogram | token | Tokens parsed from workload output |
| `hex1b.terminal.input.bytes` | Histogram | byte | Raw bytes read from presentation |
| `hex1b.terminal.input.tokens` | Histogram | token | Tokens parsed from raw input |
| `hex1b.terminal.input.events` | Counter | event | Events dispatched (tag: `type`=key\|mouse\|resize) |

### Surface Pipeline Metrics

These break down `hex1b.frame.render.duration` into sub-phases. Always active.

| Instrument | Type | Unit | Description |
|---|---|---|---|
| `hex1b.surface.diff.duration` | Histogram | ms | Time to diff previous vs current surface |
| `hex1b.surface.tokens.duration` | Histogram | ms | Time to convert diff to ANSI tokens |
| `hex1b.surface.serialize.duration` | Histogram | ms | Time to serialize tokens to ANSI string |

## Per-Node Metrics

Per-node metrics let you drill into which widgets are expensive. They record timing histograms for each node in the tree, tagged by a hierarchical metric path.

::: warning Local Development Only
Per-node metrics generate high tag cardinality (one time series per node in the tree). Enable them only during local development with Aspire — never in production.
:::

### Enabling Per-Node Metrics

```csharp
var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
        ctx.VStack(v => [
            v.Text("Hello")
        ]))
    .WithMetrics(options => options.EnablePerNodeMetrics = true)
    .Build();
```

### Naming Widgets

Use `.MetricName()` to give widgets meaningful names in the metrics output:

```csharp
ctx.VStack(main => [
    main.Border(
        main.Table(rows).MetricName("orders")
    ).MetricName("sidebar"),
    main.Editor(state).MetricName("editor")
]).MetricName("root")
```

This produces metric paths like `root.sidebar.orders` and `root.editor`.

Widgets without a `MetricName` get auto-generated names based on their type and child index — for example, `VStack[0]`, `TextBlock[2]`.

### Per-Node Instruments

| Instrument | Type | Unit | Tag | Description |
|---|---|---|---|---|
| `hex1b.node.measure.duration` | Histogram | ms | `node` | Time in Measure for one node |
| `hex1b.node.arrange.duration` | Histogram | ms | `node` | Time in Arrange for one node |
| `hex1b.node.render.duration` | Histogram | ms | `node` | Time in Render for one node |
| `hex1b.node.reconcile.duration` | Histogram | ms | `node` | Time in ReconcileAsync for one node |

### Surface Composition Instruments

For `SurfaceWidget` nodes with layered composition, these additional per-node instruments provide layer-level detail:

| Instrument | Type | Unit | Tags | Description |
|---|---|---|---|---|
| `hex1b.surface.flatten.duration` | Histogram | ms | `node` | Time to flatten all layers into a single surface |
| `hex1b.surface.composite.duration` | Histogram | ms | `node` | Time to blit flattened surface onto parent |
| `hex1b.surface.layer.count` | Histogram | int | `node` | Number of layers per render |
| `hex1b.surface.layer.duration` | Histogram | ms | `node`, `layer_index`, `layer_type` | Time per layer (type: `source`, `draw`, `computed`, `widget`) |

The `node` tag value is the hierarchical metric path (e.g., `root.sidebar.orders.VStack[0]`).

## Performance Analysis Workflow

### Step 1: Identify Slow Frames

Look at `hex1b.frame.duration` in the Aspire dashboard. Spikes above ~16ms (60 FPS target) indicate slow frames.

Check the phase breakdown — `hex1b.frame.build.duration`, `hex1b.frame.reconcile.duration`, and `hex1b.frame.render.duration` — to narrow down which phase is slow.

### Step 2: Enable Per-Node Metrics

Add `.WithMetrics(o => o.EnablePerNodeMetrics = true)` to the terminal builder. Tag suspect widgets with `.MetricName("name")` so they're easy to find in the dashboard.

### Step 3: Filter in Aspire

In the Aspire Metrics tab, filter `hex1b.node.render.duration` by the `node` tag. Use wildcards like `*sidebar*` to find all nodes under a named subtree.

### Step 4: Drill Down

Add more `.MetricName()` tags to children of the slow widget, re-run, and filter by the deeper path to isolate the exact expensive node.

### Step 5: Profile Without Names

Even without explicit `MetricName` calls, all nodes get auto-generated paths. Filter by type — for example, `node=*Table*` — to find all table render times across the tree.

## Metrics in Unit Tests

Use `MeterListener` to verify metric behavior in tests. Each `new Hex1bMetrics()` creates an isolated `Meter` instance — use `ReferenceEquals(instrument.Meter, metrics.Meter)` to filter by instance and avoid crosstalk in parallel tests.

```csharp
[Fact]
public void Frame_RecordsDuration()
{
    using var metrics = new Hex1bMetrics();
    var recorded = new List<double>();

    using var listener = new MeterListener();
    listener.InstrumentPublished = (instrument, listener) =>
    {
        if (ReferenceEquals(instrument.Meter, metrics.Meter))
            listener.EnableMeasurementEvents(instrument);
    };
    listener.SetMeasurementEventCallback<double>((inst, value, tags, state) =>
    {
        if (inst.Name == "hex1b.frame.duration") recorded.Add(value);
    });
    listener.Start();

    // ... drive app with this metrics instance ...
    Assert.NotEmpty(recorded);
    Assert.All(recorded, d => Assert.True(d > 0));
}
```

## Next Steps

- [Getting Started](/guide/getting-started) — Build your first Hex1b application
- [Widgets & Nodes](/guide/widgets-and-nodes) — Understand the widget/node architecture
- [Automation & Testing](/guide/testing) — Test your Hex1b applications
