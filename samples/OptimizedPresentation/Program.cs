using Hex1b;
using Hex1b.Terminal;
using System.Text;
using System.Threading;
using System;
using System.Threading.Tasks;

// This sample demonstrates the OptimizedPresentationAdapter
// which suppresses redundant ANSI sequences to dramatically reduce output

Console.OutputEncoding = Encoding.UTF8;

// Track how much data is actually sent to the console
var bytesWritten = 0;
var originalAdapter = new ConsolePresentationAdapter();

// Wrap it in an optimized adapter
var optimizedAdapter = new OptimizedPresentationAdapter(originalAdapter);

// Create a custom tracking adapter to count bytes
var trackingAdapter = new TrackingPresentationAdapter(optimizedAdapter);
trackingAdapter.OnWrite += (data) => bytesWritten += data.Length;

// Set up the workload adapter
var workload = new Hex1bAppWorkloadAdapter(trackingAdapter.Capabilities);

// Create the terminal
using var terminal = new Hex1bTerminal(trackingAdapter, workload);

var counter = 0;

// Run update loop in background
var cts = new CancellationTokenSource();
var updateTask = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        await Task.Delay(100);
        counter++;
    }
});

try
{
    await using var app = new Hex1bApp(
        ctx => ctx.VStack(children => [
            ctx.Text("OptimizedPresentationAdapter Demo"),
            ctx.Text(""),
            ctx.Text($"Counter: {counter}"),
            ctx.Text($"Bytes sent to console: {bytesWritten:N0}"),
            ctx.Text(""),
            ctx.Text("The counter increments but the byte count should"),
            ctx.Text("stay relatively low because most updates are suppressed!"),
            ctx.Text(""),
            ctx.Text("Press ESC to exit"),
        ]),
        new Hex1bAppOptions
        {
            WorkloadAdapter = workload
        }
    );

    await app.RunAsync(cts.Token);
}
finally
{
    cts.Cancel();
    await updateTask;
}

// Custom adapter that tracks bytes written
class TrackingPresentationAdapter : IHex1bTerminalPresentationAdapter
{
    private readonly IHex1bTerminalPresentationAdapter _inner;

    public TrackingPresentationAdapter(IHex1bTerminalPresentationAdapter inner)
    {
        _inner = inner;
        _inner.Resized += (w, h) => Resized?.Invoke(w, h);
        _inner.Disconnected += () => Disconnected?.Invoke();
    }

    public event Action<ReadOnlyMemory<byte>>? OnWrite;

    public int Width => _inner.Width;
    public int Height => _inner.Height;
    public TerminalCapabilities Capabilities => _inner.Capabilities;
    public event Action<int, int>? Resized;
    public event Action? Disconnected;

    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        OnWrite?.Invoke(data);
        return _inner.WriteOutputAsync(data, ct);
    }

    public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        => _inner.ReadInputAsync(ct);

    public ValueTask FlushAsync(CancellationToken ct = default)
        => _inner.FlushAsync(ct);

    public ValueTask EnterTuiModeAsync(CancellationToken ct = default)
        => _inner.EnterTuiModeAsync(ct);

    public ValueTask ExitTuiModeAsync(CancellationToken ct = default)
        => _inner.ExitTuiModeAsync(ct);

    public ValueTask DisposeAsync()
        => _inner.DisposeAsync();
}

