using System.Threading.Channels;
using Hex1b;
using Hex1b.Tokens;

namespace WpfTerm;

/// <summary>
/// Bridges Hex1b's terminal engine to a WPF rendering surface.
/// Receives pre-parsed cell impacts (no ANSI parsing needed) and
/// exposes the screen buffer for the WPF control to render from.
/// </summary>
public sealed class WpfTerminalAdapter : ICellImpactAwarePresentationAdapter, ITerminalLifecycleAwarePresentationAdapter, IAsyncDisposable
{
    private readonly object _bufferLock = new();
    private readonly TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<byte[]> _inputChannel = Channel.CreateUnbounded<byte[]>(
        new UnboundedChannelOptions { SingleReader = true });

    private TerminalCell[,] _screenBuffer;
    private int _width;
    private int _height;
    private int _cursorX;
    private int _cursorY;
    private bool _cursorVisible = true;
    private CursorShape _cursorShape = CursorShape.Default;
    private bool _mouseTrackingEnabled;
    private bool _sgrMouseModeEnabled;
    private bool _disposed;
    private Hex1bTerminal? _terminal;

    public WpfTerminalAdapter(int width = 120, int height = 30)
    {
        _width = width;
        _height = height;
        _screenBuffer = new TerminalCell[height, width];
        ClearBuffer();
    }

    public int Width => _width;
    public int Height => _height;
    public int CursorX => _cursorX;
    public int CursorY => _cursorY;
    public bool CursorVisible => _cursorVisible;
    public CursorShape CursorShape => _cursorShape;

    /// <summary>
    /// Whether the child process has enabled mouse tracking (modes 1000/1002/1003).
    /// </summary>
    public bool MouseTrackingEnabled => _mouseTrackingEnabled;

    public TerminalCapabilities Capabilities { get; } = new()
    {
        SupportsMouse = true,
        Supports256Colors = true,
        SupportsTrueColor = true,
        SupportsKgp = true,
    };

    public event Action<int, int>? Resized;
    public event Action? Disconnected;

    /// <summary>
    /// Raised when the screen buffer has been updated with new output.
    /// The WPF control subscribes to this to trigger re-renders.
    /// </summary>
    public event Action? OutputReceived;

    /// <summary>
    /// Gets a snapshot of the current screen buffer for rendering.
    /// </summary>
    public (TerminalCell[,] Buffer, int Width, int Height, int CursorX, int CursorY, bool CursorVisible) GetSnapshot()
    {
        lock (_bufferLock)
        {
            var copy = new TerminalCell[_height, _width];
            Array.Copy(_screenBuffer, copy, _screenBuffer.Length);
            return (copy, _width, _height, _cursorX, _cursorY, _cursorVisible);
        }
    }

    /// <summary>
    /// Allows the renderer to read the buffer directly under lock — zero-copy path.
    /// The callback must not capture or store the buffer reference.
    /// </summary>
    public void RenderUnderLock(Action<TerminalCell[,], int, int, int, int, bool, CursorShape> renderCallback)
    {
        lock (_bufferLock)
        {
            renderCallback(_screenBuffer, _width, _height, _cursorX, _cursorY, _cursorVisible, _cursorShape);
        }
    }

    /// <summary>
    /// Enqueues raw ANSI input bytes to be sent to the PTY process.
    /// Called by the WPF keyboard handler.
    /// </summary>
    public void EnqueueInput(byte[] data)
    {
        if (!_disposed)
        {
            _inputChannel.Writer.TryWrite(data);
        }
    }

    /// <summary>
    /// Triggers a resize of the terminal dimensions.
    /// Call this when the WPF control changes size.
    /// </summary>
    public void TriggerResize(int newWidth, int newHeight)
    {
        if (newWidth <= 0 || newHeight <= 0) return;
        if (newWidth == _width && newHeight == _height) return;

        lock (_bufferLock)
        {
            var newBuffer = new TerminalCell[newHeight, newWidth];
            for (int y = 0; y < newHeight; y++)
                for (int x = 0; x < newWidth; x++)
                    newBuffer[y, x] = TerminalCell.Empty;

            // Copy existing content
            int copyRows = Math.Min(_height, newHeight);
            int copyCols = Math.Min(_width, newWidth);
            for (int y = 0; y < copyRows; y++)
                for (int x = 0; x < copyCols; x++)
                    newBuffer[y, x] = _screenBuffer[y, x];

            _screenBuffer = newBuffer;
            _width = newWidth;
            _height = newHeight;
        }

        Resized?.Invoke(newWidth, newHeight);
    }

    // === ICellImpactAwarePresentationAdapter ===

    public ValueTask WriteOutputWithImpactsAsync(IReadOnlyList<AppliedToken> appliedTokens, CancellationToken ct = default)
    {
        if (_disposed || appliedTokens.Count == 0)
            return ValueTask.CompletedTask;

        bool hasChanges = false;
        lock (_bufferLock)
        {
            foreach (var applied in appliedTokens)
            {
                // Track mode changes
                if (applied.Token is PrivateModeToken pm)
                {
                    if (pm.Mode == 25)
                    {
                        _cursorVisible = pm.Enable;
                        hasChanges = true;
                    }
                    // Mouse tracking modes
                    if (pm.Mode is 1000 or 1002 or 1003) _mouseTrackingEnabled = pm.Enable;
                    if (pm.Mode == 1006) _sgrMouseModeEnabled = pm.Enable;
                }

                // Track cursor shape changes
                if (applied.Token is CursorShapeToken cst)
                {
                    _cursorShape = cst.Shape switch
                    {
                        1 => CursorShape.BlinkingBlock,
                        2 => CursorShape.SteadyBlock,
                        3 => CursorShape.BlinkingUnderline,
                        4 => CursorShape.SteadyUnderline,
                        5 => CursorShape.BlinkingBar,
                        6 => CursorShape.SteadyBar,
                        _ => CursorShape.Default
                    };
                    hasChanges = true;
                }

                // KGP tokens modify placements outside the cell buffer — still need a re-render
                if (applied.Token is KgpToken)
                {
                    hasChanges = true;
                }

                // Apply cell impacts
                foreach (var impact in applied.CellImpacts)
                {
                    if (impact.X >= 0 && impact.X < _width && impact.Y >= 0 && impact.Y < _height)
                    {
                        _screenBuffer[impact.Y, impact.X] = impact.Cell;
                        hasChanges = true;
                    }
                }

                // Track cursor position — trigger re-render if cursor moved
                int newCursorX = Math.Clamp(applied.CursorXAfter, 0, Math.Max(0, _width - 1));
                int newCursorY = Math.Clamp(applied.CursorYAfter, 0, Math.Max(0, _height - 1));
                if (newCursorX != _cursorX || newCursorY != _cursorY)
                {
                    _cursorX = newCursorX;
                    _cursorY = newCursorY;
                    hasChanges = true;
                }
            }
        }

        if (hasChanges)
        {
            OutputReceived?.Invoke();
        }

        return ValueTask.CompletedTask;
    }

    // === IHex1bTerminalPresentationAdapter ===

    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        // Fallback path — should not be called since we implement ICellImpactAwarePresentationAdapter
        if (!_disposed && !data.IsEmpty)
            OutputReceived?.Invoke();
        return ValueTask.CompletedTask;
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        if (_disposed)
            return ReadOnlyMemory<byte>.Empty;

        try
        {
            var data = await _inputChannel.Reader.ReadAsync(ct);
            return data;
        }
        catch (OperationCanceledException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
        catch (ChannelClosedException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
    public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
    public (int Row, int Column) GetCursorPosition() => (_cursorY, _cursorX);

    // === ITerminalLifecycleAwarePresentationAdapter ===

    void ITerminalLifecycleAwarePresentationAdapter.TerminalCreated(Hex1bTerminal terminal)
    {
        _terminal = terminal;
    }

    void ITerminalLifecycleAwarePresentationAdapter.TerminalStarted() { }

    void ITerminalLifecycleAwarePresentationAdapter.TerminalCompleted(int exitCode) { }

    /// <summary>
    /// Gets the active KGP placements from the terminal, or empty if not available.
    /// </summary>
    public IReadOnlyList<KgpPlacement> GetKgpPlacements()
        => _terminal?.KgpPlacements ?? [];

    /// <summary>
    /// Gets KGP image data by image ID from the terminal's image store.
    /// </summary>
    public KgpImageData? GetKgpImage(uint imageId)
        => _terminal?.KgpImageStore.GetImageById(imageId);

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        _inputChannel.Writer.TryComplete();
        Disconnected?.Invoke();
        _disconnected.TrySetResult();

        return ValueTask.CompletedTask;
    }

    private void ClearBuffer()
    {
        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
                _screenBuffer[y, x] = TerminalCell.Empty;
    }
}
