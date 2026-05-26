using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace PerfDemo;

/// <summary>
/// In-process MeterListener that subscribes to the "Hex1b" meter and aggregates
/// every histogram into mean / p50 / p95 / p99 / max + counters into totals.
///
/// This converts the rich System.Diagnostics.Metrics instrumentation in
/// <c>Hex1b.Diagnostics.Hex1bMetrics</c> into a one-shot, human-readable report
/// without needing OTLP or any external collector.
/// </summary>
internal sealed class MetricsCollector : IDisposable
{
    private readonly MeterListener _listener;
    private readonly ConcurrentDictionary<string, HistogramSink> _histograms = new();
    private readonly ConcurrentDictionary<string, long> _counters = new();
    private bool _capturing;

    public MetricsCollector(string meterName = "Hex1b")
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == meterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };

        _listener.SetMeasurementEventCallback<double>((inst, value, _, _) =>
        {
            if (!_capturing) return;
            _histograms.GetOrAdd(inst.Name, _ => new HistogramSink(inst.Unit)).Add(value);
        });
        _listener.SetMeasurementEventCallback<int>((inst, value, _, _) =>
        {
            if (!_capturing) return;
            _histograms.GetOrAdd(inst.Name, _ => new HistogramSink(inst.Unit)).Add(value);
        });
        _listener.SetMeasurementEventCallback<long>((inst, value, _, _) =>
        {
            if (!_capturing) return;
            if (inst is Counter<long>)
            {
                _counters.AddOrUpdate(inst.Name, value, (_, acc) => acc + value);
            }
            else
            {
                _histograms.GetOrAdd(inst.Name, _ => new HistogramSink(inst.Unit)).Add(value);
            }
        });

        _listener.Start();
    }

    /// <summary>Begin recording. Call after warmup to exclude first-frame noise.</summary>
    public void StartCapture() => _capturing = true;

    /// <summary>Stop recording.</summary>
    public void StopCapture() => _capturing = false;

    public void Dispose() => _listener.Dispose();

    public void PrintSummary(TextWriter writer)
    {
        writer.WriteLine();
        writer.WriteLine("== Hex1b metrics ==");

        if (_counters.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine("Counters:");
            foreach (var (name, value) in _counters.OrderBy(p => p.Key))
            {
                writer.WriteLine($"  {name,-44} {value,12:N0}");
            }
        }

        if (_histograms.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine("Histograms (count, mean, p50, p95, p99, max, unit):");
            writer.WriteLine($"  {"name",-44} {"count",8} {"mean",10} {"p50",10} {"p95",10} {"p99",10} {"max",10} unit");

            foreach (var (name, sink) in _histograms.OrderBy(p => p.Key))
            {
                var s = sink.Snapshot();
                writer.WriteLine(
                    $"  {name,-44} {s.Count,8:N0} {s.Mean,10:N3} {s.P50,10:N3} {s.P95,10:N3} {s.P99,10:N3} {s.Max,10:N3} {sink.Unit ?? ""}");
            }
        }
    }

    private sealed class HistogramSink
    {
        private readonly object _gate = new();
        private double[] _values = new double[1024];
        private int _count;

        public HistogramSink(string? unit) { Unit = unit; }
        public string? Unit { get; }

        public void Add(double value)
        {
            lock (_gate)
            {
                if (_count == _values.Length)
                {
                    Array.Resize(ref _values, _values.Length * 2);
                }
                _values[_count++] = value;
            }
        }

        public Stats Snapshot()
        {
            double[] copy;
            int count;
            lock (_gate)
            {
                count = _count;
                copy = new double[count];
                Array.Copy(_values, copy, count);
            }
            if (count == 0)
            {
                return new Stats(0, 0, 0, 0, 0, 0);
            }
            Array.Sort(copy);
            double sum = 0;
            for (var i = 0; i < count; i++) sum += copy[i];
            return new Stats(
                Count: count,
                Mean: sum / count,
                P50: Percentile(copy, 0.50),
                P95: Percentile(copy, 0.95),
                P99: Percentile(copy, 0.99),
                Max: copy[count - 1]);
        }

        private static double Percentile(double[] sorted, double p)
        {
            if (sorted.Length == 0) return 0;
            var rank = p * (sorted.Length - 1);
            var lo = (int)Math.Floor(rank);
            var hi = (int)Math.Ceiling(rank);
            if (lo == hi) return sorted[lo];
            var frac = rank - lo;
            return sorted[lo] * (1 - frac) + sorted[hi] * frac;
        }
    }

    private readonly record struct Stats(int Count, double Mean, double P50, double P95, double P99, double Max);
}
