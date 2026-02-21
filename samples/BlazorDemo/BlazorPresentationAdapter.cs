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

            var allInput = PollAllInput();
            if (allInput != null && allInput.Length > 0)
            {
                return new ReadOnlyMemory<byte>(allInput);
            }

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

    [JSImport("globalThis.termInterop.postOutput")]
    internal static partial void PostOutput(byte[] data);

    [JSImport("globalThis.termInterop.pollAllInput")]
    internal static partial byte[]? PollAllInput();

    [JSImport("globalThis.termInterop.pollResize")]
    internal static partial string PollResize();
}
