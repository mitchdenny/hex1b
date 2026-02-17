using System.Diagnostics;
using System.Threading.Channels;
using Hex1b;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

var options = PerfOptions.Parse(args);

if (options.Compare)
{
    var noCache = await RunOnceAsync(enableCaching: false, options);
    var cache = await RunOnceAsync(enableCaching: true, options);

    Print(noCache);
    Print(cache);
    Console.WriteLine();
    Console.WriteLine("Delta (cache - no-cache)");
    Console.WriteLine($"  Allocated: {FormatBytes(cache.AllocatedBytes - noCache.AllocatedBytes)}");
    Console.WriteLine($"  Bytes/frame: {(cache.BytesPerFrame - noCache.BytesPerFrame):N1}");
    return;
}

Print(await RunOnceAsync(options.EnableCaching, options));

static async Task<PerfResult> RunOnceAsync(bool enableCaching, PerfOptions options)
{
    var theme = Hex1bThemes.Default.Clone("perf")
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.LightGray)
        .Set(GlobalTheme.BackgroundColor, Hex1bColor.DarkGray)
        .Lock();

    var staticPanel = BuildStaticPanel(options.StaticLines);

    var warmupFrames = options.WarmupFrames;
    var measureFrames = options.MeasureFrames;
    var stopFrame = warmupFrames + measureFrames + 1; // extra frame to capture end measurements

    var frame = 0;
    var renderedFrames = 0;

    long startAllocated = 0;
    long endAllocated = 0;
    var startGen0 = 0;
    var startGen1 = 0;
    var startGen2 = 0;
    var endGen0 = 0;
    var endGen1 = 0;
    var endGen2 = 0;
    var startTimestamp = 0L;
    var endTimestamp = 0L;

    using var adapter = new PerfWorkloadAdapter(options.Width, options.Height, TerminalCapabilities.Minimal);

    Hex1bApp? app = null;
    app = new Hex1bApp(ctx =>
    {
        frame++;

        if (frame == warmupFrames + 1)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            startAllocated = GC.GetTotalAllocatedBytes(precise: true);
            startGen0 = GC.CollectionCount(0);
            startGen1 = GC.CollectionCount(1);
            startGen2 = GC.CollectionCount(2);
            startTimestamp = Stopwatch.GetTimestamp();
        }

        if (frame == stopFrame)
        {
            endTimestamp = Stopwatch.GetTimestamp();
            endAllocated = GC.GetTotalAllocatedBytes(precise: true);
            endGen0 = GC.CollectionCount(0);
            endGen1 = GC.CollectionCount(1);
            endGen2 = GC.CollectionCount(2);
            app!.RequestStop();
            return new TextBlockWidget("Stopping...");
        }

        var root = new VStackWidget(new Hex1bWidget[]
        {
            new TextBlockWidget($"Tick: {frame}"),
            staticPanel
        });
        renderedFrames++;
        return root;
    }, new Hex1bAppOptions
    {
        WorkloadAdapter = adapter,
        Theme = theme,
        EnableRenderCaching = enableCaching,
        EnableDefaultCtrlCExit = false,
        EnableInputCoalescing = false,
        EnableRescue = false,
        FrameRateLimitMs = 1
    });

    // Drive the app using invalidation signals. Triggering invalidation from within the widget
    // builder can be drained as part of the same render loop iteration, so we poke invalidation
    // from a timer instead.
    using var invalidateTimer = new System.Threading.Timer(
        static state => ((Hex1bApp)state!).Invalidate(),
        app,
        dueTime: TimeSpan.Zero,
        period: TimeSpan.FromMilliseconds(1));

    await app.RunAsync();

    var elapsed = (startTimestamp == 0 || endTimestamp == 0)
        ? TimeSpan.Zero
        : Stopwatch.GetElapsedTime(startTimestamp, endTimestamp);
    var allocatedBytes = endAllocated - startAllocated;

    return new PerfResult
    {
        EnableCaching = enableCaching,
        Width = options.Width,
        Height = options.Height,
        StaticLines = options.StaticLines,
        WarmupFrames = warmupFrames,
        MeasureFrames = measureFrames,
        RenderedFrames = renderedFrames,
        Elapsed = elapsed,
        AllocatedBytes = allocatedBytes,
        BytesPerFrame = measureFrames > 0 ? allocatedBytes / (double)measureFrames : 0,
        Gen0 = endGen0 - startGen0,
        Gen1 = endGen1 - startGen1,
        Gen2 = endGen2 - startGen2
    };
}

static Hex1bWidget BuildStaticPanel(int lines)
{
    var children = new Hex1bWidget[lines];
    for (var i = 0; i < children.Length; i++)
    {
        children[i] = new TextBlockWidget($"Static line {i:0000} - 0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZ");
    }

    return new BorderWidget(new VStackWidget(children))
        .Title("Static panel")
        .Cached(static _ => true);
}

static void Print(PerfResult result)
{
    Console.WriteLine(result.EnableCaching ? "Caching: ON" : "Caching: OFF");
    Console.WriteLine($"  Size: {result.Width}x{result.Height}");
    Console.WriteLine($"  Static lines: {result.StaticLines}");
    Console.WriteLine($"  Warmup frames: {result.WarmupFrames}");
    Console.WriteLine($"  Measured frames: {result.MeasureFrames}");
    Console.WriteLine($"  Allocated: {FormatBytes(result.AllocatedBytes)}");
    Console.WriteLine($"  Bytes/frame: {result.BytesPerFrame:N1}");
    Console.WriteLine($"  GC (0/1/2): {result.Gen0}/{result.Gen1}/{result.Gen2}");
    if (result.Elapsed > TimeSpan.Zero)
    {
        Console.WriteLine($"  Elapsed: {result.Elapsed.TotalMilliseconds:N1} ms");
    }
}

static string FormatBytes(long bytes)
{
    var sign = bytes < 0 ? "-" : "";
    var absBytes = Math.Abs((double)bytes);
    const double Kb = 1024.0;
    const double Mb = 1024.0 * 1024.0;
    if (absBytes >= Mb) return $"{sign}{absBytes / Mb:N2} MB";
    if (absBytes >= Kb) return $"{sign}{absBytes / Kb:N2} KB";
    return $"{bytes:N0} B";
}

internal sealed record PerfOptions(
    bool EnableCaching,
    bool Compare,
    int Width,
    int Height,
    int StaticLines,
    int WarmupFrames,
    int MeasureFrames)
{
    public static PerfOptions Parse(string[] args)
    {
        static bool HasFlag(string[] a, string flag)
            => Array.IndexOf(a, flag) >= 0;

        static int ReadInt(string[] a, string name, int defaultValue)
        {
            var idx = Array.IndexOf(a, name);
            if (idx < 0 || idx + 1 >= a.Length) return defaultValue;
            return int.TryParse(a[idx + 1], out var value) ? value : defaultValue;
        }

        var compare = HasFlag(args, "--compare");
        var enableCaching = HasFlag(args, "--cache");

        var width = ReadInt(args, "--width", 120);
        var height = ReadInt(args, "--height", 40);
        var staticLines = ReadInt(args, "--lines", 200);
        var warmup = ReadInt(args, "--warmup", 25);
        var frames = ReadInt(args, "--frames", 200);

        return new PerfOptions(
            EnableCaching: enableCaching,
            Compare: compare,
            Width: width,
            Height: height,
            StaticLines: staticLines,
            WarmupFrames: warmup,
            MeasureFrames: frames);
    }
}

internal sealed record PerfResult
{
    public required bool EnableCaching { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int StaticLines { get; init; }
    public required int WarmupFrames { get; init; }
    public required int MeasureFrames { get; init; }
    public required int RenderedFrames { get; init; }
    public required TimeSpan Elapsed { get; init; }
    public required long AllocatedBytes { get; init; }
    public required double BytesPerFrame { get; init; }
    public required int Gen0 { get; init; }
    public required int Gen1 { get; init; }
    public required int Gen2 { get; init; }
}

internal sealed class PerfWorkloadAdapter : IHex1bAppTerminalWorkloadAdapter, IDisposable
{
    private readonly Channel<Hex1bEvent> _input = Channel.CreateUnbounded<Hex1bEvent>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = true
    });

    public PerfWorkloadAdapter(int width, int height, TerminalCapabilities capabilities)
    {
        Width = width;
        Height = height;
        Capabilities = capabilities;
    }

    public void Write(string text)
    {
        // Intentionally discard output. The goal is to avoid extra allocations
        // from encoding output to bytes so we can focus on render-side allocations.
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        // Intentionally discard.
    }

    public void Flush()
    {
    }

    public ChannelReader<Hex1bEvent> InputEvents => _input.Reader;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public TerminalCapabilities Capabilities { get; }

    public int OutputQueueDepth => 0;

    public void EnterTuiMode()
    {
    }

    public void ExitTuiMode()
    {
    }

    public void Clear()
    {
    }

    public void SetCursorPosition(int left, int top)
    {
    }

    public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        Width = width;
        Height = height;
        return ValueTask.CompletedTask;
    }

    #pragma warning disable CS0067 // The event is part of the interface; this adapter never disconnects.
    public event Action? Disconnected;
    #pragma warning restore CS0067

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
        // no-op
    }
}
