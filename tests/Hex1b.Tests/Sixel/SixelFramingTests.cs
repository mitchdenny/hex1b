using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text;
using Hex1b.Diagnostics;
using Hex1b.Sixel;
using Hex1b.Tokens;

namespace Hex1b.Tests.Sixel;

[TestClass]
public class SixelFramingTests
{
    private static readonly SixelFixture SingleBand = SixelFixture.Load(
        "single-band",
        "One red pixel at the top of a six-pixel band.");

    [TestMethod]
    public async Task StandardFraming_EverySplitBoundary_ProducesIdenticalModelAndPresentationBytes()
    {
        var runs = await SixelTestTerminal.ObserveEverySplitAsync(
            SingleBand,
            TestContext.Current.CancellationToken);
        var baseline = runs[0].Observation.ModelFingerprint();

        Assert.AreEqual(SingleBand.StandardBytes.Length, runs.Count);
        foreach (var run in runs)
        {
            TestSeq.AreEqual(
                SingleBand.StandardBytes,
                run.PresentationBytes,
                $"Presentation bytes changed at split boundary {run.SplitBoundary}.");
            Assert.AreEqual(
                baseline,
                run.Observation.ModelFingerprint(),
                $"Terminal model changed at split boundary {run.SplitBoundary}.");
        }
    }

    [TestMethod]
    public async Task NativePassthrough_OneByteChunks_ForwardsBytesExactly()
    {
        await using var terminal = SixelTestTerminal.Create();
        var bytes = SingleBand.StandardBytes;

        await terminal.FeedAsync(
            bytes,
            Enumerable.Repeat(1, bytes.Length).ToArray(),
            TestContext.Current.CancellationToken);

        TestSeq.AreEqual(bytes, terminal.PresentationBytes);
    }

    [TestMethod]
    public async Task NativePassthrough_ModelRejectionAndGeometryOnlyRaster_ForwardBytesExactly()
    {
        await using var terminal = SixelTestTerminal.Create();
        var rejected = Encoding.ASCII.GetBytes("\x1bP?1q@\x1b\\");
        var geometryOnly = Encoding.ASCII.GetBytes(
            "\x1bP0;1q\"1;1;999999999;999999999#1@\x1b\\");
        var bytes = rejected.Concat(geometryOnly).ToArray();

        await terminal.FeedAsync(
            bytes,
            Enumerable.Repeat(1, bytes.Length).ToArray(),
            TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.SixelPlacements.Any(placement => placement.IsGeometryOnly),
            "geometry-only Sixel placement",
            TestContext.Current.CancellationToken);

        TestSeq.AreEqual(bytes, terminal.PresentationBytes);
        using var snapshot = terminal.Terminal.CreateSnapshot();
        var placement = TestSeq.Single(snapshot.SixelPlacements);
        Assert.IsTrue(placement.IsGeometryOnly);
    }

    [TestMethod]
    public async Task C1Framing_CompatibilityInput_ProducesSameModelAsStandardFraming()
    {
        await using var standard = SixelTestTerminal.Create();
        await standard.FeedAsync(
            SingleBand.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await standard.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "standard-framed Sixel",
            TestContext.Current.CancellationToken);

        var expectedModel = standard.Observe().ModelFingerprint();
        var runs = await SixelTestTerminal.ObserveEverySplitAsync(
            SingleBand,
            TestContext.Current.CancellationToken,
            useC1Framing: true);

        Assert.AreEqual(SingleBand.C1Bytes.Length, runs.Count);
        foreach (var run in runs)
        {
            TestSeq.AreEqual(
                SingleBand.C1Bytes,
                run.PresentationBytes,
                $"C1 presentation bytes changed at split boundary {run.SplitBoundary}.");
            Assert.AreEqual(expectedModel, run.Observation.ModelFingerprint());
        }
    }

    [TestMethod]
    public async Task Utf8EncodedC1Characters_AreTextAndCannotDispatchDcs()
    {
        await using var terminal = SixelTestTerminal.Create();
        var bytes = Encoding.UTF8.GetBytes("\u0090q@\u009cX");

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "text following UTF-8 encoded C1 characters",
            TestContext.Current.CancellationToken);

        Assert.IsEmpty(terminal.Observe().Placements);
        TestSeq.AreEqual(bytes, terminal.PresentationBytes);
    }

    [TestMethod]
    public async Task NonSixelDcs_CompleteSequence_IsNotTrackedAsSixel()
    {
        await using var terminal = SixelTestTerminal.Create();
        var bytes = Encoding.ASCII.GetBytes("\x1bP1+r544e\x1b\\X");

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "text following a non-Sixel DCS",
            TestContext.Current.CancellationToken);

        Assert.IsEmpty(terminal.Observe().Placements);
    }

    [TestMethod]
    public async Task Cancel_IncompleteSixel_DiscardsGraphicAndResumesText()
    {
        await using var terminal = SixelTestTerminal.Create();
        var bytes = Encoding.ASCII.GetBytes("\x1bPq#1;2;100;0;0@\x18X");

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "text following a cancelled Sixel DCS",
            TestContext.Current.CancellationToken);

        Assert.IsEmpty(terminal.Observe().Placements);
    }

    [TestMethod]
    public async Task DcsBoundary_AbortsIncompleteCsiBeforeFollowingText()
    {
        await using var terminal = SixelTestTerminal.Create();
        var bytes = Encoding.ASCII.GetBytes(
            "\x1b[31\x1bP1+r544e\x1b\\mX");

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("mX"),
            "text after DCS-interrupted CSI",
            TestContext.Current.CancellationToken);

        Assert.IsEmpty(terminal.Observe().Placements);
    }

    [TestMethod]
    public async Task DcsBoundary_AbortsIncompleteUtf8CodePoint()
    {
        await using var terminal = SixelTestTerminal.Create();
        var bytes = new byte[]
        {
            0xc2,
            0x1b, (byte)'P', (byte)'1', (byte)'+', (byte)'r', 0x1b, (byte)'\\',
            0xa2, (byte)'X',
        };

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "text after DCS-interrupted UTF-8 code point",
            TestContext.Current.CancellationToken);

        using var snapshot = terminal.Terminal.CreateSnapshot();
        Assert.IsFalse(snapshot.ContainsText("¢"));
        Assert.IsEmpty(terminal.Observe().Placements);
    }

    [TestMethod]
    public async Task NativePassthrough_WithBlockingWorkloadObserver_ForwardsOpenDcsImmediately()
    {
        var filter = new BlockingWorkloadFilter();
        await using var terminal = SixelTestTerminal.Create(workloadFilter: filter);
        var prefix = SingleBand.StandardBytes.AsMemory(0, SingleBand.StandardBytes.Length - 2);

        try
        {
            await terminal.FeedChunkAsync(prefix, TestContext.Current.CancellationToken);
            await filter.Entered.Task.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);

            TestSeq.AreEqual(prefix.ToArray(), terminal.PresentationBytes);
            Assert.IsEmpty(terminal.Observe().Placements);

            filter.Release.TrySetResult();
            await terminal.FeedChunkAsync(
                SingleBand.StandardBytes.AsMemory(SingleBand.StandardBytes.Length - 2),
                TestContext.Current.CancellationToken);
            await terminal.WaitForAsync(
                snapshot => snapshot.ContainsSixelData(),
                "completed split-write Sixel",
                TestContext.Current.CancellationToken);
        }
        finally
        {
            filter.Release.TrySetResult();
        }
    }

    [TestMethod]
    public async Task PresentationFilter_C1Input_OwnsNormalizedTokenOutput()
    {
        var filter = new PassThroughPresentationFilter(cloneDcsTokens: true);
        await using var terminal = SixelTestTerminal.Create(presentationFilter: filter);

        await terminal.FeedAsync(
            SingleBand.C1Bytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "filtered C1-framed Sixel",
            TestContext.Current.CancellationToken);

        TestSeq.AreEqual(SingleBand.StandardBytes, terminal.PresentationBytes);
        Assert.AreEqual(1, filter.DcsTokenCount);
        Assert.HasCount(1, terminal.Observe().Placements);
    }

    [TestMethod]
    public async Task PresentationFilter_UnknownDcs_RemainsObservableWithoutSixelState()
    {
        var filter = new PassThroughPresentationFilter(cloneDcsTokens: true);
        await using var terminal = SixelTestTerminal.Create(presentationFilter: filter);
        var bytes = new byte[]
        {
            0x1b, (byte)'P', (byte)'1', (byte)'+', (byte)'r', 0xff,
            0x1b, (byte)'\\', (byte)'X',
        };

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "text following filtered unknown DCS",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(1, filter.DcsTokenCount);
        Assert.IsEmpty(terminal.Observe().Placements);
        TestSeq.AreEqual(bytes, terminal.PresentationBytes);
    }

    [TestMethod]
    public async Task ImpactAwarePresentation_UnknownNonAsciiDcs_PreservesBytes()
    {
        await using var terminal = SixelTestTerminal.Create(impactAware: true);
        var bytes = new byte[]
        {
            0x1b, (byte)'P', (byte)'1', (byte)'+', (byte)'r', 0xc3, 0xa9,
            0x1b, (byte)'\\', (byte)'X',
        };

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "text following impact-aware unknown DCS",
            TestContext.Current.CancellationToken);

        Assert.IsEmpty(terminal.Observe().Placements);
        TestSeq.AreEqual(bytes, terminal.PresentationBytes);
    }

    [TestMethod]
    public async Task ImpactAwarePresentation_ReceivesStructuredDcsOnce()
    {
        await using var terminal = SixelTestTerminal.Create(impactAware: true);

        await terminal.FeedAsync(
            SingleBand.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "impact-aware Sixel",
            TestContext.Current.CancellationToken);

        TestSeq.AreEqual(SingleBand.StandardBytes, terminal.PresentationBytes);
        Assert.HasCount(1, terminal.Observe().Placements);
    }

    [TestMethod]
    public async Task HeadlessOutput_RawDcsUpdatesModelWithoutNativePresentation()
    {
        await using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities
        {
            SupportsSixel = true,
            CellPixelWidth = 1,
            CellPixelHeight = 6,
        });
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(20, 10)
            .Build();

        workload.Write(SingleBand.StandardBytes);

        var started = TimeProvider.System.GetTimestamp();
        while (TimeProvider.System.GetElapsedTime(started) < TimeSpan.FromSeconds(2))
        {
            using var snapshot = terminal.CreateSnapshot();
            if (snapshot.ContainsSixelData())
            {
                return;
            }
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Fail("Headless terminal did not observe the raw Sixel DCS.");
    }

    [TestMethod]
    public async Task PreTokenizedOutput_BypassesRawFramingAndDispatchesOnce()
    {
        using var metrics = new Hex1bMetrics();
        var dispatches = ListenForDispatches(metrics, out var listener);
        using (listener)
        {
            await using var terminal = SixelTestTerminal.Create(metrics: metrics);

            await terminal.FeedPreTokenizedAsync(
                SingleBand.StandardBytes,
                [new DcsToken(Encoding.ASCII.GetString(SingleBand.Payload))],
                TestContext.Current.CancellationToken);
            await terminal.WaitForAsync(
                snapshot => snapshot.ContainsSixelData(),
                "pre-tokenized Sixel",
                TestContext.Current.CancellationToken);

            Assert.HasCount(1, terminal.Observe().Placements);
            Assert.AreEqual(1, dispatches.Count(kind => kind == "sixel"));
        }
    }

    [TestMethod]
    public async Task PreTokenizedItem_DuringOpenRawDcs_ContinuesRawFramingOwnership()
    {
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedChunkAsync(
            "\x1bPq"u8.ToArray(),
            TestContext.Current.CancellationToken);
        await terminal.FeedPreTokenizedAsync(
            "@"u8.ToArray(),
            [new TextToken("@")],
            TestContext.Current.CancellationToken);
        await terminal.FeedChunkAsync(
            "\x1b\\"u8.ToArray(),
            TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "DCS spanning raw and pre-tokenized items",
            TestContext.Current.CancellationToken);

        Assert.HasCount(1, terminal.Observe().Placements);
        TestSeq.AreEqual("\x1bPq@\x1b\\"u8.ToArray(), terminal.PresentationBytes);
    }

    [TestMethod]
    public async Task PreTokenizedOutput_OverRetentionLimit_RetainsGeometryOnlyState()
    {
        var graphics = new Hex1bTerminalGraphicsOptions
        {
            MaximumRetainedInputBytesPerImage = 16,
        };
        await using var terminal = SixelTestTerminal.Create(graphics: graphics);
        var payload =
            $"7q{new string('~', graphics.MaximumRetainedInputBytesPerImage + 8)}";
        var bytes = Encoding.ASCII.GetBytes($"\x1bP{payload}\x1b\\");

        await terminal.FeedPreTokenizedAsync(
            bytes,
            [new DcsToken(payload)],
            TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacementCount == 1,
            "retention-limited geometry-only placement",
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(terminal.Terminal.SixelPlacements);
        Assert.IsTrue(placement.IsGeometryOnly);
        Assert.AreEqual(SixelParseOutcome.LimitDowngraded, placement.Image.Outcome);
        Assert.IsTrue(placement.Image.Diagnostics.Any(
            diagnostic => diagnostic.Code == SixelDiagnosticCode.RetainedContentLimitExceeded));
        Assert.AreEqual(
            graphics.MaximumRetainedInputBytesPerImage,
            placement.Image.Payload.Length);
        TestSeq.AreEqual(bytes, terminal.PresentationBytes);
    }

    [TestMethod]
    public async Task PreTokenizedOutput_UsesConfiguredCommandLimit()
    {
        var policy = SixelCompatibilityPolicy.Default with
        {
            MaximumRetainedCommands = 1,
        };
        await using var terminal = SixelTestTerminal.Create(policy: policy);
        const string payload = "7q@A";
        var bytes = Encoding.ASCII.GetBytes($"\x1bP{payload}\x1b\\");

        await terminal.FeedPreTokenizedAsync(
            bytes,
            [new DcsToken(payload)],
            TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacementCount == 1,
            "command-limited pre-tokenized placement",
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(terminal.Terminal.SixelPlacements);
        Assert.IsTrue(placement.IsGeometryOnly);
        Assert.IsTrue(placement.Image.Diagnostics.Any(
            diagnostic => diagnostic.Code == SixelDiagnosticCode.CommandRetentionLimitExceeded));
        TestSeq.AreEqual(bytes, terminal.PresentationBytes);
    }

    [TestMethod]
    public async Task RawOutput_OverRetentionLimit_ForwardsExactlyAndRetainsGeometryOnlyState()
    {
        var policy = SixelCompatibilityPolicy.Default with
        {
            MaximumRetainedDcsBytes = 16,
        };
        await using var terminal = SixelTestTerminal.Create(policy: policy);
        var payload = $"7q{new string('~', policy.MaximumRetainedDcsBytes + 8)}";
        var bytes = Encoding.ASCII.GetBytes($"\x1bP{payload}\x1b\\X");

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X") && terminal.Terminal.SixelPlacementCount == 1,
            "retention-limited raw Sixel and following text",
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(terminal.Terminal.SixelPlacements);
        Assert.IsTrue(placement.IsGeometryOnly);
        Assert.AreEqual(payload.Length - 2, placement.Image.Extents.Logical.Width);
        TestSeq.AreEqual(bytes, terminal.PresentationBytes);
    }

    [TestMethod]
    public async Task RetentionLimitedSequences_WithSharedPrefix_DoNotDeduplicate()
    {
        var policy = SixelCompatibilityPolicy.Default with
        {
            MaximumRetainedDcsBytes = 8,
        };
        await using var terminal = SixelTestTerminal.Create(policy: policy);
        var first = "\x1bP7qAAAAAA~~~~~~~~\x1b\\";
        var second = "\x1b[2;1H\x1bP7qAAAAAA}}}}}}}}\x1b\\Z";

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes(first + second),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("Z"),
            "two retention-limited placements",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(2, terminal.Terminal.SixelPlacementCount);
        Assert.AreEqual(2, terminal.Terminal.TrackedSixelCount);
        Assert.IsTrue(terminal.Terminal.SixelPlacements.All(placement => placement.IsGeometryOnly));
    }

    [TestMethod]
    public async Task DcsMetrics_MixedOutcomes_ContainNoPayloadData()
    {
        using var metrics = new Hex1bMetrics();
        var measurements = new ConcurrentBag<(string Name, long Value, string? Kind)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, activeListener) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter) &&
                instrument.Name.StartsWith("hex1b.terminal.dcs.", StringComparison.Ordinal))
            {
                activeListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            string? kind = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "kind")
                {
                    kind = tag.Value?.ToString();
                }
                Assert.DoesNotContain("q@", tag.Value?.ToString() ?? "");
            }
            measurements.Add((instrument.Name, value, kind));
        });
        listener.Start();

        await using var terminal = SixelTestTerminal.Create(metrics: metrics);
        var oversizedPayload = new string('~', DcsByteStreamParser.DefaultRetentionLimit);
        var bytes = Encoding.ASCII.GetBytes(
            "\x1bPq@\x1b\\" +
            "\x1bP1+r544e\x1b\\" +
            "\x1bP1;\x1b\\" +
            "\x1bPqABC\x18" +
            $"\x1bPq{oversizedPayload}\x1b\\Z");

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("Z"),
            "text after mixed DCS outcomes",
            TestContext.Current.CancellationToken);

        var dispatchKinds = measurements
            .Where(measurement => measurement.Name == "hex1b.terminal.dcs.dispatches")
            .Select(measurement => measurement.Kind)
            .ToArray();
        CollectionAssert.Contains(dispatchKinds, "sixel");
        CollectionAssert.Contains(dispatchKinds, "unsupported");
        CollectionAssert.Contains(dispatchKinds, "malformed");
        CollectionAssert.Contains(dispatchKinds, "cancelled");
        CollectionAssert.Contains(dispatchKinds, "retention_limit");
        Assert.AreEqual(
            1L,
            measurements
                .Where(measurement => measurement.Name == "hex1b.terminal.dcs.cancellations")
                .Sum(measurement => measurement.Value));
        Assert.AreEqual(
            1L,
            measurements
                .Where(measurement => measurement.Name == "hex1b.terminal.dcs.malformed_recoveries")
                .Sum(measurement => measurement.Value));
        Assert.AreEqual(
            1L,
            measurements
                .Where(measurement => measurement.Name == "hex1b.terminal.dcs.retention_limit")
                .Sum(measurement => measurement.Value));
    }

    [TestMethod]
    public async Task SixelMetrics_ReportBoundedOutcomesLifecycleLimitsAndDurations()
    {
        using var metrics = new Hex1bMetrics();
        var counters = new ConcurrentBag<(string Name, string? Action, string? Reason)>();
        var durations = new ConcurrentBag<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, activeListener) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter) &&
                (instrument.Name.StartsWith("hex1b.terminal.sixel.", StringComparison.Ordinal) ||
                 instrument.Name == "hex1b.terminal.raw_passthrough.duration"))
            {
                activeListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            string? action = null;
            string? reason = null;
            foreach (var tag in tags)
            {
                Assert.DoesNotContain("q#1", tag.Value?.ToString() ?? "");
                if (tag.Key is "action" or "outcome")
                    action = tag.Value?.ToString();
                if (tag.Key is "reason" or "limit")
                    reason = tag.Value?.ToString();
            }
            counters.Add((instrument.Name, action, reason));
        });
        listener.SetMeasurementEventCallback<double>((instrument, _, _, _) =>
            durations.Add(instrument.Name));
        listener.Start();

        var policy = SixelCompatibilityPolicy.Default with
        {
            MaximumPlacementsPerScreen = 1,
        };
        await using var terminal = SixelTestTerminal.Create(metrics: metrics, policy: policy);
        var image = "\x1bPq#1;2;100;0;0#1@\x1b\\";
        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes(image + "\x1b[2;1H" + image + "X"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "Sixel lifecycle metrics",
            TestContext.Current.CancellationToken);

        Assert.IsTrue(counters.Any(item =>
            item.Name == "hex1b.terminal.sixel.outcomes" && item.Action == "complete"));
        Assert.IsTrue(counters.Any(item =>
            item.Name == "hex1b.terminal.sixel.resources" && item.Action == "image_allocated"));
        Assert.IsTrue(counters.Any(item =>
            item.Name == "hex1b.terminal.sixel.resources" && item.Action == "image_deduplicated"));
        Assert.IsTrue(counters.Any(item =>
            item.Name == "hex1b.terminal.sixel.resources" &&
            item.Action == "placement_evicted" &&
            item.Reason == "placement_limit"));
        Assert.IsTrue(counters.Any(item =>
            item.Name == "hex1b.terminal.sixel.limit" && item.Reason == "placement_limit"));
        CollectionAssert.Contains(durations.ToArray(), "hex1b.terminal.raw_passthrough.duration");
        CollectionAssert.Contains(durations.ToArray(), "hex1b.terminal.sixel.processing.duration");
    }

    [TestMethod]
    public async Task Disconnect_OpenDcs_ReportsUnterminatedWithoutSixelState()
    {
        using var metrics = new Hex1bMetrics();
        var dispatches = ListenForDispatches(metrics, out var listener);
        using (listener)
        {
            await using var terminal = SixelTestTerminal.Create(metrics: metrics);
            await terminal.FeedChunkAsync(
                "\x1bPqABC"u8.ToArray(),
                TestContext.Current.CancellationToken);

            await terminal.CompleteWorkloadAsync(TestContext.Current.CancellationToken);

            var started = TimeProvider.System.GetTimestamp();
            while (!dispatches.Contains("unterminated") &&
                   TimeProvider.System.GetElapsedTime(started) < TimeSpan.FromSeconds(2))
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }

            CollectionAssert.Contains(dispatches.ToArray(), "unterminated");
            Assert.IsEmpty(terminal.Observe().Placements);
        }
    }

    private static ConcurrentBag<string?> ListenForDispatches(
        Hex1bMetrics metrics,
        out MeterListener listener)
    {
        var dispatches = new ConcurrentBag<string?>();
        listener = new MeterListener();
        listener.InstrumentPublished = (instrument, activeListener) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter) &&
                instrument.Name == "hex1b.terminal.dcs.dispatches")
            {
                activeListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "kind")
                {
                    dispatches.Add(tag.Value?.ToString());
                }
            }
        });
        listener.Start();
        return dispatches;
    }

    private sealed class BlockingWorkloadFilter : IHex1bTerminalWorkloadFilter
    {
        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask OnSessionStartAsync(
            int width,
            int height,
            DateTimeOffset timestamp,
            CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public async ValueTask OnOutputAsync(
            IReadOnlyList<AnsiToken> tokens,
            TimeSpan elapsed,
            CancellationToken ct = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(ct);
        }

        public ValueTask OnFrameCompleteAsync(TimeSpan elapsed, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
        public ValueTask OnInputAsync(
            IReadOnlyList<AnsiToken> tokens,
            TimeSpan elapsed,
            CancellationToken ct = default) =>
            ValueTask.CompletedTask;
        public ValueTask OnResizeAsync(
            int width,
            int height,
            TimeSpan elapsed,
            CancellationToken ct = default) =>
            ValueTask.CompletedTask;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class PassThroughPresentationFilter(bool cloneDcsTokens = false) :
        IHex1bTerminalPresentationFilter
    {
        public int DcsTokenCount { get; private set; }

        public ValueTask OnSessionStartAsync(
            int width,
            int height,
            DateTimeOffset timestamp,
            CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(
            IReadOnlyList<AppliedToken> appliedTokens,
            TimeSpan elapsed,
            CancellationToken ct = default)
        {
            DcsTokenCount += appliedTokens.Count(applied => applied.Token is DcsToken);
            return ValueTask.FromResult<IReadOnlyList<AnsiToken>>(
                appliedTokens
                    .Select(applied =>
                        cloneDcsTokens && applied.Token is DcsToken dcs
                            ? dcs with { }
                            : applied.Token)
                    .ToArray());
        }

        public ValueTask OnInputAsync(
            IReadOnlyList<AnsiToken> tokens,
            TimeSpan elapsed,
            CancellationToken ct = default) =>
            ValueTask.CompletedTask;
        public ValueTask OnResizeAsync(
            int width,
            int height,
            TimeSpan elapsed,
            CancellationToken ct = default) =>
            ValueTask.CompletedTask;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
    }
}
