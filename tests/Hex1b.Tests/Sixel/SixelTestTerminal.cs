using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Hex1b.Automation;
using Hex1b.Diagnostics;
using Hex1b.Reflow;
using Hex1b.Sixel;
using Hex1b.Surfaces;
using Hex1b.Tokens;

namespace Hex1b.Tests.Sixel;

internal sealed class SixelTestTerminal : IAsyncDisposable
{
    private readonly QueuedByteWorkloadAdapter _workload = new();
    private readonly ExactBytePresentationAdapter _presentation;
    private readonly CancellationTokenSource _runCancellation = new();
    private readonly Task<int> _runTask;

    private SixelTestTerminal(
        int width,
        int height,
        int cellPixelWidth,
        int cellPixelHeight,
        double actualCellPixelWidth,
        int scrollbackCapacity,
        ITerminalReflowProvider? reflow,
        Hex1bMetrics? metrics,
        IHex1bTerminalWorkloadFilter? workloadFilter,
        IHex1bTerminalPresentationFilter? presentationFilter,
        bool impactAware,
        SixelCellMetrics? cellMetrics)
    {
        var capabilities = new TerminalCapabilities
        {
            SupportsSixel = true,
            SupportsTrueColor = true,
            Supports256Colors = true,
            CellPixelWidth = cellPixelWidth,
            CellPixelHeight = cellPixelHeight,
            ActualCellPixelWidth = actualCellPixelWidth,
        };

        _presentation = impactAware
            ? new ImpactAwarePresentationAdapter(width, height, capabilities, reflow)
            : new ExactBytePresentationAdapter(width, height, capabilities, reflow);
        var options = new Hex1bTerminalOptions
        {
            WorkloadAdapter = _workload,
            PresentationAdapter = _presentation,
            Width = width,
            Height = height,
            ScrollbackCapacity = scrollbackCapacity > 0 ? scrollbackCapacity : null,
            Metrics = metrics,
        };
        if (workloadFilter is not null)
        {
            options.WorkloadFilters.Add(workloadFilter);
        }
        if (presentationFilter is not null)
        {
            options.PresentationFilters.Add(presentationFilter);
        }

        Terminal = new Hex1bTerminal(options);

        // Real presentation probing is owned by #455; tests inject protocol cell
        // metrics directly through the same seam an adapter would use.
        if (cellMetrics is { } injected)
        {
            Terminal.SetSixelCellMetrics(injected);
        }

        _runTask = Terminal.RunAsync(_runCancellation.Token);
    }

    public Hex1bTerminal Terminal { get; }

    public byte[] PresentationBytes => _presentation.CapturedBytes;

    public IReadOnlyList<AppliedToken> AppliedTokens => _presentation is ImpactAwarePresentationAdapter adapter
        ? adapter.AppliedTokens
        : [];

    public static SixelTestTerminal Create(
        int width = 20,
        int height = 10,
        int cellPixelWidth = 1,
        int cellPixelHeight = 6,
        double actualCellPixelWidth = 0,
        int scrollbackCapacity = 0,
        ITerminalReflowProvider? reflow = null,
        Hex1bMetrics? metrics = null,
        IHex1bTerminalWorkloadFilter? workloadFilter = null,
        IHex1bTerminalPresentationFilter? presentationFilter = null,
        bool impactAware = false,
        SixelCellMetrics? cellMetrics = null)
        => new(
            width,
            height,
            cellPixelWidth,
            cellPixelHeight,
            actualCellPixelWidth,
            scrollbackCapacity,
            reflow,
            metrics,
            workloadFilter,
            presentationFilter,
            impactAware,
            cellMetrics);

    public async Task FeedAsync(
        ReadOnlyMemory<byte> bytes,
        IReadOnlyList<int>? chunkSizes = null,
        CancellationToken cancellationToken = default)
    {
        var expectedPresentationLength = _presentation.CapturedLength + bytes.Length;
        var chunks = Split(bytes, chunkSizes);
        foreach (var chunk in chunks)
            await _workload.EnqueueAsync(new WorkloadOutputItem(chunk.ToArray(), Tokens: null), cancellationToken);

        await _presentation.WaitForLengthAsync(expectedPresentationLength, cancellationToken);
    }

    public async Task FeedChunkAsync(
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default)
    {
        var expectedPresentationLength = _presentation.CapturedLength + bytes.Length;
        await _workload.EnqueueAsync(
            new WorkloadOutputItem(bytes.ToArray(), Tokens: null),
            cancellationToken);
        await _presentation.WaitForLengthAsync(expectedPresentationLength, cancellationToken);
    }

    public async Task FeedPreTokenizedAsync(
        ReadOnlyMemory<byte> bytes,
        IReadOnlyList<AnsiToken> tokens,
        CancellationToken cancellationToken = default)
    {
        var expectedPresentationLength = _presentation.CapturedLength + bytes.Length;
        await _workload.EnqueueAsync(
            new WorkloadOutputItem(bytes.ToArray(), tokens),
            cancellationToken);
        await _presentation.WaitForLengthAsync(expectedPresentationLength, cancellationToken);
    }

    public async Task CompleteWorkloadAsync(CancellationToken cancellationToken = default)
    {
        _workload.Complete();
        await _runTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
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

        throw new TimeoutException($"Timed out waiting for Sixel test terminal state: {expectation}.");
    }

    public SixelTerminalObservation Observe(
        bool includeScrollback = true,
        ScrollbackWidth scrollbackWidth = ScrollbackWidth.CurrentTerminal)
    {
        using var snapshot = Terminal.CreateSnapshot(
            scrollbackLines: includeScrollback ? Terminal.ScrollbackCount : 0,
            scrollbackWidth: scrollbackWidth);
        var placements = new List<SixelPlacementObservation>();
        var occupiedRows = new HashSet<SixelOccupiedRow>();
        var occupiedCells = new HashSet<SixelOccupiedCell>();

        // Sixel placements are now independent state, not per-cell ownership,
        // so occupancy/placement observation walks the snapshot's placements
        // directly rather than scanning cells for a Sixel flag/reference.
        foreach (var placement in snapshot.SixelPlacements)
        {
            if (!placement.HasPaintedExtent)
                continue;

            for (var y = placement.PaintedTop; y <= placement.PaintedBottom; y++)
            {
                if (y < 0 || y >= snapshot.Height)
                    continue;

                var inScrollback = y < snapshot.ScrollbackLineCount;
                occupiedRows.Add(new SixelOccupiedRow(y, inScrollback));

                for (var x = placement.PaintedLeft; x <= placement.PaintedRight; x++)
                {
                    if (x < 0 || x >= snapshot.Width)
                        continue;
                    if (!placement.CoversCell(y, x))
                        continue;

                    occupiedCells.Add(new SixelOccupiedCell(x, y, inScrollback));
                }
            }

            var sixel = placement.Image;
            var pixels = placement.GetVisiblePixels();
            placements.Add(new SixelPlacementObservation(
                placement.PaintedLeft,
                placement.PaintedTop,
                sixel.WidthInCells,
                sixel.HeightInCells,
                sixel.Payload,
                pixels?.Width ?? 0,
                pixels?.Height ?? 0,
                pixels is null ? "" : SixelPixelGrid.Format(pixels),
                pixels,
                sixel.CellMetrics));
        }

        return new SixelTerminalObservation(
            snapshot.Width,
            snapshot.Height,
            snapshot.CursorX,
            snapshot.CursorY,
            snapshot.InAlternateScreen,
            snapshot.ScrollbackLineCount,
            snapshot.CellPixelWidth,
            snapshot.CellPixelHeight,
            Terminal.Capabilities.EffectiveCellPixelWidth,
            [.. placements],
            [.. occupiedRows.OrderBy(row => row.Row)],
            [.. occupiedCells.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column)],
            Enumerable.Range(0, snapshot.Height).Select(snapshot.GetLine).ToArray());
    }

    public string CreateSvgEvidence()
    {
        using var snapshot = Terminal.CreateSnapshot(scrollbackLines: Terminal.ScrollbackCount);
        foreach (var placement in snapshot.SixelPlacements)
        {
            var pixels = placement.GetVisiblePixels();
            if (pixels is not null)
                return SixelPixelGrid.ToSvg(pixels);
        }

        throw new InvalidOperationException("No decoded Sixel raster is available for SVG evidence.");
    }

    public static async Task<IReadOnlyList<SixelSplitRun>> ObserveEverySplitAsync(
        SixelFixture fixture,
        CancellationToken cancellationToken = default,
        bool useC1Framing = false)
    {
        var bytes = useC1Framing ? fixture.C1Bytes : fixture.StandardBytes;
        var runs = new List<SixelSplitRun>(bytes.Length);
        for (var split = 0; split < bytes.Length; split++)
        {
            await using var terminal = Create();
            var chunkSizes = split == 0
                ? new[] { bytes.Length }
                : new[] { split, bytes.Length - split };
            await terminal.FeedAsync(bytes, chunkSizes, cancellationToken);
            await terminal.WaitForAsync(
                snapshot => snapshot.ContainsSixelData(),
                $"fixture '{fixture.Name}' at split {split}",
                cancellationToken);
            runs.Add(new SixelSplitRun(split, terminal.PresentationBytes, terminal.Observe()));
        }

        return runs;
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
        private readonly Channel<WorkloadOutputItem> _output =
            Channel.CreateUnbounded<WorkloadOutputItem>();
        private readonly object _eventLock = new();
        private Action? _disconnected;
        private bool _completed;

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
                {
                    return item;
                }
            }

            Action? disconnected;
            lock (_eventLock)
            {
                _completed = true;
                disconnected = _disconnected;
            }
            disconnected?.Invoke();
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

    private class ExactBytePresentationAdapter(
        int width,
        int height,
        TerminalCapabilities capabilities,
        ITerminalReflowProvider? reflow) :
        IHex1bTerminalPresentationAdapter,
        ITerminalReflowProvider,
        IInternalTerminalReflowProvider
    {
        private readonly List<byte> _bytes = [];
        private TaskCompletionSource _changed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Width { get; } = width;
        public int Height { get; } = height;
        public TerminalCapabilities Capabilities { get; } = capabilities;
        public bool ReflowEnabled => reflow is not null;
        public bool ShouldClearSoftWrapOnAbsolutePosition =>
            reflow?.ShouldClearSoftWrapOnAbsolutePosition ?? false;

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

        public event Action<int, int>? Resized
        {
            add { }
            remove { }
        }

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            TaskCompletionSource changed;
            lock (_bytes)
            {
                _bytes.AddRange(data.Span);
                changed = _changed;
                _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            changed.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public async Task WaitForLengthAsync(int minimumLength, CancellationToken cancellationToken)
        {
            while (true)
            {
                Task changed;
                lock (_bytes)
                {
                    if (_bytes.Count >= minimumLength)
                        return;
                    changed = _changed.Task;
                }
                await changed.WaitAsync(cancellationToken);
            }
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
        public ReflowResult Reflow(ReflowContext context) =>
            (reflow ?? throw new InvalidOperationException("Reflow is not enabled.")).Reflow(context);

        bool IInternalTerminalReflowProvider.TryReflowWithAnchors(
            ReflowContext context,
            IReadOnlyList<TerminalReflowAnchor> anchors,
            out InternalReflowResult result)
        {
            if (reflow is null)
                throw new InvalidOperationException("Reflow is not enabled.");

            return InternalTerminalReflow.TryReflow(reflow, context, anchors, out result);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ImpactAwarePresentationAdapter(
        int width,
        int height,
        TerminalCapabilities capabilities,
        ITerminalReflowProvider? reflow) :
        ExactBytePresentationAdapter(width, height, capabilities, reflow),
        ICellImpactAwarePresentationAdapter
    {
        private readonly List<AppliedToken> _appliedTokens = [];

        public IReadOnlyList<AppliedToken> AppliedTokens => _appliedTokens;

        public ValueTask WriteOutputWithImpactsAsync(
            IReadOnlyList<AppliedToken> appliedTokens,
            CancellationToken ct = default)
        {
            _appliedTokens.AddRange(appliedTokens);
            var bytes = AnsiTokenUtf8Serializer.Serialize(
                appliedTokens.Select(applied => applied.Token));
            return WriteOutputAsync(bytes, ct);
        }
    }
}

/// <summary>
/// Test-only convenience for querying which Sixel image (if any) paints a
/// specific cell in a snapshot. Sixel graphics are independent placement
/// state now (stage #451), not per-cell ownership, so this is a small
/// "topmost placement covering this cell" search rather than a direct cell
/// property lookup.
/// </summary>
internal static class SixelSnapshotExtensions
{
    public static SixelData? GetSixelDataAt(this Hex1bTerminalSnapshot snapshot, int x, int y)
    {
        SixelPlacement? topmost = null;
        foreach (var placement in snapshot.SixelPlacements)
        {
            if (!placement.CoversCell(y, x))
                continue;
            if (topmost is null || placement.Sequence > topmost.Sequence)
                topmost = placement;
        }

        return topmost?.Image;
    }
}

internal sealed record SixelSplitRun(
    int SplitBoundary,
    byte[] PresentationBytes,
    SixelTerminalObservation Observation);

internal sealed record SixelTerminalObservation(
    int Width,
    int Height,
    int CursorX,
    int CursorY,
    bool InAlternateScreen,
    int ScrollbackLines,
    int CellPixelWidth,
    int CellPixelHeight,
    double EffectiveCellPixelWidth,
    IReadOnlyList<SixelPlacementObservation> Placements,
    IReadOnlyList<SixelOccupiedRow> OccupiedRows,
    IReadOnlyList<SixelOccupiedCell> OccupiedCells,
    IReadOnlyList<string> Lines)
{
    public string ModelFingerprint()
    {
        var builder = new StringBuilder();
        builder.Append($"{Width}x{Height}|cursor={CursorX},{CursorY}|alt={InAlternateScreen}");
        builder.Append($"|history={ScrollbackLines}|cell={CellPixelWidth}x{CellPixelHeight}");
        foreach (var placement in Placements)
        {
            builder.Append(
                $"|sixel={placement.OriginColumn},{placement.OriginRow}," +
                $"{placement.WidthInCells}x{placement.HeightInCells}," +
                $"{placement.PixelWidth}x{placement.PixelHeight}," +
                $"{placement.Payload},{placement.PixelGrid}");
        }
        foreach (var row in OccupiedRows)
            builder.Append($"|occupied-row={row.Row},history={row.InScrollback}");
        foreach (var cell in OccupiedCells)
            builder.Append(
                $"|occupied-cell={cell.Column},{cell.Row},history={cell.InScrollback}");
        foreach (var line in Lines)
            builder.Append('|').Append(line);
        return builder.ToString();
    }

    public string CompositePixelGrid()
    {
        if (Placements.Count == 0)
            return "";

        var width = Placements.Max(
            placement => placement.OriginColumn * CellPixelWidth + placement.PixelWidth);
        var height = Placements.Max(
            placement => placement.OriginRow * CellPixelHeight + placement.PixelHeight);
        var composite = new SixelPixelBuffer(width, height);

        foreach (var placement in Placements)
        {
            if (placement.Pixels is null)
                continue;

            var originX = placement.OriginColumn * CellPixelWidth;
            var originY = placement.OriginRow * CellPixelHeight;
            for (var y = 0; y < placement.Pixels.Height; y++)
            {
                for (var x = 0; x < placement.Pixels.Width; x++)
                {
                    var pixel = placement.Pixels[x, y];
                    if (!pixel.IsTransparent)
                        composite[originX + x, originY + y] = pixel;
                }
            }
        }

        return SixelPixelGrid.Format(composite);
    }
}

internal sealed record SixelPlacementObservation(
    int OriginColumn,
    int OriginRow,
    int WidthInCells,
    int HeightInCells,
    string Payload,
    int PixelWidth,
    int PixelHeight,
    string PixelGrid,
    SixelPixelBuffer? Pixels,
    SixelCellMetrics CellMetrics);

internal sealed record SixelOccupiedRow(int Row, bool InScrollback);

internal sealed record SixelOccupiedCell(int Column, int Row, bool InScrollback);

internal static class SixelPixelGrid
{
    public static string Format(SixelPixelBuffer pixels)
    {
        var symbols = new Dictionary<Rgba32, char>();
        var nextSymbol = 'A';
        var rows = new string[pixels.Height];
        for (var y = 0; y < pixels.Height; y++)
        {
            var row = new char[pixels.Width];
            for (var x = 0; x < pixels.Width; x++)
            {
                var pixel = pixels[x, y];
                if (pixel.IsTransparent)
                {
                    row[x] = '.';
                    continue;
                }

                if (!symbols.TryGetValue(pixel, out var symbol))
                {
                    symbol = nextSymbol++;
                    symbols.Add(pixel, symbol);
                }
                row[x] = symbol;
            }
            rows[y] = new string(row);
        }

        var legend = string.Join(
            ", ",
            symbols.Select(pair =>
                $"{pair.Value}=#{pair.Key.R:X2}{pair.Key.G:X2}{pair.Key.B:X2}{pair.Key.A:X2}"));
        return $"{string.Join('\n', rows)}\n[{legend}]";
    }

    public static string ToSvg(SixelPixelBuffer pixels)
    {
        const int scale = 12;
        var builder = new StringBuilder();
        builder.AppendLine(
            $"""<svg xmlns="http://www.w3.org/2000/svg" width="{pixels.Width * scale}" height="{pixels.Height * scale}" viewBox="0 0 {pixels.Width} {pixels.Height}" shape-rendering="crispEdges">""");
        builder.AppendLine("""  <rect width="100%" height="100%" fill="#181818"/>""");
        builder.AppendLine("""  <g id="sixel-pixels">""");
        for (var y = 0; y < pixels.Height; y++)
        {
            for (var x = 0; x < pixels.Width; x++)
            {
                var pixel = pixels[x, y];
                if (pixel.IsTransparent)
                    continue;

                var opacity = (pixel.A / 255.0).ToString("F3", CultureInfo.InvariantCulture);
                builder.AppendLine(
                    $"""    <rect x="{x}" y="{y}" width="1" height="1" fill="#{pixel.R:X2}{pixel.G:X2}{pixel.B:X2}" fill-opacity="{opacity}"/>""");
            }
        }
        builder.AppendLine("  </g>");
        builder.AppendLine("""  <g id="pixel-grid" fill="none" stroke="#ffffff" stroke-opacity="0.35" stroke-width="0.04">""");
        for (var x = 0; x <= pixels.Width; x++)
            builder.AppendLine($"""    <line x1="{x}" y1="0" x2="{x}" y2="{pixels.Height}"/>""");
        for (var y = 0; y <= pixels.Height; y++)
            builder.AppendLine($"""    <line x1="0" y1="{y}" x2="{pixels.Width}" y2="{y}"/>""");
        builder.AppendLine("  </g>");
        builder.AppendLine("</svg>");
        return builder.ToString();
    }
}
