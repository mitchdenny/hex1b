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
/// <para>
/// The exact bytes a frame is made of are the renderer's business, not this
/// adapter's. Both cloud demos share this class and differ only in the
/// <see cref="ICloudRenderer"/> they supply, so pacing, resize handling, cell-metric
/// discovery, mouse tracking, and the screen-mode lifecycle are written once.
/// </para>
/// </remarks>
internal sealed class CloudWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    // 1003 reports all motion, not just drags, which is what lets the pointer act as
    // an attractor without requiring a held button. 1006 selects SGR-encoded reports
    // so coordinates past column 223 survive.
    //
    // CSI 16 t asks for the cell size in pixels and CSI 14 t for the window's text
    // area in pixels. TerminalCapabilities.CellPixelWidth/Height are documented
    // defaults (10x20) that nothing probes -- discovering real metrics is deferred to
    // issue #455 -- so on a HiDPI terminal, where a cell is nearer 16x38 device
    // pixels, assuming the default paints motes far smaller than intended and, in the
    // Sixel demo's raster mode, a raster far smaller than the viewport. Both queries
    // are sent because terminals answer one or the other.
    private const string EnterSequence =
        "\x1b[?1049h\x1b[?25l\x1b[?1003h\x1b[?1006h\x1b[16t\x1b[14t";

    private const string ExitSequence = "\x1b[?1006l\x1b[?1003l\x1b[?25h\x1b[?1049l";

    private readonly DustCloud _cloud;
    private readonly TimeSpan _frameInterval;
    private readonly System.Diagnostics.Stopwatch _frameClock = System.Diagnostics.Stopwatch.StartNew();
    private TimeSpan _lastFrameStarted;

    // Upper bound on a single simulation step, so a stalled frame cannot teleport
    // motes across the field in one jump.
    private const double MaxFrameDeltaSeconds = 0.1;
    private readonly int _moteCount;
    private readonly int? _maxFrames;
    private readonly Lock _gate = new();
    private readonly StringBuilder _inputBuffer = new();

    // Renderers are built against fixed cell metrics, so a measured cell size means
    // building a new one rather than mutating the old.
    private readonly Func<double, double, ICloudRenderer> _rendererFactory;
    private ICloudRenderer _renderer;
    private double _cellPixelWidth;
    private double _cellPixelHeight;
    private bool _cellSizeMeasured;

    private int _columns;
    private int _rows;
    private int _framesRendered;
    private bool _started;
    private bool _quitRequested;
    private bool _completed;
    private bool _exitWritten;

    private Action? _disconnected;

    public CloudWorkloadAdapter(
        int moteCount,
        int cellPixelWidth,
        int cellPixelHeight,
        TimeSpan frameInterval,
        int? maxFrames,
        int seed,
        Func<double, double, ICloudRenderer> rendererFactory)
    {
        _moteCount = moteCount;
        _cellPixelWidth = cellPixelWidth;
        _cellPixelHeight = cellPixelHeight;
        _frameInterval = frameInterval;
        _maxFrames = maxFrames;
        _rendererFactory = rendererFactory;
        _cloud = new DustCloud(seed);
        _renderer = rendererFactory(cellPixelWidth, cellPixelHeight);
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
            RebuildFieldLocked();
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Rebuilds the simulation field from the current viewport and cell metrics.
    /// </summary>
    private void RebuildFieldLocked()
    {
        if (_columns <= 0 || _rows <= 0)
        {
            return;
        }

        _cloud.Reset(
            (int)Math.Round(_columns * _cellPixelWidth),
            (int)Math.Round(_rows * _cellPixelHeight),
            _moteCount);
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

        // Sleep only for the time left in the budget, not a full interval. Delaying
        // unconditionally would make the real frame period "interval + render time",
        // which both lowers the frame rate and makes it wobble with scene cost.
        var elapsed = _frameClock.Elapsed - _lastFrameStarted;
        var remaining = _frameInterval - elapsed;
        if (remaining > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Finish();
            }
        }

        if (ShouldStop())
        {
            return Finish();
        }

        // Resize and cell-metric replies reset the mote list. Keep the dimensions,
        // simulation step, and renderer together under the same lock so a reset
        // cannot invalidate an enumeration or mix geometry from different frames.
        lock (_gate)
        {
            var columns = _columns;
            var rows = _rows;

            if (columns <= 0 || rows <= 0)
            {
                // The terminal has not reported its size yet, so there is nothing
                // meaningful to paint into.
                return ReadOnlyMemory<byte>.Empty;
            }

            // Advance by the time that actually passed rather than the nominal interval,
            // so a late frame still lands the motes where they belong. A dropped frame
            // then shows as a longer step instead of the whole cloud lurching.
            var now = _frameClock.Elapsed;
            var delta = now - _lastFrameStarted;
            _lastFrameStarted = now;

            // Clamp so a stall (or a debugger pause) cannot teleport the simulation.
            var deltaSeconds = Math.Clamp(delta.TotalSeconds, 0.001, MaxFrameDeltaSeconds);

            _cloud.Advance(deltaSeconds);
            _framesRendered++;

            return _renderer.RenderFrame(_cloud, columns, rows);
        }
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

            // XTWINOPS geometry replies, answering the CSI 16 t / CSI 14 t queries
            // sent on entry.
            if (index + 2 < text.Length && text[index + 1] == '[' && char.IsAsciiDigit(text[index + 2]))
            {
                var terminator = FindWindowOpTerminator(text, index + 2);
                if (terminator == -2)
                {
                    // Still arriving; keep it buffered.
                    break;
                }

                if (terminator >= 0)
                {
                    ApplyWindowOpReportLocked(text.AsSpan(index + 2, terminator - index - 2));
                    consumedThrough = terminator + 1;
                    index = terminator + 1;
                    continue;
                }
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

    /// <summary>
    /// Finds the terminator of an XTWINOPS reply body of digits and semicolons.
    /// </summary>
    /// <returns>
    /// The index of the terminating <c>t</c>, <c>-2</c> if the sequence is still
    /// arriving, or <c>-1</c> if this is some other escape sequence.
    /// </returns>
    private static int FindWindowOpTerminator(string text, int start)
    {
        for (var index = start; index < text.Length; index++)
        {
            var current = text[index];
            if (current == 't')
            {
                return index;
            }

            if (!char.IsAsciiDigit(current) && current != ';')
            {
                // Not a geometry reply; let the normal path consume it.
                return -1;
            }
        }

        return -2;
    }

    /// <summary>
    /// Applies an XTWINOPS geometry reply, updating the cell metrics motes are sized
    /// and positioned against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <c>CSI 16 t</c> reply is <c>6;height;width</c> and reports the cell size
    /// directly. A <c>CSI 14 t</c> reply is <c>4;height;width</c> and reports the text
    /// area, which is divided by the character grid to derive the cell size.
    /// </para>
    /// <para>
    /// The direct report wins: once a cell size has been measured, a later derived
    /// value is ignored, because the text area can include padding the character grid
    /// does not cover.
    /// </para>
    /// </remarks>
    private void ApplyWindowOpReportLocked(ReadOnlySpan<char> body)
    {
        var firstSeparator = body.IndexOf(';');
        if (firstSeparator < 0)
        {
            return;
        }

        if (!int.TryParse(body[..firstSeparator], out var kind))
        {
            return;
        }

        var remainder = body[(firstSeparator + 1)..];
        var secondSeparator = remainder.IndexOf(';');
        if (secondSeparator < 0)
        {
            return;
        }

        if (!int.TryParse(remainder[..secondSeparator], out var height)
            || !int.TryParse(remainder[(secondSeparator + 1)..], out var width)
            || width <= 0
            || height <= 0)
        {
            return;
        }

        double cellWidth;
        double cellHeight;

        switch (kind)
        {
            case 6:
                cellWidth = width;
                cellHeight = height;
                break;

            case 4:
                if (_cellSizeMeasured || _columns <= 0 || _rows <= 0)
                {
                    return;
                }

                cellWidth = (double)width / _columns;
                cellHeight = (double)height / _rows;
                break;

            default:
                return;
        }

        if (!IsPlausibleCellSize(cellWidth, cellHeight))
        {
            return;
        }

        _cellSizeMeasured = kind == 6;
        _cellPixelWidth = cellWidth;
        _cellPixelHeight = cellHeight;
        _renderer = _rendererFactory(cellWidth, cellHeight);
        RebuildFieldLocked();
    }

    /// <summary>
    /// Rejects implausible cell metrics so a malformed reply cannot produce a
    /// simulation field of absurd size.
    /// </summary>
    private static bool IsPlausibleCellSize(double width, double height) =>
        double.IsFinite(width)
        && double.IsFinite(height)
        && width is >= 2 and <= 100
        && height is >= 2 and <= 200;

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
