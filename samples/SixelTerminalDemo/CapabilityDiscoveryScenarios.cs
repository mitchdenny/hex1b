using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Hex1b;
using Hex1b.Sixel;

/// <summary>
/// Deterministic, no-real-terminal-required scenarios demonstrating stage #455's
/// Sixel capability and protocol-cell-metrics discovery contract: direct
/// declaration pre-empting the probe, fragmented/interleaved wire replies with
/// preserved keyboard input, precedence/disagreement between sources, fractional
/// derivation from window/grid geometry, resize invalidation, existing-placement
/// immutability across a later metrics change, and query-ownership/support
/// advertisement across native and headless presentations.
/// </summary>
/// <remarks>
/// Every scenario here uses <see cref="FakeConsoleDriver"/> (an internal
/// <c>IConsoleDriver</c> the demo can implement thanks to
/// <c>InternalsVisibleTo</c>) or <see cref="HeadlessPresentationAdapter"/>, never
/// a real terminal — the same approach
/// <c>tests/Hex1b.Tests/Sixel/SixelCapabilityDiscoveryTests.cs</c> and
/// <c>Hex1bTerminalQueryOwnershipTests.cs</c> use, so this file's observations are
/// direct, reproducible evidence of the same behavior those tests assert.
/// </remarks>
internal static class CapabilityDiscoveryScenarios
{
    private static readonly TimeSpan DemoProbeTimeout = TimeSpan.FromMilliseconds(60);

    /// <summary>
    /// Runs every scenario and returns one (title, observation) pair per scenario,
    /// in the order the issue's required demo behaviors are listed.
    /// </summary>
    public static async Task<IReadOnlyList<(string Title, string Observation)>> RunAllAsync()
    {
        return
        [
            ("Direct declaration skips probing", await DirectDeclarationSkipsProbingAsync()),
            ("Fragmented and interleaved probe replies preserve input", await FragmentedAndInterleavedProbeAsync()),
            ("Precedence: CSI 16 t wins over conflicting OSC 1337", await PrecedenceAndDisagreementAsync()),
            ("Fractional derivation from window/grid geometry", await FractionalDerivationAsync()),
            ("Implausible dimensions are rejected with diagnostics", await ImplausibleDimensionRejectionAsync()),
            ("Resize invalidates only derived metrics, not support", await ResizeInvalidatesDerivedMetricsOnlyAsync()),
            ("A later metrics change never rewrites an existing placement", await PlacementImmutabilityAcrossMetricsChangeAsync()),
            ("Query ownership and support advertisement", await QueryOwnershipAndAdvertisementAsync()),
        ];
    }

    // Wire-format reply builders, matching ConsolePresentationAdapter's parsers
    // exactly (see SixelCapabilityDiscoveryTests.cs, which these mirror). ------

    private static string FormatNumber(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Da1Reply(bool declaresSixel) => declaresSixel ? "\x1b[?62;4c" : "\x1b[?62c";

    private static string Csi16Reply(double heightPx, double widthPx) =>
        $"\x1b[6;{FormatNumber(heightPx)};{FormatNumber(widthPx)}t";

    private static string Csi14Reply(double heightPx, double widthPx) =>
        $"\x1b[4;{FormatNumber(heightPx)};{FormatNumber(widthPx)}t";

    private static string Csi18Reply(double rows, double cols) =>
        $"\x1b[8;{FormatNumber(rows)};{FormatNumber(cols)}t";

    private static string Osc1337Reply(double heightPoints, double widthPoints) =>
        $"\x1b]1337;ReportCellSize={FormatNumber(heightPoints)};{FormatNumber(widthPoints)}\x1b\\";

    private const uint KgpProbeImageId = 2147483647u;

    private static readonly string KgpReply = $"\x1b_Gi={KgpProbeImageId};OK\x1b\\";

    private const string BackgroundReply = "\x1b]11;rgb:1200/3400/5600\x1b\\";

    // 1. Direct declaration pre-empts probing entirely. ----------------------

    private static async Task<string> DirectDeclarationSkipsProbingAsync()
    {
        using var driver = new FakeConsoleDriver(); // No replies queued: a probe would hang.
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: DemoProbeTimeout);
        var declared = new SixelCellMetrics(11, 22, SixelCellMetricsSource.Direct, SixelCellMetricsReliability.Authoritative);
        adapter.WithSixelSupport(SixelPresentationSupport.Native, declared);

        var beforeProbe = adapter.Capabilities;
        await adapter.EnterRawModeAsync();

        // The KGP/background-colour probe pass still runs unconditionally (it is an
        // unrelated capability), so written bytes are not zero overall; what a direct
        // declaration pre-empts is only the five Sixel-specific probe queries below.
        var written = driver.WrittenText;
        string[] sixelProbeQueries = ["\x1b[c", "\x1b[16t", "\x1b[14t", "\x1b[18t", "\x1b]1337;ReportCellSize\x1b\\"];
        var sixelProbeQueriesSent = sixelProbeQueries.Count(written.Contains);

        return
            $"before EnterRawModeAsync: SixelSupport={beforeProbe.SixelSupport}, SixelCellMetrics={beforeProbe.SixelCellMetrics}; " +
            $"after EnterRawModeAsync: SixelSupport={adapter.Capabilities.SixelSupport}, SixelCellMetrics={adapter.Capabilities.SixelCellMetrics}, " +
            $"wrote {written.Length} total probe byte(s) but {sixelProbeQueriesSent}/5 Sixel-specific probe queries " +
            "(0 expected — a direct declaration pre-empts only the Sixel-specific queries, not the unrelated KGP/background pass), " +
            $"diagnostics still NotProbed: {adapter.SixelProbeDiagnostics.Attempts.Count == 0}";
    }

    // 2. Fragmented (single-byte) AND interleaved replies, alongside the ------
    //    existing KGP/background probe and arbitrary keyboard input, preserve
    //    every unrelated byte in order.

    private static async Task<string> FragmentedAndInterleavedProbeAsync()
    {
        // Every reply is split into single-byte read chunks and interwoven with
        // single-byte "keyboard" fragments and the pre-existing KGP/background
        // probe reply, so the demultiplexer must recognize each signature byte
        // by byte regardless of what surrounds it.
        var da1 = Da1Reply(declaresSixel: true);
        var csi16 = Csi16Reply(heightPx: 19, widthPx: 9);
        const string keyboard = "ab" + "cd" + "ef" + "ghi";

        var chunks = new List<string>();
        chunks.AddRange(keyboard[..2].Select(c => c.ToString()));
        chunks.AddRange(da1.Select(c => c.ToString()));
        chunks.AddRange(keyboard[2..4].Select(c => c.ToString()));
        chunks.AddRange(csi16.Select(c => c.ToString()));
        chunks.AddRange(keyboard[4..6].Select(c => c.ToString()));
        chunks.Add(KgpReply);
        chunks.Add(BackgroundReply);
        chunks.AddRange(keyboard[6..].Select(c => c.ToString()));

        using var driver = new FakeConsoleDriver([.. chunks]);
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: DemoProbeTimeout);
        await adapter.EnterRawModeAsync();

        var preserved = await adapter.ReadInputAsync();
        var preservedText = Encoding.UTF8.GetString(preserved.Span);

        return
            $"fed DA1+CSI16 fragmented one byte at a time, interleaved with keyboard bytes '{keyboard}' " +
            $"and the existing KGP/background probe replies; resolved SixelSupport={adapter.Capabilities.SixelSupport}, " +
            $"SixelCellMetrics={adapter.Capabilities.SixelCellMetrics}, SupportsKgp={adapter.Capabilities.SupportsKgp}; " +
            $"preserved input '{preservedText}' (byte-for-byte, in order, matches '{keyboard}': {preservedText == keyboard})";
    }

    // 3. CSI 16 t wins over a conflicting OSC 1337 value; disagreement is -----
    //    surfaced as a diagnostic, never silently dropped.

    private static async Task<string> PrecedenceAndDisagreementAsync()
    {
        var csi16 = Csi16Reply(heightPx: 19, widthPx: 9);
        var osc1337 = Osc1337Reply(heightPoints: 24, widthPoints: 12);
        var da1 = Da1Reply(declaresSixel: true);

        using var driver = new FakeConsoleDriver(da1 + csi16 + osc1337 + KgpReply + BackgroundReply);
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: DemoProbeTimeout);
        await adapter.EnterRawModeAsync();

        var metrics = adapter.Capabilities.SixelCellMetrics;
        var diagnostics = adapter.SixelProbeDiagnostics;

        return
            $"CSI16 reported 9x19px, OSC1337 reported 12x24pt; selected metrics source={metrics?.Source}, " +
            $"width={metrics?.Width}, height={metrics?.Height} (CSI16 wins per precedence); " +
            $"MetricsDisagreement={diagnostics.MetricsDisagreement}, DisagreementDetail=\"{diagnostics.DisagreementDetail}\"";
    }

    // 4. Fractional derivation: only CSI 14 (window pixels) and CSI 18 -------
    //    (grid) respond, so metrics are derived and fractional, never assumed
    //    authoritative.

    private static async Task<string> FractionalDerivationAsync()
    {
        var csi14 = Csi14Reply(heightPx: 634, widthPx: 803); // Deliberately not evenly divisible.
        var csi18 = Csi18Reply(rows: 31, cols: 80);
        var da1 = Da1Reply(declaresSixel: true);

        using var driver = new FakeConsoleDriver(da1 + csi14 + csi18 + KgpReply + BackgroundReply);
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: DemoProbeTimeout);
        await adapter.EnterRawModeAsync();

        var metrics = adapter.Capabilities.SixelCellMetrics;

        return
            $"window reported 803x634px over an 80x31 grid (no CSI16/OSC1337 reply); derived cell size " +
            $"{metrics?.Width:0.###}x{metrics?.Height:0.###}px (803/80={803.0 / 80:0.###}, 634/31={634.0 / 31:0.###}), " +
            $"source={metrics?.Source}, reliability={metrics?.Reliability} (fractional, non-authoritative)";
    }

    // 5. Zero, negative, and non-finite dimensions are rejected with a -------
    //    diagnostic detail rather than silently accepted or crashing.

    private static async Task<string> ImplausibleDimensionRejectionAsync()
    {
        // The window-operation parser only scans through digit/';' bytes (see
        // TryConsumeWindowOperationResponse), so a negative sign there simply
        // never matches rather than surfacing as Malformed. OSC 1337 has no
        // such pre-filter, so it is the reply used here to demonstrate
        // plausibility rejection after a value parses successfully.
        var da1 = Da1Reply(declaresSixel: true);
        var osc1337 = "\x1b]1337;ReportCellSize=-5;20\x1b\\";

        using var driver = new FakeConsoleDriver(da1 + osc1337 + KgpReply + BackgroundReply);
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: DemoProbeTimeout);
        await adapter.EnterRawModeAsync();

        var attempt = adapter.SixelProbeDiagnostics.Attempts
            .FirstOrDefault(a => a.Source == SixelCellMetricsSource.Osc1337);

        return
            $"OSC1337 reported a negative height (-5); outcome={attempt.Outcome}, detail=\"{attempt.Detail}\"; " +
            $"SixelCellMetrics={adapter.Capabilities.SixelCellMetrics} (rejected values never become the selected metrics)";
    }

    // 6. Resize invalidates only Derived-sourced metrics; SixelSupport itself -
    //    (a fundamental capability) is untouched.

    private static async Task<string> ResizeInvalidatesDerivedMetricsOnlyAsync()
    {
        var csi14 = Csi14Reply(heightPx: 640, widthPx: 800);
        var csi18 = Csi18Reply(rows: 32, cols: 80);
        var da1 = Da1Reply(declaresSixel: true);

        using var driver = new FakeConsoleDriver(da1 + csi14 + csi18 + KgpReply + BackgroundReply)
        {
            WindowPixelSize = (800, 640),
        };
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: DemoProbeTimeout);
        await adapter.EnterRawModeAsync();

        var beforeResize = adapter.Capabilities;
        driver.RaiseResized(120, 40);
        var afterResize = adapter.Capabilities;

        return
            $"derived {beforeResize.SixelCellMetrics?.Width:0.###}x{beforeResize.SixelCellMetrics?.Height:0.###}px cells " +
            $"(source={beforeResize.SixelCellMetrics?.Source}) before resize; after a 120x40 resize, " +
            $"SixelCellMetrics={(afterResize.SixelCellMetrics is null ? "null (invalidated)" : "still set (unexpected)")}, " +
            $"SixelSupport={afterResize.SixelSupport} (unchanged — a resize does not change what the presentation can render)";
    }

    // 7. A metrics change made after a placement exists never rewrites that ---
    //    placement's already-recorded metrics; only later placements see it.

    private static async Task<string> PlacementImmutabilityAcrossMetricsChangeAsync()
    {
        var workload = new ScriptedWorkloadAdapter();
        var capabilities = new TerminalCapabilities { SupportsSixel = true };
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithPresentation(new HeadlessPresentationAdapter(80, 24, capabilities))
            .WithDimensions(80, 24)
            .Build();

        terminal.SetSixelCellMetrics(new SixelCellMetrics(
            10, 20, SixelCellMetricsSource.Direct, SixelCellMetricsReliability.Authoritative));

        var firstFixture = RawSixelFixtures.All[0]; // "Solid RGB block": an independently authored raw DCS fixture.
        workload.EnqueueOutput(firstFixture.StandardDcsBytes);
        await WaitUntilAsync(() => terminal.SixelPlacementCount == 1);

        var firstMetricsAtCreation = terminal.SixelPlacements[0].Image.CellMetrics;

        // Change the override metrics AFTER the first placement was created.
        terminal.SetSixelCellMetrics(new SixelCellMetrics(
            20, 40, SixelCellMetricsSource.Direct, SixelCellMetricsReliability.Authoritative));

        var firstMetricsAfterChange = terminal.SixelPlacements[0].Image.CellMetrics;

        // A second placement, on a fresh row, under the new metrics. It must use
        // different payload content than the first: Sixel data is content-
        // addressable (see SixelData's remarks), so reusing the same payload would
        // return the same cached SixelData instance — and its own CellMetrics,
        // captured once at first creation — rather than exercising a fresh capture
        // under the changed metrics.
        var secondFixture = RawSixelFixtures.All[1]; // "RGB rounding": distinct payload content.
        workload.EnqueueOutput(Encoding.ASCII.GetBytes("\x1b[10;1H").Concat(secondFixture.StandardDcsBytes).ToArray());
        await WaitUntilAsync(() => terminal.SixelPlacementCount == 2);

        var secondMetrics = terminal.SixelPlacements[1].Image.CellMetrics;
        await workload.DisposeAsync();

        return
            $"first placement recorded {firstMetricsAtCreation.Width}x{firstMetricsAtCreation.Height}px cells at creation; " +
            $"after SetSixelCellMetrics(20x40) ran, the same placement still reports " +
            $"{firstMetricsAfterChange.Width}x{firstMetricsAfterChange.Height}px (unchanged: {firstMetricsAfterChange == firstMetricsAtCreation}); " +
            $"a second placement created afterward reports {secondMetrics.Width}x{secondMetrics.Height}px (picks up the new metrics)";
    }

    // 8. Query ownership: native passthrough stays silent (the real upstream ---
    //    terminal answers), while every other presentation gets a synthesized
    //    reply from Hex1bTerminal's own authoritative model, and effective
    //    support (not raw parser capability) governs advertisement.

    private static async Task<string> QueryOwnershipAndAdvertisementAsync()
    {
        const string da1Query = "\x1b[c";
        var results = new List<string>();

        // Native (ConsolePresentationAdapter over a fake driver): the adapter
        // itself implements INativeUpstreamPresentationAdapter, so Hex1bTerminal
        // must not synthesize a reply — the (simulated) real terminal already
        // answers over the same raw byte channel.
        {
            using var driver = new FakeConsoleDriver(); // No reply queued: nothing should be written back either.
            await using var presentation = new ConsolePresentationAdapter(driver, kgpProbeTimeout: DemoProbeTimeout);
            presentation.WithSixelSupport(SixelPresentationSupport.Native);
            await presentation.EnterRawModeAsync();

            var workload = new ScriptedWorkloadAdapter();
            var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
            {
                PresentationAdapter = presentation,
                WorkloadAdapter = workload,
                Width = 80,
                Height = 24,
            });
            await using var t = terminal;

            workload.EnqueueOutput(da1Query);
            await Task.Delay(TimeSpan.FromMilliseconds(150));
            results.Add($"native ConsolePresentationAdapter: workload received {workload.WrittenBytes.Length} synthesized byte(s) (0 expected — silence, the upstream terminal owns the reply)");
        }

        // Default headless (no declared Sixel support): capability discovery has
        // not run, so SixelSupport defaults to Unknown — a distinct value from a
        // confirmed-unsupported declaration, but Hex1bTerminal must still treat
        // both the same way for advertisement purposes: it owns the reply and
        // correctly reports "not available."
        {
            var presentation = new HeadlessPresentationAdapter(80, 24);
            var workload = new ScriptedWorkloadAdapter();
            var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
            {
                PresentationAdapter = presentation,
                WorkloadAdapter = workload,
                Width = 80,
                Height = 24,
            });
            await using var t = terminal;

            workload.EnqueueOutput(da1Query);
            await workload.WaitForWrittenLengthAsync(1);
            results.Add($"default headless (SixelSupport={presentation.Capabilities.SixelSupport}): reply={Encoding.UTF8.GetString(workload.WrittenBytes)} (no parameter 4 — an unknown/no-probe state never advertises Sixel)");
        }

        // Explicitly confirmed unsupported (SixelSupport.None): distinct from the
        // Unknown default above in the capability model itself, but reaches the
        // same workload-facing answer, since neither is an affirmative "yes."
        {
            var capabilities = new TerminalCapabilities
            {
                SupportsSixel = false,
                SixelSupport = SixelPresentationSupport.None,
            };
            var presentation = new HeadlessPresentationAdapter(80, 24, capabilities);
            var workload = new ScriptedWorkloadAdapter();
            var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
            {
                PresentationAdapter = presentation,
                WorkloadAdapter = workload,
                Width = 80,
                Height = 24,
            });
            await using var t = terminal;

            workload.EnqueueOutput(da1Query);
            await workload.WaitForWrittenLengthAsync(1);
            results.Add($"confirmed-unsupported headless (SixelSupport={presentation.Capabilities.SixelSupport}): reply={Encoding.UTF8.GetString(workload.WrittenBytes)} (no parameter 4 — same answer as Unknown, for a different, explicit reason)");
        }

        // Authoritative headless (explicit SixelSupport.Headless + declared
        // metrics): Hex1bTerminal owns the reply and correctly advertises
        // support because the effective path (its own authoritative model) can
        // render Sixel, not merely because Hex1b's parser understands it.
        {
            var capabilities = new TerminalCapabilities
            {
                SupportsSixel = true,
                SixelSupport = SixelPresentationSupport.Headless,
                SixelCellMetrics = new SixelCellMetrics(10, 20, SixelCellMetricsSource.Direct, SixelCellMetricsReliability.Authoritative),
            };
            var presentation = new HeadlessPresentationAdapter(80, 24, capabilities);
            var workload = new ScriptedWorkloadAdapter();
            var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
            {
                PresentationAdapter = presentation,
                WorkloadAdapter = workload,
                Width = 80,
                Height = 24,
            });
            await using var t = terminal;

            workload.EnqueueOutput(da1Query);
            await workload.WaitForWrittenLengthAsync(1);
            results.Add($"authoritative headless: reply={Encoding.UTF8.GetString(workload.WrittenBytes)} (parameter 4 present — effective support governs advertisement)");
        }

        return string.Join("; ", results);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, int maxAttempts = 200, int delayMs = 10)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (predicate())
                return;
            await Task.Delay(delayMs);
        }

        throw new TimeoutException("Condition did not become true within the demo's bounded wait.");
    }
}

/// <summary>
/// A fake <c>IConsoleDriver</c> with queued read chunks and captured written
/// bytes, so <see cref="ConsolePresentationAdapter"/>'s probe can be exercised
/// deterministically without a real terminal. This mirrors
/// <c>tests/Hex1b.Tests/ConsolePresentationAdapterTests.cs</c>'s test double of
/// the same name.
/// </summary>
internal sealed class FakeConsoleDriver : IConsoleDriver
{
    private readonly Queue<byte[]> _readChunks = new();
    private readonly List<byte> _written = [];

    public FakeConsoleDriver(params string[] readChunks)
    {
        foreach (var chunk in readChunks)
            _readChunks.Enqueue(Encoding.ASCII.GetBytes(chunk));
    }

    public bool DataAvailable => _readChunks.Count > 0;

    public int Width => 80;

    public int Height => 24;

    public Encoding InputEncoding { get; init; } = Encoding.UTF8;

    public string WrittenText => Encoding.ASCII.GetString([.. _written]);

    /// <summary>
    /// When set, <see cref="TryGetWindowPixelSize"/> reports these values;
    /// otherwise it reports failure, matching a platform with no
    /// <c>TIOCGWINSZ</c>-equivalent pixel fields available.
    /// </summary>
    public (int Width, int Height)? WindowPixelSize { get; init; }

    public event Action<int, int>? Resized;

    /// <summary>Simulates a terminal resize notification.</summary>
    public void RaiseResized(int width, int height) => Resized?.Invoke(width, height);

    public bool TryGetWindowPixelSize(out int pixelWidth, out int pixelHeight)
    {
        if (WindowPixelSize is { } size)
        {
            pixelWidth = size.Width;
            pixelHeight = size.Height;
            return true;
        }

        pixelWidth = 0;
        pixelHeight = 0;
        return false;
    }

    public void EnterRawMode(bool preserveOPost = false)
    {
    }

    public void ExitRawMode()
    {
    }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (_readChunks.Count > 0)
        {
            var chunk = _readChunks.Dequeue();
            chunk.AsSpan().CopyTo(buffer.Span);
            return ValueTask.FromResult(chunk.Length);
        }

        return WaitForCancellationAsync(ct);
    }

    public void Write(ReadOnlySpan<byte> data) => _written.AddRange(data.ToArray());

    public void Flush()
    {
    }

    public void DrainInput() => _readChunks.Clear();

    public void Dispose()
    {
    }

    private static async ValueTask<int> WaitForCancellationAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
        catch (OperationCanceledException)
        {
        }

        return 0;
    }
}

/// <summary>
/// A scripted <c>IHex1bTerminalWorkloadAdapter</c>: bytes enqueued via
/// <see cref="EnqueueOutput(string)"/>/<see cref="EnqueueOutput(byte[])"/> are
/// what a hosted application would write (protocol queries or Sixel DCS
/// sequences); bytes <see cref="Hex1bTerminal"/> writes back are captured for
/// inspection, mirroring how a real workload would receive a synthesized reply
/// as ordinary input. Adapted from
/// <c>Hex1bTerminalQueryOwnershipTests.cs</c>'s <c>QueuedOutputWorkloadAdapter</c>.
/// </summary>
internal sealed class ScriptedWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    private readonly Channel<ReadOnlyMemory<byte>> _output = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    private readonly List<byte> _written = [];
    private TaskCompletionSource _writtenChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public event Action? Disconnected
    {
        add { }
        remove { }
    }

    public void EnqueueOutput(string text) => _output.Writer.TryWrite(Encoding.UTF8.GetBytes(text));

    public void EnqueueOutput(byte[] bytes) => _output.Writer.TryWrite(bytes);

    public byte[] WrittenBytes
    {
        get
        {
            lock (_written)
                return [.. _written];
        }
    }

    public async Task WaitForWrittenLengthAsync(int minimumLength, int timeoutMs = 2000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        while (true)
        {
            Task changed;
            lock (_written)
            {
                if (_written.Count >= minimumLength)
                    return;
                changed = _writtenChanged.Task;
            }

            await changed.WaitAsync(cts.Token);
        }
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        while (await _output.Reader.WaitToReadAsync(ct))
        {
            if (_output.Reader.TryRead(out var item))
                return item;
        }

        return ReadOnlyMemory<byte>.Empty;
    }

    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        TaskCompletionSource changed;
        lock (_written)
        {
            _written.AddRange(data.ToArray());
            changed = _writtenChanged;
            _writtenChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        changed.TrySetResult();
        return ValueTask.CompletedTask;
    }

    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        _output.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
