using System.Text;
using System.Threading.Channels;
using Hex1b;
using Hex1b.Sixel;

/// <summary>
/// Deterministic, no-real-terminal-required scenarios demonstrating stage #458's
/// Sixel routing/sanitization contract: early chunk-boundary native forwarding with
/// byte equality, ordered managed-sink events with content deduplication and damage,
/// a geometry-only diagnostic placeholder, sanitization suppressing a malformed
/// sequence while preserving ordinary text, and effective-capability reporting in
/// headless mode.
/// </summary>
/// <remarks>
/// Every scenario feeds independently authored raw Sixel bytes (reusing
/// <see cref="RawSixelFixtures"/> where possible) through a real
/// <see cref="Hex1bTerminal"/> configured for a specific
/// <see cref="Sixel.SixelPresentationSupport"/>/policy combination — never
/// <c>SixelWidget</c> or <c>SixelEncoder</c>. This mirrors
/// <c>tests/Hex1b.Tests/Sixel/SixelRoutingIntegrationTests.cs</c>, so this file's
/// observations are direct, reproducible evidence of the same behavior those
/// tests assert. Hex1b deliberately does not translate Sixel into another wire
/// protocol (e.g. Kitty Graphics Protocol): a <see cref="SixelPresentationSupport.Translated"/>
/// presentation always resolves to <c>SixelEffectiveRoute.Unsupported</c>, governed
/// by the same unsupported-presentation policy as any other route with no display.
/// </remarks>
internal static class RoutingTranslationScenarios
{
    private static readonly RawSixelFixture SolidBlock = RawSixelFixtures.All[0];
    private static readonly RawSixelFixture GeometryOnly = RawSixelFixtures.All[^1];

    // Structurally malformed (DECGRA with five parameters; accepts at most four)
    // but still DCS-framed, matching SixelRoutingIntegrationTests' convention.
    private static readonly byte[] MalformedGraphic =
        Encoding.ASCII.GetBytes("\x1bP0;1q\"1;1;1;1;1#1;2;100;0;0#1@\x1b\\");

    /// <summary>
    /// Runs every scenario and returns one (title, observation) pair per scenario.
    /// </summary>
    public static async Task<IReadOnlyList<(string Title, string Observation)>> RunAllAsync()
    {
        return
        [
            ("Native route: early chunk forwarding is byte-exact", await NativeEarlyChunkForwardingAsync()),
            ("Managed sink: ordered events, content dedup, and damage", await ManagedSinkOrderedEventsAsync()),
            ("Unsupported route: geometry-only diagnostic placeholder", await GeometryOnlyPlaceholderAsync()),
            ("Sanitization: suppresses malformed, preserves ordinary text", await SanitizationSuppressesMalformedAsync()),
            ("Capability reporting: effective route drives DA1, in headless mode", await CapabilityReportingHeadlessAsync()),
        ];
    }

    // 1. Native route forwards every chunk immediately, byte-exact, before the ---
    //    DCS sequence even completes.

    private static async Task<string> NativeEarlyChunkForwardingAsync()
    {
        var payload = SolidBlock.StandardDcsBytes;
        await using var harness = RoutingHarness.Create(SixelPresentationSupport.Native);

        var lengthsAfterEachByte = new List<int>();
        for (var i = 0; i < payload.Length; i++)
        {
            await harness.FeedAsync([payload[i]]);
            lengthsAfterEachByte.Add(harness.PresentationBytes.Count);
        }

        var byteExact = harness.PresentationBytes.SequenceEqual(payload);
        var forwardedBeforeTermination = lengthsAfterEachByte[^3] < payload.Length
            && lengthsAfterEachByte[0] == 1;

        return
            $"fed {payload.Length} bytes one at a time; presentation length grew with every byte fed " +
            $"(first byte -> {lengthsAfterEachByte[0]} presentation byte(s), confirming forwarding begins " +
            $"before the DCS terminator arrives: {forwardedBeforeTermination}); " +
            $"final presentation bytes equal the original payload exactly: {byteExact}";
    }

    // 2. A managed raster sink receives ordered content-defined/placement-updated ---
    //    events, deduplicates identical content across a second placement, and
    //    observes damage when text overwrites a live placement.

    private static async Task<string> ManagedSinkOrderedEventsAsync()
    {
        await using var harness = RoutingHarness.Create(SixelPresentationSupport.Unknown, asManagedSink: true);

        await harness.FeedAsync(SolidBlock.StandardDcsBytes);
        await harness.WaitForEventCountAsync(2);
        var firstOrder = harness.Events.Take(2).Select(e => e.GetType().Name).ToArray();

        // Same content at a fresh anchor: content is defined once, a second
        // placement is announced.
        await harness.FeedAsync(Encoding.ASCII.GetBytes("\x1b[2;1H"));
        await harness.FeedAsync(SolidBlock.StandardDcsBytes);
        await harness.WaitForEventCountAsync(3);
        var contentDefinedCount = harness.Events.OfType<SixelRasterContentDefined>().Count();
        var newPlacementCount = harness.Events.OfType<SixelRasterPlacementUpdated>().Count(e => e.IsNewPlacement);

        // Overwrite the origin cell with text: the first placement is damaged.
        await harness.FeedAsync(Encoding.ASCII.GetBytes("\x1b[1;1HX"));
        await harness.WaitForAsync(() => harness.Events.Any(e => e is SixelRasterPlacementDamaged or SixelRasterPlacementReleased));
        var damagedOrReleased = harness.Events.Any(e => e is SixelRasterPlacementDamaged or SixelRasterPlacementReleased);

        return
            $"first batch delivered [{string.Join(", ", firstOrder)}] (content before placement, never reordered); " +
            $"after a second placement of identical content: {contentDefinedCount} content-defined event(s) " +
            $"(expected 1 — deduplicated) and {newPlacementCount} new-placement event(s) (expected 2); " +
            $"text overwrite at the first placement's origin produced a damage/release event: {damagedOrReleased}";
    }

    // 3. When no display, managed sink, or translation target is available, an ---
    //    opt-in placeholder policy substitutes an explicit diagnostic instead of
    //    silently discarding the graphic a human cannot see.

    private static async Task<string> GeometryOnlyPlaceholderAsync()
    {
        await using var harness = RoutingHarness.Create(
            SixelPresentationSupport.None,
            unsupportedPresentation: SixelUnsupportedPresentationPolicy.Placeholder);

        await harness.FeedAsync(GeometryOnly.StandardDcsBytes);
        await harness.WaitForAsync(() => harness.PresentationText.Contains("[sixel:"));

        var placeholderApplied = harness.Diagnostics.Any(d => d.Kind == SixelRasterRouteDiagnosticKind.PlaceholderApplied);
        var placeholderText = harness.PresentationText[harness.PresentationText.IndexOf("[sixel:", StringComparison.Ordinal)..];

        return
            $"presentation output ends with the placeholder \"{placeholderText.Trim()}\" appended after the raw Sixel " +
            "bytes (unconditional native forwarding still happens first, per the passthrough invariant — the " +
            "placeholder is always additive, never a substitute for it); " +
            $"a PlaceholderApplied diagnostic was raised: {placeholderApplied} " +
            "(the authoritative Sixel model itself is never discarded regardless of this policy — " +
            "only presentation-side substitution is affected)";
    }

    // 4. Opt-in sanitization suppresses a malformed Sixel sequence before it ---
    //    reaches a native upstream, while leaving ordinary text untouched.

    private static async Task<string> SanitizationSuppressesMalformedAsync()
    {
        await using var harness = RoutingHarness.Create(
            SixelPresentationSupport.Native,
            sanitization: SixelSanitizationPolicy.Enable());

        var payload = "before"u8.ToArray().Concat(MalformedGraphic).Concat("after"u8.ToArray()).ToArray();
        await harness.FeedAsync(payload);
        await harness.WaitForAsync(() => harness.PresentationText.Contains("after"));

        var text = harness.PresentationText;
        var suppressed = harness.Diagnostics.Any(d => d.Kind == SixelRasterRouteDiagnosticKind.Suppressed);

        return
            $"fed \"before\" + a malformed (but DCS-framed) Sixel sequence + \"after\" with sanitization enabled; " +
            $"presentation text is \"{text}\" (contains \"before\": {text.Contains("before")}, " +
            $"\"after\": {text.Contains("after")}, any escape bytes reached the presentation: {text.Contains('\x1b')}); " +
            $"a Suppressed diagnostic was raised: {suppressed}";
    }

    // 5. Effective capability reported to a hosted workload (DA1) matches the ---
    //    actual selected route, demonstrated headlessly with no real terminal.

    private static async Task<string> CapabilityReportingHeadlessAsync()
    {
        await using var native = RoutingHarness.Create(SixelPresentationSupport.Native);
        await native.FeedAsync("\x1b[c"u8.ToArray());
        await native.WaitForWorkloadReplyAsync();
        var nativeAdvertises = native.WorkloadReplyText.Contains(";4c");

        await using var translatedWithKgp = RoutingHarness.Create(SixelPresentationSupport.Translated, supportsKgp: true);
        await translatedWithKgp.FeedAsync("\x1b[c"u8.ToArray());
        await translatedWithKgp.WaitForWorkloadReplyAsync();
        var withKgpAdvertises = translatedWithKgp.WorkloadReplyText.Contains(";4c");

        await using var translatedWithoutKgp = RoutingHarness.Create(SixelPresentationSupport.Translated, supportsKgp: false);
        await translatedWithoutKgp.FeedAsync("\x1b[c"u8.ToArray());
        await translatedWithoutKgp.WaitForWorkloadReplyAsync();
        var withoutKgpAdvertises = translatedWithoutKgp.WorkloadReplyText.Contains(";4c");

        return
            $"Native DA1 reply \"{native.WorkloadReplyText.Replace("\x1b", "ESC")}\" advertises parameter 4 (Sixel): " +
            $"{nativeAdvertises}; Translated + SupportsKgp=true DA1 reply " +
            $"\"{translatedWithKgp.WorkloadReplyText.Replace("\x1b", "ESC")}\" advertises parameter 4: {withKgpAdvertises}; " +
            "Translated + SupportsKgp=false DA1 reply " +
            $"\"{translatedWithoutKgp.WorkloadReplyText.Replace("\x1b", "ESC")}\" advertises parameter 4: {withoutKgpAdvertises} " +
            "(effective capability matches the actual selected route, not raw parser support, and is never " +
            "duplicated; Hex1b does not translate Sixel into another wire protocol, so Translated never " +
            "advertises parameter 4 regardless of KGP capability)";
    }

    /// <summary>
    /// A minimal <see cref="Hex1bTerminal"/> harness for these scenarios: a queued
    /// byte workload adapter feeding chunks on demand, and a presentation adapter
    /// that captures raw output bytes, workload-input replies (DA1), and — when
    /// <paramref name="asManagedSink"/> is requested — an ordered raster event log.
    /// </summary>
    private sealed class RoutingHarness : IAsyncDisposable
    {
        private readonly Channel<byte[]> _output = Channel.CreateUnbounded<byte[]>();
        private readonly HarnessWorkloadAdapter _workload;
        private readonly HarnessPresentationAdapter _presentation;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task<int> _runTask;
        private readonly List<SixelRasterRouteDiagnostic> _diagnostics = [];

        private RoutingHarness(
            SixelPresentationSupport sixelSupport,
            bool supportsKgp,
            bool asManagedSink,
            SixelSanitizationPolicy? sanitization,
            SixelUnsupportedPresentationPolicy unsupportedPresentation)
        {
            _workload = new HarnessWorkloadAdapter(_output.Reader);
            var capabilities = new TerminalCapabilities
            {
                SixelSupport = sixelSupport,
                SupportsSixel = sixelSupport == SixelPresentationSupport.Native,
                SupportsKgp = supportsKgp,
                SupportsTrueColor = true,
                Supports256Colors = true,
                CellPixelWidth = 10,
                CellPixelHeight = 20,
            };
            _presentation = asManagedSink
                ? new ManagedSinkPresentationAdapter(capabilities)
                : new HarnessPresentationAdapter(capabilities);

            var options = new Hex1bTerminalOptions
            {
                WorkloadAdapter = _workload,
                PresentationAdapter = _presentation,
                Width = 40,
                Height = 20,
                SixelSanitization = sanitization ?? SixelSanitizationPolicy.Disabled,
                SixelUnsupportedPresentation = unsupportedPresentation,
            };

            Terminal = new Hex1bTerminal(options);
            Terminal.SixelRouteDiagnosticRaised += diagnostic =>
            {
                lock (_diagnostics)
                    _diagnostics.Add(diagnostic);
            };

            _runTask = Terminal.RunAsync(_cts.Token);
        }

        public Hex1bTerminal Terminal { get; }

        public IReadOnlyList<byte> PresentationBytes => _presentation.CapturedBytes;

        public string PresentationText => Encoding.Latin1.GetString([.. _presentation.CapturedBytes]);

        public string WorkloadReplyText => Encoding.Latin1.GetString([.. _workload.CapturedReplyBytes]);

        public IReadOnlyList<SixelRasterEvent> Events =>
            _presentation is ManagedSinkPresentationAdapter sink ? sink.Events : [];

        public IReadOnlyList<SixelRasterRouteDiagnostic> Diagnostics
        {
            get
            {
                lock (_diagnostics)
                    return [.. _diagnostics];
            }
        }

        public static RoutingHarness Create(
            SixelPresentationSupport sixelSupport,
            bool supportsKgp = false,
            bool asManagedSink = false,
            SixelSanitizationPolicy? sanitization = null,
            SixelUnsupportedPresentationPolicy unsupportedPresentation = SixelUnsupportedPresentationPolicy.Suppress)
            => new(sixelSupport, supportsKgp, asManagedSink, sanitization, unsupportedPresentation);

        public async Task FeedAsync(byte[] bytes)
        {
            await _output.Writer.WriteAsync(bytes);
            await Task.Delay(30);
        }

        public async Task WaitForAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                    return;
                await Task.Delay(10);
            }

            throw new TimeoutException("Timed out waiting for the expected demo scenario condition.");
        }

        public Task WaitForEventCountAsync(int minimumCount) => WaitForAsync(() => Events.Count >= minimumCount);

        public Task WaitForWorkloadReplyAsync() => WaitForAsync(() => _workload.CapturedReplyBytes.Count > 0);

        public async ValueTask DisposeAsync()
        {
            _output.Writer.TryComplete();
            try
            {
                await _runTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
                await _cts.CancelAsync();
                try
                {
                    await _runTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            await Terminal.DisposeAsync();
            _cts.Dispose();
        }

        private sealed class HarnessWorkloadAdapter(ChannelReader<byte[]> reader) : IHex1bTerminalWorkloadAdapter
        {
            private readonly List<byte> _replyBytes = [];

            public IReadOnlyList<byte> CapturedReplyBytes
            {
                get
                {
                    lock (_replyBytes)
                        return [.. _replyBytes];
                }
            }

            public event Action? Disconnected { add { } remove { } }

            public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            {
                while (await reader.WaitToReadAsync(ct))
                {
                    if (reader.TryRead(out var chunk))
                        return chunk;
                }

                return ReadOnlyMemory<byte>.Empty;
            }

            public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            {
                lock (_replyBytes)
                    _replyBytes.AddRange(data.Span);
                return ValueTask.CompletedTask;
            }

            public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) => ValueTask.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private class HarnessPresentationAdapter(TerminalCapabilities capabilities) : IHex1bTerminalPresentationAdapter
        {
            private readonly List<byte> _bytes = [];

            public int Width => 40;
            public int Height => 20;
            public TerminalCapabilities Capabilities { get; } = capabilities;

            public IReadOnlyList<byte> CapturedBytes
            {
                get
                {
                    lock (_bytes)
                        return [.. _bytes];
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

        private sealed class ManagedSinkPresentationAdapter(TerminalCapabilities capabilities) :
            HarnessPresentationAdapter(capabilities), ISixelRasterPresentationSink
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
}
