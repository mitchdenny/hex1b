using System.Text;
using Hex1b;

/// <summary>
/// Drives the dust cloud animation by emitting raw escape sequences, one frame per
/// <see cref="ReadOutputAsync"/> call.
/// </summary>
/// <remarks>
/// <para>
/// The terminal pulls frames from this adapter, so pacing lives here: each read waits
/// out the frame interval and then returns the bytes for the next frame.
/// </para>
/// <para>
/// Screen mode is the workload's responsibility in Hex1b, so this adapter owns both
/// halves of the alternate-screen lifecycle, and likewise enables and disables mouse
/// reporting itself. The exit sequence is written from the quit path and again from
/// <see cref="DisposeAsync"/>, because a cancelled run must still leave the user on
/// the normal buffer with a visible cursor and mouse tracking switched off.
/// </para>
/// </remarks>
internal sealed class SixelCloudWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    // 1003 reports all motion, not just drags, which is what lets the pointer act as
    // an attractor without requiring a held button. 1006 selects SGR-encoded reports
    // so coordinates past column 223 survive.
    private const string EnterSequence = "\x1b[?1049h\x1b[?25l\x1b[?1003h\x1b[?1006h";
    private const string ExitSequence = "\x1b[?1006l\x1b[?1003l\x1b[?25h\x1b[?1049l";

    private readonly DustCloud _cloud;
    private readonly SixelCloudRenderer _renderer;
    private readonly TimeSpan _frameInterval;
    private readonly int _moteCount;
    private readonly int _cellPixelWidth;
    private readonly int _cellPixelHeight;
    private readonly int? _maxFrames;
    private readonly Lock _gate = new();
    private readonly StringBuilder _inputBuffer = new();

    private int _columns;
    private int _rows;
    private int _framesRendered;
    private bool _started;
    private bool _quitRequested;
    private bool _completed;
    private bool _exitWritten;

    private Action? _disconnected;

    public SixelCloudWorkloadAdapter(
        int moteCount,
        int cellPixelWidth,
        int cellPixelHeight,
        TimeSpan frameInterval,
        int? maxFrames,
        int seed)
    {
        _moteCount = moteCount;
        _cellPixelWidth = cellPixelWidth;
        _cellPixelHeight = cellPixelHeight;
        _frameInterval = frameInterval;
        _maxFrames = maxFrames;
        _cloud = new DustCloud(seed);
        _renderer = new SixelCloudRenderer(cellPixelWidth, cellPixelHeight);
    }

    public event Action? Disconnected
    {
        add
        {
            var replay = false;
            lock (_gate)
            {
                if (_completed)
                {
                    // A subscriber that attaches after the run finished would
                    // otherwise never learn the workload is gone.
                    replay = true;
                }
                else
                {
                    _disconnected += value;
                }
            }

            if (replay)
            {
                value?.Invoke();
            }
        }
        remove
        {
            lock (_gate)
            {
                _disconnected -= value;
            }
        }
    }

    public ValueTask ResizeAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _columns = width;
            _rows = height;
            _cloud.Reset(
                width * _cellPixelWidth,
                height * _cellPixelHeight,
                _moteCount);
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken cancellationToken = default)
    {
        if (ShouldStop())
        {
            return Finish();
        }

        if (!_started)
        {
            _started = true;
            return Encoding.ASCII.GetBytes(EnterSequence);
        }

        try
        {
            await Task.Delay(_frameInterval, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Finish();
        }

        if (ShouldStop())
        {
            return Finish();
        }

        int columns;
        int rows;
        lock (_gate)
        {
            columns = _columns;
            rows = _rows;
        }

        if (columns <= 0 || rows <= 0)
        {
            // The terminal has not reported its size yet, so there is nothing
            // meaningful to paint into.
            return ReadOnlyMemory<byte>.Empty;
        }

        _cloud.Advance(_frameInterval.TotalSeconds);
        _framesRendered++;

        return _renderer.RenderFrame(_cloud, columns, rows);
    }

    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _inputBuffer.Append(Encoding.ASCII.GetString(data.Span));
            ProcessInputLocked();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        SignalDisconnected();
        return ValueTask.CompletedTask;
    }

    /// <summary>The number of animation frames emitted so far.</summary>
    public int FramesRendered => _framesRendered;

    /// <summary>
    /// Consumes buffered input, extracting quit keys and SGR mouse reports.
    /// </summary>
    /// <remarks>
    /// Mouse reports arrive as <c>ESC [ &lt; button ; column ; row (M|m)</c>. Input can be
    /// split across reads, so an incomplete trailing report is left in the buffer for
    /// the next call rather than discarded.
    /// </remarks>
    private void ProcessInputLocked()
    {
        var text = _inputBuffer.ToString();
        var consumedThrough = 0;
        var index = 0;

        while (index < text.Length)
        {
            var current = text[index];

            if (current is 'q' or 'Q' or '\u0003')
            {
                _quitRequested = true;
                consumedThrough = index + 1;
                index++;
                continue;
            }

            if (current != '\u001b')
            {
                consumedThrough = index + 1;
                index++;
                continue;
            }

            // A lone Escape quits, but Escape also introduces mouse reports, so it
            // only counts as a quit once we know no sequence follows it.
            if (index + 2 < text.Length && text[index + 1] == '[' && text[index + 2] == '<')
            {
                var terminator = FindMouseTerminator(text, index + 3);
                if (terminator < 0)
                {
                    // Report is still arriving; keep it buffered.
                    break;
                }

                ApplyMouseReportLocked(text.AsSpan(index + 3, terminator - index - 3));
                consumedThrough = terminator + 1;
                index = terminator + 1;
                continue;
            }

            if (index + 1 >= text.Length)
            {
                // Could be a bare Escape or the start of a sequence. Waiting one more
                // read would stall quitting, so treat a trailing Escape as quit.
                _quitRequested = true;
                consumedThrough = index + 1;
                index++;
                continue;
            }

            // Some other escape sequence; skip the introducer and let the following
            // characters be consumed normally.
            consumedThrough = index + 1;
            index++;
        }

        _inputBuffer.Remove(0, consumedThrough);
    }

    private static int FindMouseTerminator(string text, int start)
    {
        for (var index = start; index < text.Length; index++)
        {
            if (text[index] is 'M' or 'm')
            {
                return index;
            }
        }

        return -1;
    }

    private void ApplyMouseReportLocked(ReadOnlySpan<char> body)
    {
        // body is "button;column;row".
        var firstSeparator = body.IndexOf(';');
        if (firstSeparator < 0)
        {
            return;
        }

        var remainder = body[(firstSeparator + 1)..];
        var secondSeparator = remainder.IndexOf(';');
        if (secondSeparator < 0)
        {
            return;
        }

        if (!int.TryParse(remainder[..secondSeparator], out var column)
            || !int.TryParse(remainder[(secondSeparator + 1)..], out var row))
        {
            return;
        }

        if (column <= 0 || row <= 0 || _columns <= 0 || _rows <= 0)
        {
            return;
        }

        // Mouse coordinates are 1-based cells; the simulation is 0-based pixels.
        // Aiming at the cell centre keeps the attractor from sitting on a cell edge.
        var pixelX = ((column - 1) + 0.5) * _cellPixelWidth;
        var pixelY = ((row - 1) + 0.5) * _cellPixelHeight;
        _cloud.SetPointer(pixelX, pixelY);
    }

    private bool ShouldStop()
    {
        lock (_gate)
        {
            return _quitRequested || (_maxFrames is { } limit && _framesRendered >= limit);
        }
    }

    private ReadOnlyMemory<byte> Finish()
    {
        bool writeExit;
        lock (_gate)
        {
            writeExit = !_exitWritten;
            _exitWritten = true;
        }

        if (!writeExit)
        {
            SignalDisconnected();
            return ReadOnlyMemory<byte>.Empty;
        }

        // Restore the normal buffer first and only report disconnection afterward, so
        // the terminal is guaranteed to have flushed the exit sequence before shutdown.
        return Encoding.ASCII.GetBytes(ExitSequence);
    }

    private void SignalDisconnected()
    {
        Action? handler;
        lock (_gate)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            handler = _disconnected;
            _disconnected = null;
        }

        handler?.Invoke();
    }
}
