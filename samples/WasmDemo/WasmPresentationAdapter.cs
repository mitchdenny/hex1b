using System.Runtime.InteropServices.JavaScript;

namespace GlobeDemoWasm;

/// <summary>
/// Implements IHex1bTerminalPresentationAdapter by bridging to xterm.js
/// via Web Worker postMessage interop. Uses polling via JSImport to read
/// input queued by the JS onmessage handler. With WasmEnableThreads,
/// .NET's internal threading (async/await, ThreadPool) works properly.
/// </summary>
public sealed partial class WasmPresentationAdapter : Hex1b.IHex1bTerminalPresentationAdapter
{
    private int _width;
    private int _height;

    // Event-driven input signal — replaces the previous 50ms poll loop.
    // JS calls SignalInputAvailable() via [JSExport] whenever new input or a
    // resize arrives; ReadInputAsync drains the JS queues, and if nothing is
    // available, awaits this TCS instead of sleeping.
    private static TaskCompletionSource? s_inputSignal;

    public WasmPresentationAdapter(int initialCols = 80, int initialRows = 24)
    {
        _width = initialCols;
        _height = initialRows;
    }

    public int Width => _width;
    public int Height => _height;

    public Hex1b.TerminalCapabilities Capabilities => new()
    {
        SupportsTrueColor = true,
        Supports256Colors = true,
        SupportsAlternateScreen = true,
        SupportsBracketedPaste = true,
        SupportsSixel = false,
        SupportsMouse = true
    };

    public event Action<int, int>? Resized;
    public event Action? Disconnected;

    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        PostOutput(data.ToArray());
        return ValueTask.CompletedTask;
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Check for resize — defer event fire via Task.Run to avoid
            // synchronous re-entrancy within ReadInputAsync. In single-threaded
            // WASM, Task.Run schedules on the next event loop tick, breaking the
            // chain that was killing the animation timer.
            var resize = PollResize();
            if (!string.IsNullOrEmpty(resize))
            {
                var parts = resize.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out var cols) && int.TryParse(parts[1], out var rows))
                {
                    if (cols != _width || rows != _height)
                    {
                        _width = cols;
                        _height = rows;
                        _ = Task.Run(() => Resized?.Invoke(cols, rows));
                    }
                }
            }

            // Drain all queued input at once for responsiveness
            var allInput = PollAllInput();
            if (allInput != null && allInput.Length > 0)
            {
                return new ReadOnlyMemory<byte>(allInput);
            }

            // Nothing pending — install a fresh signal and wait for JS to ping us.
            // Re-check the JS queues after publishing the TCS to close the race
            // where input arrived between our drain and registration.
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.Exchange(ref s_inputSignal, tcs);

            var lateInput = PollAllInput();
            var lateResize = PollResize();
            if ((lateInput != null && lateInput.Length > 0) || !string.IsNullOrEmpty(lateResize))
            {
                // Consume the signal we just installed so a future Signal call
                // doesn't fire against a stale TCS.
                Interlocked.CompareExchange(ref s_inputSignal, null, tcs);
                if (lateInput != null && lateInput.Length > 0)
                {
                    return new ReadOnlyMemory<byte>(lateInput);
                }
                // We had a resize but no input bytes — loop so we process the resize
                // through the normal path above (which fires Resized and re-polls).
                continue;
            }

            try
            {
                using var reg = ct.Register(static state => ((TaskCompletionSource)state!).TrySetCanceled(), tcs);
                // Race the signal against a 50ms poll fallback. When SignalInputAvailable
                // is wired via [JSExport] (the f1 fast path), tcs.Task wins and we get
                // sub-millisecond wake-up. When the JS-side wiring fails for any reason,
                // Task.Delay wins and we degrade to the original 50ms poll behaviour
                // instead of blocking ReadInputAsync forever.
                await Task.WhenAny(tcs.Task, Task.Delay(50, ct));
                Interlocked.CompareExchange(ref s_inputSignal, null, tcs);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return ReadOnlyMemory<byte>.Empty;
    }

    public ValueTask FlushAsync(CancellationToken ct) => ValueTask.CompletedTask;
    public ValueTask EnterRawModeAsync(CancellationToken ct) => ValueTask.CompletedTask;
    public ValueTask ExitRawModeAsync(CancellationToken ct) => ValueTask.CompletedTask;

    public (int Row, int Column) GetCursorPosition() => (0, 0);

    public ValueTask DisposeAsync()
    {
        Disconnected?.Invoke();
        return ValueTask.CompletedTask;
    }

    [JSImport("postTerminalOutput", "main.js")]
    internal static partial void PostOutput(byte[] data);

    [JSImport("notifyReady", "main.js")]
    internal static partial void NotifyReady(int cols, int rows);

    [JSImport("pollAllInput", "main.js")]
    internal static partial byte[]? PollAllInput();

    [JSImport("pollResize", "main.js")]
    internal static partial string PollResize();

    /// <summary>
    /// Called from JS (worker's interop.js) whenever new input or a resize
    /// has been enqueued. Wakes any pending <see cref="ReadInputAsync"/> call
    /// immediately so input latency does not depend on a poll interval.
    /// </summary>
    [JSExport]
    internal static void SignalInputAvailable()
    {
        var tcs = Interlocked.Exchange(ref s_inputSignal, null);
        tcs?.TrySetResult();
    }

    internal static WasmPresentationAdapter? Instance { get; set; }
}
