using System.Threading.Channels;
using Hex1b.Automation;
using Hex1b.Sixel;
using Hex1b.Tokens;

namespace Hex1b.Tests.Sixel;

/// <summary>
/// A minimal <see cref="Hex1bTerminal"/> harness for stage #458's routing/translation/
/// sanitization/diagnostics coverage: unlike <see cref="SixelTestTerminal"/> (which
/// always declares native Sixel support), this harness lets each test configure the
/// exact <see cref="Hex1b.Sixel.SixelPresentationSupport"/> value, KGP capability,
/// managed-sink participation, sanitization, and unsupported-presentation policy that
/// stage #458's route computation depends on.
/// </summary>
internal sealed class SixelRoutingTestTerminal : IAsyncDisposable
{
    private readonly QueuedByteWorkloadAdapter _workload = new();
    private readonly RoutingPresentationAdapter _presentation;
    private readonly CancellationTokenSource _runCancellation = new();
    private readonly Task<int> _runTask;
    private readonly List<SixelRasterRouteDiagnostic> _diagnostics = [];

    private SixelRoutingTestTerminal(
        int width,
        int height,
        SixelPresentationSupport sixelSupport,
        bool supportsKgp,
        bool asManagedSink,
        SixelSanitizationPolicy? sanitization,
        SixelUnsupportedPresentationPolicy unsupportedPresentation,
        int cellPixelWidth,
        int cellPixelHeight)
    {
        var capabilities = new TerminalCapabilities
        {
            SixelSupport = sixelSupport,
            SupportsSixel = sixelSupport == SixelPresentationSupport.Native,
            SupportsKgp = supportsKgp,
            SupportsTrueColor = true,
            Supports256Colors = true,
            CellPixelWidth = cellPixelWidth,
            CellPixelHeight = cellPixelHeight,
        };

        _presentation = asManagedSink
            ? new RoutingManagedSinkPresentationAdapter(width, height, capabilities)
            : new RoutingPresentationAdapter(width, height, capabilities);

        var options = new Hex1bTerminalOptions
        {
            WorkloadAdapter = _workload,
            PresentationAdapter = _presentation,
            Width = width,
            Height = height,
            SixelSanitization = sanitization ?? SixelSanitizationPolicy.Disabled,
            SixelUnsupportedPresentation = unsupportedPresentation,
        };

        Terminal = new Hex1bTerminal(options);
        Terminal.SixelRouteDiagnosticRaised += diagnostic =>
        {
            lock (_diagnostics)
                _diagnostics.Add(diagnostic);
        };

        _runTask = Terminal.RunAsync(_runCancellation.Token);
    }

    public Hex1bTerminal Terminal { get; }

    public byte[] PresentationBytes => _presentation.CapturedBytes;

    public int PresentationLength => _presentation.CapturedLength;

    public string PresentationText => System.Text.Encoding.Latin1.GetString(PresentationBytes);

    public IReadOnlyList<SixelRasterEvent> RasterEvents =>
        _presentation is RoutingManagedSinkPresentationAdapter sink ? sink.Events : [];

    public IReadOnlyList<SixelRasterRouteDiagnostic> Diagnostics
    {
        get
        {
            lock (_diagnostics)
                return [.. _diagnostics];
        }
    }

    public static SixelRoutingTestTerminal Create(
        SixelPresentationSupport sixelSupport = SixelPresentationSupport.Native,
        bool supportsKgp = false,
        bool asManagedSink = false,
        SixelSanitizationPolicy? sanitization = null,
        SixelUnsupportedPresentationPolicy unsupportedPresentation = SixelUnsupportedPresentationPolicy.Suppress,
        int width = 20,
        int height = 10,
        int cellPixelWidth = 1,
        int cellPixelHeight = 6)
        => new(
            width,
            height,
            sixelSupport,
            supportsKgp,
            asManagedSink,
            sanitization,
            unsupportedPresentation,
            cellPixelWidth,
            cellPixelHeight);

    public async Task FeedAsync(
        ReadOnlyMemory<byte> bytes,
        IReadOnlyList<int>? chunkSizes = null,
        CancellationToken cancellationToken = default)
    {
        var chunks = Split(bytes, chunkSizes);
        foreach (var chunk in chunks)
            await _workload.EnqueueAsync(new WorkloadOutputItem(chunk.ToArray(), Tokens: null), cancellationToken);

        // There is no single presentation-side signal that always advances (a
        // Headless/Unsupported+Suppress route may never write a byte, and a managed
        // sink may never receive raw bytes at all), so callers observe completion via
        // WaitForAsync/WaitForEventsAsync against terminal snapshot state or recorded
        // events instead. A short settle delay here keeps single-shot feeds simple for
        // tests that don't need a specific condition.
        await Task.Delay(50, cancellationToken);
    }

    public async Task FeedPreTokenizedAsync(
        ReadOnlyMemory<byte> bytes,
        IReadOnlyList<AnsiToken> tokens,
        CancellationToken cancellationToken = default)
    {
        await _workload.EnqueueAsync(new WorkloadOutputItem(bytes.ToArray(), tokens), cancellationToken);
        await Task.Delay(50, cancellationToken);
    }

    /// <summary>
    /// Mutates the presentation's declared <see cref="Hex1b.Sixel.SixelPresentationSupport"/>
    /// mid-session (all other capability fields unchanged), simulating a
    /// post-discovery capability update on the same live connection. Since
    /// <see cref="Hex1bTerminal.Capabilities"/> reads the presentation's
    /// <c>Capabilities</c> property live rather than caching it, the effective
    /// Sixel route is recomputed from this new value starting with the very next
    /// processed batch.
    /// </summary>
    public void SetSixelSupport(SixelPresentationSupport sixelSupport)
    {
        _presentation.Capabilities = _presentation.Capabilities with
        {
            SixelSupport = sixelSupport,
            SupportsSixel = sixelSupport == SixelPresentationSupport.Native,
        };
    }

    public async Task WaitForAsync(
        Func<Hex1bTerminalSnapshot, bool> condition,
        string expectation,
        CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromSeconds(2);
        var started = TimeProvider.System.GetTimestamp();
        while (TimeProvider.System.GetElapsedTime(started) < timeout)
        {
            using var snapshot = Terminal.CreateSnapshot(scrollbackLines: Terminal.ScrollbackCount);
            if (condition(snapshot))
                return;

            await Task.Delay(10, cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for Sixel routing test terminal state: {expectation}.");
    }

    public async Task WaitForEventCountAsync(int minimumCount, CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromSeconds(2);
        var started = TimeProvider.System.GetTimestamp();
        while (TimeProvider.System.GetElapsedTime(started) < timeout)
        {
            if (RasterEvents.Count >= minimumCount)
                return;

            await Task.Delay(10, cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for at least {minimumCount} raster events.");
    }

    public async Task WaitForPresentationLengthAsync(int minimumLength, CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromSeconds(2);
        var started = TimeProvider.System.GetTimestamp();
        while (TimeProvider.System.GetElapsedTime(started) < timeout)
        {
            if (PresentationLength >= minimumLength)
                return;

            await Task.Delay(10, cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for at least {minimumLength} presentation bytes.");
    }

    public async ValueTask DisposeAsync()
    {
        _workload.Complete();
        try
        {
            await _runTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
        }
        catch (TimeoutException)
        {
            _runCancellation.Cancel();
            try
            {
                await _runTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        await Terminal.DisposeAsync();
        _runCancellation.Dispose();
    }

    private static IReadOnlyList<ReadOnlyMemory<byte>> Split(
        ReadOnlyMemory<byte> bytes,
        IReadOnlyList<int>? chunkSizes)
    {
        if (chunkSizes is null || chunkSizes.Count == 0)
            return [bytes];

        var result = new List<ReadOnlyMemory<byte>>(chunkSizes.Count);
        var offset = 0;
        foreach (var size in chunkSizes)
        {
            if (size <= 0 || offset + size > bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(chunkSizes));
            result.Add(bytes.Slice(offset, size));
            offset += size;
        }

        if (offset != bytes.Length)
            throw new ArgumentException("Chunk sizes must consume the complete byte sequence.", nameof(chunkSizes));
        return result;
    }

    private sealed class QueuedByteWorkloadAdapter : IHex1bTerminalTokenWorkloadAdapter
    {
        private readonly Channel<WorkloadOutputItem> _output = Channel.CreateUnbounded<WorkloadOutputItem>();

        public event Action? Disconnected { add { } remove { } }

        public ValueTask EnqueueAsync(WorkloadOutputItem item, CancellationToken cancellationToken)
            => _output.Writer.WriteAsync(item, cancellationToken);

        public void Complete() => _output.Writer.TryComplete();

        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => (await ReadOutputItemAsync(ct)).Bytes;

        public async ValueTask<WorkloadOutputItem> ReadOutputItemAsync(CancellationToken ct = default)
        {
            while (await _output.Reader.WaitToReadAsync(ct))
            {
                if (_output.Reader.TryRead(out var item))
                    return item;
            }

            return default;
        }

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Complete();
            return ValueTask.CompletedTask;
        }
    }

    private class RoutingPresentationAdapter(int width, int height, TerminalCapabilities capabilities) :
        IHex1bTerminalPresentationAdapter
    {
        private readonly List<byte> _bytes = [];

        public int Width { get; } = width;
        public int Height { get; } = height;

        // Mutable (rather than the init-only field this class started with) so tests
        // can simulate a mid-session capability change — e.g. a post-discovery
        // update — and observe the resulting Sixel route change on the very next
        // batch, since Hex1bTerminal.Capabilities reads this property live rather
        // than caching it at construction.
        public TerminalCapabilities Capabilities { get; set; } = capabilities;

        public byte[] CapturedBytes
        {
            get
            {
                lock (_bytes)
                    return [.. _bytes];
            }
        }

        public int CapturedLength
        {
            get
            {
                lock (_bytes)
                    return _bytes.Count;
            }
        }

        public event Action<int, int>? Resized { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            lock (_bytes)
                _bytes.AddRange(data.Span);
            return ValueTask.CompletedTask;
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            catch (OperationCanceledException)
            {
            }
            return ReadOnlyMemory<byte>.Empty;
        }

        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RoutingManagedSinkPresentationAdapter(int width, int height, TerminalCapabilities capabilities) :
        RoutingPresentationAdapter(width, height, capabilities),
        ISixelRasterPresentationSink
    {
        private readonly List<SixelRasterEvent> _events = [];

        public IReadOnlyList<SixelRasterEvent> Events
        {
            get
            {
                lock (_events)
                    return [.. _events];
            }
        }

        public ValueTask OnSixelRasterEventsAsync(IReadOnlyList<SixelRasterEvent> events, CancellationToken ct = default)
        {
            lock (_events)
                _events.AddRange(events);
            return ValueTask.CompletedTask;
        }
    }
}
