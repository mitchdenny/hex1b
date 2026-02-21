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

            // Yield to JS event loop — longer delay reduces frame rate
            // so each frame has visible rotation delta in the braille grid
            try
            {
                await Task.Delay(50, ct);
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

    internal static WasmPresentationAdapter? Instance { get; set; }
}
