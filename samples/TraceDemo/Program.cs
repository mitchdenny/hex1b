using Hex1b;
using Hex1b.Charts;

var traceStart = new DateTimeOffset(2026, 4, 18, 10, 0, 0, TimeSpan.Zero);

var traceData = new[]
{
    new TraceSpanItem("span-1", null, "GET /api/orders", "api-gateway",
        traceStart, TimeSpan.FromMilliseconds(450)),
    new TraceSpanItem("span-2", "span-1", "authenticate", "auth-svc",
        traceStart.AddMilliseconds(20), TimeSpan.FromMilliseconds(100)),
    new TraceSpanItem("span-3", "span-1", "fetch-orders", "order-svc",
        traceStart.AddMilliseconds(130), TimeSpan.FromMilliseconds(250)),
    new TraceSpanItem("span-4", "span-3", "SELECT * FROM orders", "db",
        traceStart.AddMilliseconds(140), TimeSpan.FromMilliseconds(140)),
    new TraceSpanItem("span-5", "span-3", "cache.put", "cache-svc",
        traceStart.AddMilliseconds(290), TimeSpan.FromMilliseconds(60)),
    new TraceSpanItem("span-6", "span-1", "serialize-response", "api-gateway",
        traceStart.AddMilliseconds(390), TimeSpan.FromMilliseconds(40),
        Status: TraceSpanStatus.Error),
};

var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
        ctx.TraceTimeline(traceData)
            .FillHeight()
    )
    .Build();

await terminal.RunAsync();
