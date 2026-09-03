using System.Threading.Channels;
using Hex1b;

/// <summary>
/// Drives the demo one screen at a time, advancing only when the viewer presses a
/// key.
/// </summary>
/// <remarks>
/// <para>
/// The terminal pulls bytes through <see cref="ReadOutputAsync"/> and pushes
/// keystrokes back through <see cref="WriteInputAsync"/>, so paging is expressed
/// as a workload that simply refuses to produce the next screen until a key
/// arrives. That keeps the demo an ordinary workload rather than something that
/// reaches around the terminal.
/// </para>
/// <para>
/// Navigation is deliberately small: Enter or Space moves forward, <c>p</c> moves
/// back, and <c>q</c> or Ctrl+C quits.
/// </para>
/// </remarks>
internal sealed class PagedScreenWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    private readonly IReadOnlyList<DemoScreen> _screens;
    private readonly int _catalogueTotal;
    private readonly int _promptRow;
    private readonly Channel<DemoNavigation> _input =
        Channel.CreateUnbounded<DemoNavigation>(new UnboundedChannelOptions
        {
            SingleReader = true,
        });

    private readonly object _eventLock = new();
    private Action? _disconnected;
    private bool _completed;

    private int _index;
    private bool _promptPending;
    private bool _awaitingInput;
    private bool _quit;

    /// <param name="screens">The screens to page through, which may be a filtered subset.</param>
    /// <param name="catalogueTotal">
    /// The total number of screens in the full demo. Reported alongside each screen
    /// number so a filtered run still shows a screen's real catalogue position.
    /// </param>
    /// <param name="promptRow">The row the footer prompt is drawn on.</param>
    public PagedScreenWorkloadAdapter(
        IReadOnlyList<DemoScreen> screens,
        int catalogueTotal,
        int promptRow)
    {
        _screens = screens;
        _catalogueTotal = catalogueTotal;
        _promptRow = promptRow;
    }

    public event Action? Disconnected
    {
        add
        {
            var invokeNow = false;
            lock (_eventLock)
            {
                _disconnected += value;
                invokeNow = _completed;
            }
            if (invokeNow)
                value?.Invoke();
        }
        remove
        {
            lock (_eventLock)
                _disconnected -= value;
        }
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        // A screen is delivered as two reads: the graphic, then the description and
        // prompt. The wait for input happens at the START of the read that follows,
        // so the description and prompt are already flushed to the terminal before
        // anything blocks. Awaiting before returning them would hold them back until
        // the key had already been pressed, leaving the screen with no visible prompt.
        if (_awaitingInput)
        {
            _awaitingInput = false;

            DemoNavigation navigation;
            try
            {
                navigation = await _input.Reader.ReadAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Finish();
            }

            switch (navigation)
            {
                case DemoNavigation.Quit:
                    _quit = true;
                    break;
                case DemoNavigation.Previous:
                    _index = Math.Max(0, _index - 1);
                    break;
                default:
                    _index++;
                    break;
            }
        }

        if (_quit || _index >= _screens.Count)
        {
            return Finish();
        }

        var screen = _screens[_index];

        if (!_promptPending)
        {
            _promptPending = true;
            return DemoScreenRenderer.Render(screen, _catalogueTotal);
        }

        _promptPending = false;
        _awaitingInput = true;

        return DemoScreenRenderer.RenderPrompt(
            screen,
            _catalogueTotal,
            _promptRow,
            isLast: _index == _screens.Count - 1);
    }

    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        foreach (var value in data.Span)
        {
            var navigation = value switch
            {
                // CR and LF both appear depending on the upstream terminal's mode.
                (byte)'\r' or (byte)'\n' or (byte)' ' => DemoNavigation.Next,
                (byte)'p' or (byte)'P' => DemoNavigation.Previous,
                // q, Ctrl+C, and Escape all leave.
                (byte)'q' or (byte)'Q' or 0x03 or 0x1b => DemoNavigation.Quit,
                _ => (DemoNavigation?)null,
            };

            if (navigation is { } resolved)
            {
                _input.Writer.TryWrite(resolved);
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        _input.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    private ReadOnlyMemory<byte> Finish()
    {
        Action? disconnected;
        lock (_eventLock)
        {
            _completed = true;
            disconnected = _disconnected;
        }
        disconnected?.Invoke();
        return ReadOnlyMemory<byte>.Empty;
    }
}

internal enum DemoNavigation
{
    Next,
    Previous,
    Quit,
}
