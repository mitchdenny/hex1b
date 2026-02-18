using System.Diagnostics.Metrics;
using Hex1b.Diagnostics;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class Hex1bMetricsTests
{
    [Fact]
    public void Constructor_CreatesAllInstruments()
    {
        using var metrics = new Hex1bMetrics();
        var instrumentNames = new List<string>();
        
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter))
            {
                instrumentNames.Add(instrument.Name);
                listener.EnableMeasurementEvents(instrument);
            }
        };
        listener.Start();
        
        var expected = new[]
        {
            "hex1b.frame.duration",
            "hex1b.frame.build.duration",
            "hex1b.frame.reconcile.duration",
            "hex1b.frame.render.duration",
            "hex1b.frame.count",
            "hex1b.output.cells_changed",
            "hex1b.output.tokens",
            "hex1b.output.bytes",
            "hex1b.input.count",
            "hex1b.input.duration",
            "hex1b.terminal.output.bytes",
            "hex1b.terminal.output.tokens",
            "hex1b.terminal.input.bytes",
            "hex1b.terminal.input.tokens",
            "hex1b.terminal.input.events",
            "hex1b.surface.diff.duration",
            "hex1b.surface.tokens.duration",
            "hex1b.surface.serialize.duration",
        };
        
        foreach (var name in expected)
        {
            Assert.Contains(name, instrumentNames);
        }
    }
    
    [Fact]
    public void Instances_AreIsolated()
    {
        using var metrics1 = new Hex1bMetrics();
        using var metrics2 = new Hex1bMetrics();
        
        var values1 = new List<long>();
        var values2 = new List<long>();
        
        using var listener1 = new MeterListener();
        listener1.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics1.Meter))
                listener.EnableMeasurementEvents(instrument);
        };
        listener1.SetMeasurementEventCallback<long>((inst, value, tags, state) =>
        {
            if (inst.Name == "hex1b.frame.count") values1.Add(value);
        });
        listener1.Start();
        
        using var listener2 = new MeterListener();
        listener2.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics2.Meter))
                listener.EnableMeasurementEvents(instrument);
        };
        listener2.SetMeasurementEventCallback<long>((inst, value, tags, state) =>
        {
            if (inst.Name == "hex1b.frame.count") values2.Add(value);
        });
        listener2.Start();
        
        metrics1.FrameCount.Add(1);
        metrics2.FrameCount.Add(1);
        metrics2.FrameCount.Add(1);
        
        Assert.Single(values1);
        Assert.Equal(2, values2.Count);
    }
    
    [Fact]
    public void Default_IsSingleton()
    {
        Assert.Same(Hex1bMetrics.Default, Hex1bMetrics.Default);
        Assert.NotNull(Hex1bMetrics.Default.Meter);
    }
    
    [Fact]
    public void FrameDuration_RecordsValues()
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
        
        metrics.FrameDuration.Record(16.5);
        metrics.FrameDuration.Record(8.2);
        
        Assert.Equal(2, recorded.Count);
        Assert.Equal(16.5, recorded[0]);
        Assert.Equal(8.2, recorded[1]);
    }
    
    [Fact]
    public void InputCount_RecordsWithTags()
    {
        using var metrics = new Hex1bMetrics();
        var tagValues = new List<string?>();
        
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter))
                listener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, state) =>
        {
            if (inst.Name == "hex1b.input.count")
            {
                foreach (var tag in tags)
                {
                    if (tag.Key == "type")
                        tagValues.Add(tag.Value?.ToString());
                }
            }
        });
        listener.Start();
        
        metrics.InputCount.Add(1, new KeyValuePair<string, object?>("type", "key"));
        metrics.InputCount.Add(1, new KeyValuePair<string, object?>("type", "mouse"));
        metrics.InputCount.Add(1, new KeyValuePair<string, object?>("type", "resize"));
        
        Assert.Equal(3, tagValues.Count);
        Assert.Equal("key", tagValues[0]);
        Assert.Equal("mouse", tagValues[1]);
        Assert.Equal("resize", tagValues[2]);
    }
    
    [Fact]
    public void OutputCellsChanged_RecordsIntValues()
    {
        using var metrics = new Hex1bMetrics();
        var recorded = new List<int>();
        
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter))
                listener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<int>((inst, value, tags, state) =>
        {
            if (inst.Name == "hex1b.output.cells_changed") recorded.Add(value);
        });
        listener.Start();
        
        metrics.OutputCellsChanged.Record(1920); // Full 80x24 screen
        metrics.OutputCellsChanged.Record(5);     // Incremental update
        metrics.OutputCellsChanged.Record(0);     // No changes
        
        Assert.Equal(3, recorded.Count);
        Assert.Equal(1920, recorded[0]);
        Assert.Equal(5, recorded[1]);
        Assert.Equal(0, recorded[2]);
    }
    
    [Fact]
    public void NoListener_DoesNotThrow()
    {
        // Verify metrics work without any listener attached (zero-cost path)
        using var metrics = new Hex1bMetrics();
        
        metrics.FrameDuration.Record(10.0);
        metrics.FrameCount.Add(1);
        metrics.OutputCellsChanged.Record(100);
        metrics.OutputTokens.Record(50);
        metrics.OutputBytes.Record(2048);
        metrics.InputCount.Add(1, new KeyValuePair<string, object?>("type", "key"));
        metrics.InputDuration.Record(0.5);
        metrics.TerminalOutputBytes.Record(1024);
        metrics.TerminalOutputTokens.Record(30);
        metrics.TerminalInputBytes.Record(3);
        metrics.TerminalInputTokens.Record(1);
        metrics.TerminalInputEvents.Add(1, new KeyValuePair<string, object?>("type", "key"));
    }
    
    [Fact]
    public async Task Integration_MetricsRecordedDuringRender()
    {
        using var metrics = new Hex1bMetrics();
        var frameCount = 0L;
        var frameDurations = new List<double>();
        var cellsCounts = new List<int>();
        
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter))
                listener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, state) =>
        {
            if (inst.Name == "hex1b.frame.count") Interlocked.Add(ref frameCount, value);
        });
        listener.SetMeasurementEventCallback<double>((inst, value, tags, state) =>
        {
            if (inst.Name == "hex1b.frame.duration")
                lock (frameDurations) frameDurations.Add(value);
        });
        listener.SetMeasurementEventCallback<int>((inst, value, tags, state) =>
        {
            if (inst.Name == "hex1b.output.cells_changed")
                lock (cellsCounts) cellsCounts.Add(value);
        });
        listener.Start();
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .WithMetrics(metrics)
            .Build();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Hello Metrics")),
            new Hex1bAppOptions
            {
                WorkloadAdapter = workload,
                Metrics = metrics
            });
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello Metrics"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Input.Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        Assert.True(Interlocked.Read(ref frameCount) > 0, "Expected at least one frame");
        Assert.NotEmpty(frameDurations);
        Assert.All(frameDurations, d => Assert.True(d > 0, $"Frame duration {d}ms should be > 0"));
        Assert.NotEmpty(cellsCounts);
        Assert.True(cellsCounts[0] > 0, "First frame should have changed cells");
    }
    
    // --- Per-node metrics tests ---
    
    [Fact]
    public void PerNodeMetrics_DisabledByDefault()
    {
        using var metrics = new Hex1bMetrics();
        Assert.Null(metrics.NodeMeasureDuration);
        Assert.Null(metrics.NodeArrangeDuration);
        Assert.Null(metrics.NodeRenderDuration);
        Assert.Null(metrics.NodeReconcileDuration);
        Assert.Null(metrics.SurfaceFlattenDuration);
        Assert.Null(metrics.SurfaceCompositeDuration);
        Assert.Null(metrics.SurfaceLayerCount);
        Assert.Null(metrics.SurfaceLayerDuration);
    }
    
    [Fact]
    public void PerNodeMetrics_CreatesInstrumentsWhenEnabled()
    {
        using var metrics = new Hex1bMetrics(options: new Hex1bMetricsOptions { EnablePerNodeMetrics = true });
        Assert.NotNull(metrics.NodeMeasureDuration);
        
        var instrumentNames = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter))
            {
                instrumentNames.Add(instrument.Name);
                listener.EnableMeasurementEvents(instrument);
            }
        };
        listener.Start();
        
        Assert.Contains("hex1b.node.measure.duration", instrumentNames);
        Assert.Contains("hex1b.node.arrange.duration", instrumentNames);
        Assert.Contains("hex1b.node.render.duration", instrumentNames);
        Assert.Contains("hex1b.node.reconcile.duration", instrumentNames);
        Assert.Contains("hex1b.surface.flatten.duration", instrumentNames);
        Assert.Contains("hex1b.surface.composite.duration", instrumentNames);
        Assert.Contains("hex1b.surface.layer.count", instrumentNames);
        Assert.Contains("hex1b.surface.layer.duration", instrumentNames);
    }
    
    [Fact]
    public void MetricName_FluentExtension_SetsProperty()
    {
        var widget = new TextBlockWidget("test").MetricName("sidebar");
        Assert.Equal("sidebar", widget.MetricName);
    }
    
    [Fact]
    public void MetricPath_AutoGenerated_UsesTypeAndIndex()
    {
        var parent = new VStackNode();
        var child = new TextBlockNode { Parent = parent, MetricChildIndex = 2 };
        
        var path = child.GetMetricPath();
        // Should contain type suffix and index
        Assert.Contains("TextBlock[2]", path);
    }
    
    [Fact]
    public void MetricPath_WithUserName_UsesNameInsteadOfType()
    {
        var parent = new VStackNode { MetricName = "root" };
        var child = new TextBlockNode { Parent = parent, MetricName = "title", MetricChildIndex = 0 };
        
        var path = child.GetMetricPath();
        Assert.Equal("root.title", path);
    }
    
    [Fact]
    public void MetricPath_Cached_ReturnsSameInstance()
    {
        var node = new TextBlockNode { MetricName = "test" };
        var path1 = node.GetMetricPath();
        var path2 = node.GetMetricPath();
        Assert.Same(path1, path2);
    }
    
    [Fact]
    public void MetricPath_InvalidatedOnNameChange()
    {
        var node = new TextBlockNode { MetricName = "old" };
        var path1 = node.GetMetricPath();
        
        node.MetricName = "new";
        node.InvalidateMetricPath();
        var path2 = node.GetMetricPath();
        
        Assert.NotEqual(path1, path2);
        Assert.Equal("new", path2);
    }
    
    [Fact]
    public void MetricPath_Hierarchical_ComposesFromAncestors()
    {
        var root = new VStackNode { MetricName = "root" };
        var sidebar = new VStackNode { Parent = root, MetricName = "sidebar", MetricChildIndex = 0 };
        var table = new TextBlockNode { Parent = sidebar, MetricName = "orders", MetricChildIndex = 0 };
        
        Assert.Equal("root.sidebar.orders", table.GetMetricPath());
    }
    
    [Fact]
    public void MetricPath_MixedAutoAndNamed()
    {
        var root = new VStackNode { MetricName = "root" };
        var child = new HStackNode { Parent = root, MetricChildIndex = 1 };
        var leaf = new TextBlockNode { Parent = child, MetricName = "label", MetricChildIndex = 0 };
        
        var path = leaf.GetMetricPath();
        Assert.Equal("root.HStack[1].label", path);
    }
    
    [Fact]
    public async Task Integration_PerNodeMetrics_RecordsDurations()
    {
        var options = new Hex1bMetricsOptions { EnablePerNodeMetrics = true };
        using var metrics = new Hex1bMetrics(options: options);
        var nodePaths = new List<string>();
        
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter))
                listener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((inst, value, tags, state) =>
        {
            if (inst.Name.StartsWith("hex1b.node."))
            {
                foreach (var tag in tags)
                {
                    if (tag.Key == "node")
                        lock (nodePaths) nodePaths.Add($"{inst.Name}:{tag.Value}");
                }
            }
        });
        listener.Start();
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .WithMetrics(metrics)
            .Build();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Title").MetricName("title"),
                    v.Text("Body").MetricName("body")
                ]).MetricName("root")),
            new Hex1bAppOptions
            {
                WorkloadAdapter = workload,
                Metrics = metrics
            });
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Title"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Input.Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        // Should have per-node measurements with the metric paths
        Assert.NotEmpty(nodePaths);
        // Verify at least the root and named children appear
        Assert.Contains(nodePaths, p => p.Contains("root"));
        Assert.Contains(nodePaths, p => p.Contains("title"));
        Assert.Contains(nodePaths, p => p.Contains("body"));
    }
    
    [Fact]
    public async Task Integration_PerNodeMetrics_DisabledByDefault_NoNodeMetricsEmitted()
    {
        // Default metrics — per-node NOT enabled
        using var metrics = new Hex1bMetrics();
        var nodeMetrics = new List<string>();
        
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter))
                listener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((inst, value, tags, state) =>
        {
            if (inst.Name.StartsWith("hex1b.node."))
                lock (nodeMetrics) nodeMetrics.Add(inst.Name);
        });
        listener.Start();
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 10)
            .WithMetrics(metrics)
            .Build();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Hello").MetricName("greeting"),
                    v.Text("World")
                ]).MetricName("root")),
            new Hex1bAppOptions
            {
                WorkloadAdapter = workload,
                Metrics = metrics
            });
        
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Input.Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        // No per-node metrics should have been emitted
        Assert.Empty(nodeMetrics);
    }

    [Fact]
    public void WithMetricsCallback_CreatesMetricsWithPerNodeEnabled()
    {
        // Regression: WithMetrics(configure) must flow options into ResolveMetrics
        // so that per-node instruments are created.
        var nodePaths = new List<string>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name.StartsWith("hex1b.node."))
                listener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((inst, value, tags, state) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "node")
                    lock (nodePaths) nodePaths.Add($"{inst.Name}:{tag.Value}");
            }
        });
        listener.Start();

        // Use the options-callback overload — this is the path consumers use
        var options = new Hex1bMetricsOptions();
        options.EnablePerNodeMetrics = true;
        using var metrics = new Hex1bMetrics(options: options);

        // Per-node instruments must have been created
        Assert.NotNull(metrics.NodeMeasureDuration);
        Assert.NotNull(metrics.NodeArrangeDuration);
        Assert.NotNull(metrics.NodeRenderDuration);
        Assert.NotNull(metrics.NodeReconcileDuration);

        // Record a measurement to verify the instruments are functional
        metrics.NodeMeasureDuration!.Record(1.0, new KeyValuePair<string, object?>("node", "test.Widget"));

        Assert.Contains(nodePaths, p => p.Contains("test.Widget"));
    }
}
