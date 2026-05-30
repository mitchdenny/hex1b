using System.Runtime.InteropServices.JavaScript;

namespace BlazorDemo;

/// <summary>
/// IHex1bTerminalPresentationAdapter for Blazor WASM using [JSImport] for fast interop.
/// Runs on the main thread — no cross-thread marshaling overhead.
/// </summary>
public sealed partial class BlazorPresentationAdapter : Hex1b.IHex1bTerminalPresentationAdapter
{
    private int _width;
    private int _height;

    // Event-driven input signal — see WasmPresentationAdapter for rationale.
    private static TaskCompletionSource? s_inputSignal;

    public BlazorPresentationAdapter(int initialCols = 80, int initialRows = 24)
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
                Console.WriteLine($"[diag] ReadInputAsync returning {allInput.Length}B (fast path)");
                return new ReadOnlyMemory<byte>(allInput);
            }

            // Install a signal and recheck to close the race between drain and registration.
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.Exchange(ref s_inputSignal, tcs);

            var lateInput = PollAllInput();
            var lateResize = PollResize();
            if ((lateInput != null && lateInput.Length > 0) || !string.IsNullOrEmpty(lateResize))
            {
                Interlocked.CompareExchange(ref s_inputSignal, null, tcs);
                if (lateInput != null && lateInput.Length > 0)
                {
                    Console.WriteLine($"[diag] ReadInputAsync returning {lateInput.Length}B (late-drain path)");
                    return new ReadOnlyMemory<byte>(lateInput);
                }
                continue;
            }

            try
            {
                using var reg = ct.Register(static state => ((TaskCompletionSource)state!).TrySetCanceled(), tcs);
                // Race the signal against a 50ms poll fallback. When SignalInputAvailable
                // is wired via [JSExport] (the f1 fast path), tcs.Task wins and we get
                // sub-millisecond wake-up. When the runtime-API wiring fails to attach
                // (e.g. globalThis.getDotnetRuntime not exposed in the current Blazor
                // boot path), Task.Delay wins and we degrade to the original 50ms poll
                // behaviour instead of blocking ReadInputAsync forever.
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

    [JSImport("globalThis.termInterop.postOutput")]
    internal static partial void PostOutput(byte[] data);

    [JSImport("globalThis.termInterop.pollAllInput")]
    internal static partial byte[]? PollAllInput();

    [JSImport("globalThis.termInterop.pollResize")]
    internal static partial string PollResize();

    /// <summary>
    /// Called from interop.js whenever new input or a resize has been enqueued.
    /// Wakes any pending <see cref="ReadInputAsync"/> call immediately so input
    /// latency does not depend on a poll interval.
    /// </summary>
    [JSExport]
    internal static void SignalInputAvailable()
    {
        var tcs = Interlocked.Exchange(ref s_inputSignal, null);
        tcs?.TrySetResult();
    }
}
