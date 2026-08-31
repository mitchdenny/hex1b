using System.Threading.Channels;
using Hex1b;

namespace KgpValidation;

/// <summary>
/// Raw workload that drives the validation pages without Hex1bApp or widgets.
/// </summary>
/// <remarks>
/// Output is a channel of complete ANSI/KGP frames. Input is intentionally
/// limited to a few printable navigation keys and common cursor-key sequences,
/// keeping the harness independent of Hex1b's widget input router.
/// </remarks>
internal sealed class KgpValidationWorkload : IHex1bTerminalWorkloadAdapter
{
    private readonly object _gate = new();
    private readonly Channel<ReadOnlyMemory<byte>> _output;
    private readonly IReadOnlyList<KgpValidationScenario> _scenarios;
    private int _scenarioIndex;
    private int _scenarioVariant;
    private int _width = 80;
    private int _height = 24;
    private bool _enteredAlternateScreen;
    private bool _exitRequested;
    private bool _disconnected;
    private bool _disposed;

    public KgpValidationWorkload()
        : this(KgpScenarioCatalog.All)
    {
    }

    public KgpValidationWorkload(
        IReadOnlyList<KgpValidationScenario> scenarios)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        if (scenarios.Count == 0)
            throw new ArgumentException("At least one scenario is required.", nameof(scenarios));

        _scenarios = scenarios;
        _output = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            });
    }

    public int CurrentScenarioIndex
    {
        get
        {
            lock (_gate)
                return _scenarioIndex;
        }
    }

    public int ScenarioCount => _scenarios.Count;

    public int CurrentScenarioVariant
    {
        get
        {
            lock (_gate)
                return _scenarioVariant;
        }
    }

    public event Action? Disconnected;

    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(
        CancellationToken ct = default)
    {
        try
        {
            while (await _output.Reader.WaitToReadAsync(ct))
            {
                if (_output.Reader.TryRead(out var data))
                    return data;
            }
        }
        catch (OperationCanceledException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
        catch (ChannelClosedException)
        {
            // Completion is handled below.
        }

        SignalDisconnected();
        return ReadOnlyMemory<byte>.Empty;
    }

    public ValueTask WriteInputAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var request = ParseNavigation(data.Span);
        if (request == NavigationRequest.None)
            return ValueTask.CompletedTask;

        lock (_gate)
        {
            if (_disposed || _exitRequested)
                return ValueTask.CompletedTask;

            switch (request)
            {
                case NavigationRequest.Next:
                    SelectScenario((_scenarioIndex + 1) % _scenarios.Count);
                    QueueCurrentFrame();
                    break;
                case NavigationRequest.Previous:
                    SelectScenario(
                        (_scenarioIndex - 1 + _scenarios.Count) % _scenarios.Count);
                    QueueCurrentFrame();
                    break;
                case NavigationRequest.First:
                    SelectScenario(0);
                    QueueCurrentFrame();
                    break;
                case NavigationRequest.Last:
                    SelectScenario(_scenarios.Count - 1);
                    QueueCurrentFrame();
                    break;
                case NavigationRequest.ToggleVariant:
                    var count = _scenarios[_scenarioIndex].VariantCount;
                    if (count > 1)
                    {
                        _scenarioVariant = (_scenarioVariant + 1) % count;
                        QueueCurrentFrame();
                    }
                    break;
                case >= NavigationRequest.Jump1 and <= NavigationRequest.Jump9:
                    var target = request - NavigationRequest.Jump1;
                    if ((int)target < _scenarios.Count)
                    {
                        SelectScenario((int)target);
                        QueueCurrentFrame();
                    }
                    break;
                case NavigationRequest.Exit:
                    RequestExitLocked();
                    break;
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ResizeAsync(
        int width,
        int height,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_disposed || _exitRequested)
                return ValueTask.CompletedTask;

            _width = Math.Max(1, width);
            _height = Math.Max(1, height);
            QueueCurrentFrame();
        }
        return ValueTask.CompletedTask;
    }

    public void RequestExit()
    {
        lock (_gate)
            RequestExitLocked();
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
                return ValueTask.CompletedTask;

            _disposed = true;
            _output.Writer.TryComplete();
        }
        SignalDisconnected();
        return ValueTask.CompletedTask;
    }

    private void QueueCurrentFrame()
    {
        var frame = KgpValidationFrameRenderer.Render(
            _scenarios[_scenarioIndex],
            _scenarioIndex,
            _scenarios.Count,
            _width,
            _height,
            enterAlternateScreen: !_enteredAlternateScreen,
            variant: _scenarioVariant);
        _enteredAlternateScreen = true;
        _output.Writer.TryWrite(frame);
    }

    private void SelectScenario(int scenarioIndex)
    {
        _scenarioIndex = scenarioIndex;
        _scenarioVariant = 0;
    }

    private void RequestExitLocked()
    {
        if (_exitRequested || _disposed)
            return;

        _exitRequested = true;
        _output.Writer.TryWrite(KgpValidationFrameRenderer.Cleanup());
        _output.Writer.TryComplete();
    }

    private void SignalDisconnected()
    {
        Action? disconnected;
        lock (_gate)
        {
            if (_disconnected)
                return;
            _disconnected = true;
            disconnected = Disconnected;
        }
        disconnected?.Invoke();
    }

    private static NavigationRequest ParseNavigation(ReadOnlySpan<byte> data)
    {
        if (data.SequenceEqual("\x1b[C"u8) ||
            data.SequenceEqual("\x1bOC"u8) ||
            data.SequenceEqual("\x1b[6~"u8))
        {
            return NavigationRequest.Next;
        }

        if (data.SequenceEqual("\x1b[D"u8) ||
            data.SequenceEqual("\x1bOD"u8) ||
            data.SequenceEqual("\x1b[5~"u8))
        {
            return NavigationRequest.Previous;
        }

        if (data.SequenceEqual("\x1b[H"u8) ||
            data.SequenceEqual("\x1bOH"u8) ||
            data.SequenceEqual("\x1b[1~"u8) ||
            data.SequenceEqual("\x1b[7~"u8))
        {
            return NavigationRequest.First;
        }

        if (data.SequenceEqual("\x1b[F"u8) ||
            data.SequenceEqual("\x1bOF"u8) ||
            data.SequenceEqual("\x1b[4~"u8) ||
            data.SequenceEqual("\x1b[8~"u8))
        {
            return NavigationRequest.Last;
        }

        if (!data.IsEmpty && data[0] == 0x1B)
        {
            return data.Length == 1
                ? NavigationRequest.Exit
                : NavigationRequest.None;
        }

        foreach (var value in data)
        {
            switch (value)
            {
                case (byte)'n':
                case (byte)'N':
                case (byte)' ':
                case (byte)'\r':
                    return NavigationRequest.Next;
                case (byte)'p':
                case (byte)'P':
                case 0x7F:
                    return NavigationRequest.Previous;
                case (byte)'r':
                case (byte)'R':
                    return NavigationRequest.ToggleVariant;
                case (byte)'q':
                case (byte)'Q':
                case 0x03:
                    return NavigationRequest.Exit;
                case >= (byte)'1' and <= (byte)'9':
                    return NavigationRequest.Jump1 + (value - (byte)'1');
            }
        }

        return NavigationRequest.None;
    }

    private enum NavigationRequest
    {
        None,
        Next,
        Previous,
        First,
        Last,
        ToggleVariant,
        Jump1,
        Jump2,
        Jump3,
        Jump4,
        Jump5,
        Jump6,
        Jump7,
        Jump8,
        Jump9,
        Exit,
    }
}
