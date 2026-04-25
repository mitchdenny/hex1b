using Hex1b;
using Hex1b.Charts;
using System.Text.Json;

// Load OTLP trace data from exported JSON
var tracePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "webfrontend-traces.json");
if (!File.Exists(tracePath))
{
    // Fallback for running from project directory
    tracePath = Path.Combine(Directory.GetCurrentDirectory(), "webfrontend-traces.json");
}

var json = File.ReadAllText(tracePath);
var doc = JsonDocument.Parse(json);

// Parse all spans from OTLP format
var allSpans = new List<TraceSpanItem>();
foreach (var resourceSpans in doc.RootElement.GetProperty("resourceSpans").EnumerateArray())
{
    foreach (var scopeSpans in resourceSpans.GetProperty("scopeSpans").EnumerateArray())
    {
        var scopeName = scopeSpans.GetProperty("scope").GetProperty("name").GetString() ?? "unknown";
        foreach (var span in scopeSpans.GetProperty("spans").EnumerateArray())
        {
            var spanId = span.GetProperty("spanId").GetString()!;
            var traceId = span.GetProperty("traceId").GetString()!;
            var name = span.GetProperty("name").GetString()!;
            var parentSpanId = span.TryGetProperty("parentSpanId", out var parentProp)
                ? parentProp.GetString()
                : null;

            var startNano = long.Parse(span.GetProperty("startTimeUnixNano").GetString()!);
            var endNano = long.Parse(span.GetProperty("endTimeUnixNano").GetString()!);
            var startTime = DateTimeOffset.FromUnixTimeMilliseconds(startNano / 1_000_000);
            var duration = TimeSpan.FromTicks((endNano - startNano) / 100); // nano to ticks

            var status = TraceSpanStatus.Ok;
            if (span.TryGetProperty("status", out var statusProp))
            {
                var code = statusProp.TryGetProperty("code", out var codeProp) ? codeProp.GetInt32() : 0;
                status = code == 2 ? TraceSpanStatus.Error : code == 1 ? TraceSpanStatus.Ok : TraceSpanStatus.Unset;
            }

            allSpans.Add(new TraceSpanItem(
                spanId, parentSpanId, name, scopeName,
                startTime, duration, Status: status));
        }
    }
}

// Group by traceId and pick the trace with the most spans
var traceGroups = allSpans.GroupBy(s => s.SpanId[..0] == "" ? "x" : "x") // group all first
    .SelectMany(g => g)
    .ToList();

// Actually group by traceId — we need the traceId which isn't on TraceSpanItem.
// Re-parse to get traceId mapping
var spanTraceMap = new Dictionary<string, string>();
foreach (var resourceSpans in doc.RootElement.GetProperty("resourceSpans").EnumerateArray())
{
    foreach (var scopeSpans in resourceSpans.GetProperty("scopeSpans").EnumerateArray())
    {
        foreach (var span in scopeSpans.GetProperty("spans").EnumerateArray())
        {
            spanTraceMap[span.GetProperty("spanId").GetString()!] = span.GetProperty("traceId").GetString()!;
        }
    }
}

// Find the trace with the most spans
var spansByTrace = allSpans
    .GroupBy(s => spanTraceMap.GetValueOrDefault(s.SpanId, ""))
    .OrderByDescending(g => g.Count())
    .First();

var traceData = spansByTrace.ToArray();

Console.WriteLine($"Loaded {allSpans.Count} spans across {spanTraceMap.Values.Distinct().Count()} traces");
Console.WriteLine($"Showing trace with {traceData.Length} spans");

var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
        ctx.TraceTimeline(traceData)
            .FillHeight()
    )
    .Build();

await terminal.RunAsync();
