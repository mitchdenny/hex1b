using System.Text;
using Hex1b.Sixel;
using Hex1b.Tests;

namespace Hex1b.Tests.Sixel;

/// <summary>
/// Coverage for stage #455's <see cref="ConsolePresentationAdapter"/> Sixel
/// capability/protocol-cell-metrics discovery probe: direct declarations,
/// per-source precedence, plausibility rejection, fragmentation/interleaving of
/// the five new wire replies (DA1, CSI 16/14/18 t, OSC 1337) alongside the
/// existing KGP/background probe, and resize invalidation.
/// </summary>
/// <remarks>
/// These tests reuse <see cref="FakeConsoleDriver"/> from
/// <see cref="ConsolePresentationAdapterTests"/> so the byte-level plumbing
/// (queued read chunks, written-query capture, <c>TryGetWindowPixelSize</c>,
/// simulated resize) matches the established KGP/background probe test style
/// exactly.
/// </remarks>
[TestClass]
public class SixelCapabilityDiscoveryTests
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(25);

    // Reply builders --------------------------------------------------------

    private static string Da1Reply(bool declaresSixel) => declaresSixel ? "\x1b[?62;4c" : "\x1b[?62c";

    private static string Csi16Reply(double heightPx, double widthPx) => $"\x1b[6;{Format(heightPx)};{Format(widthPx)}t";

    private static string Csi14Reply(double heightPx, double widthPx) => $"\x1b[4;{Format(heightPx)};{Format(widthPx)}t";

    private static string Csi18Reply(double rows, double cols) => $"\x1b[8;{Format(rows)};{Format(cols)}t";

    private static string Osc1337Reply(double heightPoints, double widthPoints, double? scale = null) =>
        scale is { } s
            ? $"\x1b]1337;ReportCellSize={Format(heightPoints)};{Format(widthPoints)};{Format(s)}\x1b\\"
            : $"\x1b]1337;ReportCellSize={Format(heightPoints)};{Format(widthPoints)}\x1b\\";

    private static string Format(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    // Direct declaration pre-empts probing -----------------------------------

    [TestMethod]
    public async Task WithSixelSupport_DeclaredDirectly_SkipsProbingEntirely()
    {
        using var driver = new FakeConsoleDriver(); // No replies queued at all.
        var declared = new SixelCellMetrics(11, 22, SixelCellMetricsSource.Direct, SixelCellMetricsReliability.Authoritative);
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);
        adapter.WithSixelSupport(SixelPresentationSupport.Native, declared);

        // Capabilities reflect the declaration immediately, before the probe runs.
        Assert.AreEqual(SixelPresentationSupport.Native, adapter.Capabilities.SixelSupport);
        Assert.IsTrue(adapter.Capabilities.SupportsSixel);
        Assert.AreEqual(declared, adapter.Capabilities.SixelCellMetrics);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        // No Sixel probe queries were ever written to the driver.
        Assert.DoesNotContain("\x1b[c", driver.WrittenText);
        Assert.DoesNotContain("\x1b[16t", driver.WrittenText);
        Assert.DoesNotContain("\x1b[14t", driver.WrittenText);
        Assert.DoesNotContain("\x1b[18t", driver.WrittenText);
        Assert.DoesNotContain("ReportCellSize", driver.WrittenText);

        // Diagnostics stay at "not probed" -- discovery never ran.
        Assert.AreEqual(SixelCapabilityProbeDiagnostics.NotProbed, adapter.SixelProbeDiagnostics);
        Assert.AreEqual(declared, adapter.Capabilities.SixelCellMetrics);
    }

    [TestMethod]
    public async Task WithSixelSupport_DeclaredNone_IsConfirmedUnsupportedNotUnknown()
    {
        using var driver = new FakeConsoleDriver();
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);
        adapter.WithSixelSupport(SixelPresentationSupport.None);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        Assert.AreEqual(SixelPresentationSupport.None, adapter.Capabilities.SixelSupport);
        Assert.IsFalse(adapter.Capabilities.SupportsSixel);
        // A direct "none" declaration still pre-empted probing (no DA1 sent),
        // which is what makes it distinct from an unanswered/unknown probe.
        Assert.DoesNotContain("\x1b[c", driver.WrittenText);
    }

    // Single-source acceptance -------------------------------------------------

    [TestMethod]
    public async Task EnterRawModeAsync_Csi16Reply_SelectsAuthoritativeCsi16Metrics()
    {
        using var driver = new FakeConsoleDriver(Csi16Reply(40, 20));
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        var metrics = adapter.Capabilities.SixelCellMetrics;
        Assert.IsNotNull(metrics);
        Assert.AreEqual(20, metrics.Value.Width);
        Assert.AreEqual(40, metrics.Value.Height);
        Assert.AreEqual(SixelCellMetricsSource.Csi16, metrics.Value.Source);
        Assert.IsTrue(metrics.Value.IsAuthoritative);
        Assert.AreEqual(metrics, adapter.SixelProbeDiagnostics.SelectedMetrics);
        Assert.IsFalse(adapter.SixelProbeDiagnostics.MetricsDisagreement);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_Osc1337ReplyWithoutScale_ScaleDefaultsToOne()
    {
        using var driver = new FakeConsoleDriver(Osc1337Reply(20, 10));
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        var metrics = adapter.Capabilities.SixelCellMetrics;
        Assert.IsNotNull(metrics);
        Assert.AreEqual(10, metrics.Value.Width);
        Assert.AreEqual(20, metrics.Value.Height);
        Assert.AreEqual(SixelCellMetricsSource.Osc1337, metrics.Value.Source);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_Osc1337ReplyWithScale_MultipliesPointsByScale()
    {
        using var driver = new FakeConsoleDriver(Osc1337Reply(20, 10, scale: 2));
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        var metrics = adapter.Capabilities.SixelCellMetrics;
        Assert.IsNotNull(metrics);
        Assert.AreEqual(20, metrics.Value.Width);
        Assert.AreEqual(40, metrics.Value.Height);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_Csi14AndCsi18Reply_DerivesFractionalCellMetrics()
    {
        // 1000px / 24 rows = 41.666...px per row; 800px / 80 cols = 10px per column.
        using var driver = new FakeConsoleDriver(Csi14Reply(1000, 800) + Csi18Reply(24, 80));
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        var metrics = adapter.Capabilities.SixelCellMetrics;
        Assert.IsNotNull(metrics);
        Assert.AreEqual(SixelCellMetricsSource.Derived, metrics.Value.Source);
        Assert.AreEqual(SixelCellMetricsReliability.Derived, metrics.Value.Reliability);
        Assert.AreEqual(10d, metrics.Value.Width, 1e-9);
        Assert.AreEqual(1000d / 24d, metrics.Value.Height, 1e-9);
        Assert.IsFalse(double.IsInteger(metrics.Value.Height), "Derivation must preserve fractional results, not round/truncate.");
    }

    [TestMethod]
    public async Task EnterRawModeAsync_OnlyTiocgwinszAvailable_DerivesFromLocalPixelFields()
    {
        using var driver = new FakeConsoleDriver
        {
            WindowPixelSize = (Width: 800, Height: 480), // FakeConsoleDriver.Width/Height are 80x24.
        };
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        var metrics = adapter.Capabilities.SixelCellMetrics;
        Assert.IsNotNull(metrics);
        Assert.AreEqual(SixelCellMetricsSource.Derived, metrics.Value.Source);
        Assert.AreEqual(10d, metrics.Value.Width, 1e-9); // 800 / 80
        Assert.AreEqual(20d, metrics.Value.Height, 1e-9); // 480 / 24
        Assert.Contains("Derived from TIOCGWINSZ pixel fields.", string.Join(' ', adapter.SixelProbeDiagnostics.Attempts.Select(a => a.Detail)));
    }

    // Precedence and disagreement ----------------------------------------------

    [TestMethod]
    public async Task EnterRawModeAsync_Csi16ConflictsWithOsc1337_Csi16WinsAndDisagreementIsRecorded()
    {
        using var driver = new FakeConsoleDriver(Csi16Reply(40, 20) + Osc1337Reply(999, 999));
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        var metrics = adapter.Capabilities.SixelCellMetrics;
        Assert.IsNotNull(metrics);
        Assert.AreEqual(SixelCellMetricsSource.Csi16, metrics.Value.Source);
        Assert.AreEqual(20, metrics.Value.Width);
        Assert.AreEqual(40, metrics.Value.Height);

        Assert.IsTrue(adapter.SixelProbeDiagnostics.MetricsDisagreement);
        Assert.IsNotNull(adapter.SixelProbeDiagnostics.DisagreementDetail);
        StringAssert.Contains(adapter.SixelProbeDiagnostics.DisagreementDetail, "Csi16");
    }

    [TestMethod]
    public async Task EnterRawModeAsync_Csi16ConflictsWithPhysicalGeometry_Csi16StillWins()
    {
        // CSI 16 t disagrees with the CSI14/CSI18-derived value; per the issue's
        // precedence, CSI 16 t wins even over "physical" window geometry.
        using var driver = new FakeConsoleDriver(Csi16Reply(40, 20) + Csi14Reply(240, 240) + Csi18Reply(24, 80));
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        var metrics = adapter.Capabilities.SixelCellMetrics;
        Assert.IsNotNull(metrics);
        Assert.AreEqual(SixelCellMetricsSource.Csi16, metrics.Value.Source);
        Assert.IsTrue(adapter.SixelProbeDiagnostics.MetricsDisagreement);
    }

    // Plausibility rejection ----------------------------------------------------

    // Note: negative dimensions cannot arrive over the wire as a recognized CSI
    // 16 t reply at all -- the parser's digit/semicolon character class (see
    // TryConsumeWindowOperationResponse) deliberately stops scanning the moment
    // it meets a byte that could not plausibly belong to a numeric window-op
    // reply (to avoid ever misconsuming ordinary keyboard input that merely
    // starts like one, e.g. "CSI 3 ~" for Delete). A literal '-' falls outside
    // that class, so such a reply is treated as "not a match" (eventually
    // TimedOut) rather than "matched but Rejected". Negative-value rejection is
    // covered separately below via OSC 1337, whose payload has no such
    // pre-filter.
    [TestMethod]
    [DataRow(0d, 20d, DisplayName = "Zero height")]
    [DataRow(40d, 0d, DisplayName = "Zero width")]
    [DataRow(40d, 999_999_999d, DisplayName = "Overflow-scale width")]
    public async Task EnterRawModeAsync_ImplausibleCsi16Dimensions_RejectedAndFallsBackToNextTier(double height, double width)
    {
        using var driver = new FakeConsoleDriver(Csi16Reply(height, width) + Osc1337Reply(20, 10));
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        var metrics = adapter.Capabilities.SixelCellMetrics;
        Assert.IsNotNull(metrics);
        // Csi16 was rejected as implausible, so Osc1337 (next tier) was selected instead.
        Assert.AreEqual(SixelCellMetricsSource.Osc1337, metrics.Value.Source);

        var csi16Attempt = adapter.SixelProbeDiagnostics.Attempts.Single(a => a.Source == SixelCellMetricsSource.Csi16);
        Assert.AreEqual(SixelMetricsProbeOutcome.Rejected, csi16Attempt.Outcome);
        Assert.IsNotNull(csi16Attempt.Detail);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_Osc1337NegativeDimension_RejectedAsImplausible()
    {
        // OSC 1337's payload is captured verbatim up to the ST terminator with
        // no digit/semicolon pre-filter, so (unlike CSI 16/14/18 t) a literal
        // '-' parses fine as a double and reaches the plausibility check.
        using var driver = new FakeConsoleDriver("\x1b]1337;ReportCellSize=-5;20\x1b\\");
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        Assert.IsNull(adapter.Capabilities.SixelCellMetrics);
        var attempt = adapter.SixelProbeDiagnostics.Attempts.Single(a => a.Source == SixelCellMetricsSource.Osc1337);
        Assert.AreEqual(SixelMetricsProbeOutcome.Rejected, attempt.Outcome);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_AllSourcesImplausibleOrAbsent_NoMetricsSelected()
    {
        using var driver = new FakeConsoleDriver(Csi16Reply(0, 0));
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        Assert.IsNull(adapter.Capabilities.SixelCellMetrics);
        Assert.IsNull(adapter.SixelProbeDiagnostics.SelectedMetrics);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_MalformedCsi16NumericParameters_TreatedAsMalformedNotRejected()
    {
        // An empty parameter field ("6;;20") stays entirely within the
        // digit/semicolon character class the parser requires to recognize a
        // window-op-shaped reply, but fails double-parsing once split -- this
        // is the scenario the parser can actually classify as Malformed
        // (distinct from a value that parses fine but fails plausibility).
        using var driver = new FakeConsoleDriver("\x1b[6;;20t");
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        var attempt = adapter.SixelProbeDiagnostics.Attempts.Single(a => a.Source == SixelCellMetricsSource.Csi16);
        Assert.AreEqual(SixelMetricsProbeOutcome.Malformed, attempt.Outcome);
        Assert.IsNull(adapter.Capabilities.SixelCellMetrics);
    }

    // DA1 / Sixel-support tri-state: accepted / confirmed-unsupported / unknown ---

    [TestMethod]
    public async Task EnterRawModeAsync_Da1DeclaresSixel_SetsNativeSupportAndTrueDeclaration()
    {
        using var driver = new FakeConsoleDriver(Da1Reply(declaresSixel: true));
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        Assert.IsTrue(adapter.Capabilities.SupportsSixel);
        Assert.AreEqual(SixelPresentationSupport.Native, adapter.Capabilities.SixelSupport);
        Assert.AreEqual(true, adapter.SixelProbeDiagnostics.Da1DeclaresSixel);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_Da1RepliesWithoutSixelParameter_IsConfirmedUnsupported()
    {
        using var driver = new FakeConsoleDriver(Da1Reply(declaresSixel: false));
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        Assert.IsFalse(adapter.Capabilities.SupportsSixel);
        Assert.AreEqual(SixelPresentationSupport.None, adapter.Capabilities.SixelSupport);
        // Confirmed unsupported: DA1 replied, and explicitly did NOT declare param 4.
        Assert.AreEqual(false, adapter.SixelProbeDiagnostics.Da1DeclaresSixel);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_Da1NeverReplies_IsUnknownNotConfirmedUnsupported()
    {
        using var driver = new FakeConsoleDriver(); // No DA1 reply queued.
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        // Conservative default: no claimed support...
        Assert.IsFalse(adapter.Capabilities.SupportsSixel);
        Assert.AreEqual(SixelPresentationSupport.None, adapter.Capabilities.SixelSupport);
        // ...but the diagnostics explicitly distinguish "never answered" (null)
        // from "answered and declined" (false).
        Assert.IsNull(adapter.SixelProbeDiagnostics.Da1DeclaresSixel);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_Da1MalformedReply_IsUnknownNotConfirmedUnsupported()
    {
        using var driver = new FakeConsoleDriver("\x1b[?c"); // No parameters at all.
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        Assert.IsFalse(adapter.Capabilities.SupportsSixel);
        Assert.IsNull(adapter.SixelProbeDiagnostics.Da1DeclaresSixel);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_BareCsiCQuery_IsNeverMisreadAsADa1Reply()
    {
        // A bare "CSI c" (no '?') is what a *workload* sends when asking the
        // terminal for its own identity; it must never be misconsumed as if it
        // were a DA1 *reply* to our probe.
        using var driver = new FakeConsoleDriver("\x1bc" + Da1Reply(declaresSixel: true));
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        Assert.AreEqual(true, adapter.SixelProbeDiagnostics.Da1DeclaresSixel);
    }

    // Fragmentation: every supported reply, split at every byte boundary --------

    [TestMethod]
    public async Task EnterRawModeAsync_Da1Reply_FragmentedAtEveryByteBoundary_StillParsesCorrectly()
    {
        await AssertFragmentedAtEveryBoundaryAsync(
            Encoding.ASCII.GetBytes(Da1Reply(declaresSixel: true)),
            adapter => Assert.AreEqual(true, adapter.SixelProbeDiagnostics.Da1DeclaresSixel));
    }

    [TestMethod]
    public async Task EnterRawModeAsync_Csi16Reply_FragmentedAtEveryByteBoundary_StillParsesCorrectly()
    {
        await AssertFragmentedAtEveryBoundaryAsync(
            Encoding.ASCII.GetBytes(Csi16Reply(40, 20)),
            adapter =>
            {
                var metrics = adapter.Capabilities.SixelCellMetrics;
                Assert.IsNotNull(metrics);
                Assert.AreEqual(SixelCellMetricsSource.Csi16, metrics.Value.Source);
                Assert.AreEqual(20, metrics.Value.Width);
                Assert.AreEqual(40, metrics.Value.Height);
            });
    }

    [TestMethod]
    public async Task EnterRawModeAsync_Csi14And18Replies_FragmentedAtEveryByteBoundary_StillParsesCorrectly()
    {
        await AssertFragmentedAtEveryBoundaryAsync(
            Encoding.ASCII.GetBytes(Csi14Reply(960, 800) + Csi18Reply(24, 80)),
            adapter =>
            {
                var metrics = adapter.Capabilities.SixelCellMetrics;
                Assert.IsNotNull(metrics);
                Assert.AreEqual(SixelCellMetricsSource.Derived, metrics.Value.Source);
                Assert.AreEqual(10d, metrics.Value.Width, 1e-9);
                Assert.AreEqual(40d, metrics.Value.Height, 1e-9);
            });
    }

    [TestMethod]
    public async Task EnterRawModeAsync_Osc1337Reply_FragmentedAtEveryByteBoundary_StillParsesCorrectly()
    {
        await AssertFragmentedAtEveryBoundaryAsync(
            Encoding.ASCII.GetBytes(Osc1337Reply(20, 10, scale: 2)),
            adapter =>
            {
                var metrics = adapter.Capabilities.SixelCellMetrics;
                Assert.IsNotNull(metrics);
                Assert.AreEqual(SixelCellMetricsSource.Osc1337, metrics.Value.Source);
                Assert.AreEqual(20, metrics.Value.Width);
                Assert.AreEqual(40, metrics.Value.Height);
            });
    }

    /// <summary>
    /// Splits <paramref name="reply"/> at every possible byte boundary (including
    /// the degenerate "arrives whole" and "one byte, then the rest" cases) across
    /// two separate driver reads, asserting <paramref name="assertOutcome"/> holds
    /// for every split point. This is what "fragment every supported response at
    /// every byte boundary" means made concrete and exhaustive rather than a
    /// handful of hand-picked split points.
    /// </summary>
    private static async Task AssertFragmentedAtEveryBoundaryAsync(
        byte[] reply,
        Action<ConsolePresentationAdapter> assertOutcome)
    {
        for (var split = 0; split <= reply.Length; split++)
        {
            var first = reply[..split];
            var second = reply[split..];
            using var driver = first.Length == 0
                ? new FakeConsoleDriver(second)
                : new FakeConsoleDriver(Encoding.ASCII.GetString(first), Encoding.ASCII.GetString(second));
            await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

            await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

            try
            {
                assertOutcome(adapter);
            }
            catch (Exception ex)
            {
                throw new AssertFailedException(
                    $"Assertion failed for split point {split} of {reply.Length} (\"{Encoding.ASCII.GetString(first)}\" | \"{Encoding.ASCII.GetString(second)}\").",
                    ex);
            }
        }
    }

    // Interleaving with KGP/background probes and keyboard input -----------------

    [TestMethod]
    public async Task EnterRawModeAsync_AllRepliesInterleavedWithKeyboardInput_ResolvesEverySourceAndPreservesInput()
    {
        var kgp = "\x1b_Gi=2147483647;OK\x1b\\";
        var bg = "\x1b]11;rgb:1200/3400/5600\x1b\\";
        var da1 = Da1Reply(declaresSixel: true);
        var csi16 = Csi16Reply(40, 20);
        var csi14 = Csi14Reply(960, 800);
        var csi18 = Csi18Reply(24, 80);
        var osc1337 = Osc1337Reply(20, 10);
        const string keyboard = "Xy\x1b[Az";

        // Deliberately scrambled arrival order, not the order queries were sent in.
        var combined = string.Concat(osc1337, keyboard[..1], da1, csi18, kgp, keyboard[1..2], csi16, bg, keyboard[2..], csi14);

        // Split into small fixed-size chunks so several of the sequences above
        // are fragmented mid-flight simultaneously, not just one at a time.
        using var driver = FakeConsoleDriver.FromByteChunks(Encoding.ASCII, ChunkEvery(Encoding.ASCII.GetBytes(combined), 3));
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: TimeSpan.FromMilliseconds(200));

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        Assert.IsTrue(adapter.Capabilities.SupportsKgp);
        Assert.AreEqual(0x123456, adapter.Capabilities.DefaultBackground, "Background probe result should still resolve while sixel probes are interleaved.");
        Assert.IsTrue(adapter.Capabilities.SupportsSixel);
        Assert.AreEqual(SixelPresentationSupport.Native, adapter.Capabilities.SixelSupport);

        var metrics = adapter.Capabilities.SixelCellMetrics;
        Assert.IsNotNull(metrics);
        Assert.AreEqual(SixelCellMetricsSource.Csi16, metrics.Value.Source); // Highest precedence among csi16/osc1337/derived.
        Assert.IsTrue(adapter.SixelProbeDiagnostics.MetricsDisagreement);

        var input = await adapter.ReadInputAsync(TestContext.Current.CancellationToken);
        Assert.AreEqual(keyboard, Encoding.ASCII.GetString(input.Span));
    }

    private static byte[][] ChunkEvery(byte[] bytes, int size)
    {
        var chunks = new List<byte[]>();
        for (var i = 0; i < bytes.Length; i += size)
        {
            chunks.Add(bytes[i..Math.Min(i + size, bytes.Length)]);
        }
        return [.. chunks];
    }

    // Timeout / malformed / cancellation preserve user input ----------------------

    [TestMethod]
    public async Task EnterRawModeAsync_ProbeTimesOutWithNoReplies_PreservesKeyboardInputThatArrivedMeanwhile()
    {
        using var driver = new FakeConsoleDriver("hello");
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        Assert.IsFalse(adapter.Capabilities.SupportsSixel);
        Assert.IsNull(adapter.SixelProbeDiagnostics.Da1DeclaresSixel);
        var attempts = adapter.SixelProbeDiagnostics.Attempts;
        Assert.IsTrue(attempts.All(a => a.Outcome == SixelMetricsProbeOutcome.TimedOut));

        var input = await adapter.ReadInputAsync(TestContext.Current.CancellationToken);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(input.Span));
    }

    [TestMethod]
    public async Task EnterRawModeAsync_MalformedRepliesAmongValidInput_PreservesUnrelatedInput()
    {
        using var driver = new FakeConsoleDriver("\x1b[6;abc;def t leading garbage " + Da1Reply(declaresSixel: false) + "trailing keys");
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);

        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        Assert.AreEqual(false, adapter.SixelProbeDiagnostics.Da1DeclaresSixel);

        var input = await adapter.ReadInputAsync(TestContext.Current.CancellationToken);
        var text = Encoding.ASCII.GetString(input.Span);
        StringAssert.Contains(text, "leading garbage");
        StringAssert.Contains(text, "trailing keys");
    }

    [TestMethod]
    public async Task EnterRawModeAsync_ExternallyCancelledMidProbe_StillPreservesBytesAlreadyRead()
    {
        // Only one chunk is ever queued; a second read blocks until the driver
        // observes cancellation. Both the fake driver here and the real
        // Unix/Windows drivers (see ReadAsync) treat a cancelled read as "no
        // more data" (return 0) rather than throwing, so external
        // cancellation completes EnterRawModeAsync gracefully -- exactly like
        // the internal KGP/probe timeout path -- without ever blocking
        // indefinitely or losing bytes already read.
        using var driver = new FakeConsoleDriver("partial-input");
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: TimeSpan.FromSeconds(30));
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await adapter.EnterRawModeAsync(cts.Token);

        // Even under external cancellation, the bytes already read before the
        // cancellation was observed are not lost.
        var input = await adapter.ReadInputAsync(TestContext.Current.CancellationToken);
        Assert.AreEqual("partial-input", Encoding.ASCII.GetString(input.Span));

        // Partial diagnostics were still computed (all unanswered sources are
        // "timed out", never silently dropped).
        Assert.IsTrue(adapter.SixelProbeDiagnostics.Attempts.Count > 0);
        Assert.IsTrue(adapter.SixelProbeDiagnostics.Attempts.All(a => a.Outcome == SixelMetricsProbeOutcome.TimedOut));
    }

    // Resize invalidation ----------------------------------------------------------

    [TestMethod]
    public async Task Resize_InvalidatesDerivedMetrics_ButNotAuthoritativeCsi16Metrics()
    {
        using var driver = new FakeConsoleDriver(Csi16Reply(40, 20));
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);
        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);
        Assert.AreEqual(SixelCellMetricsSource.Csi16, adapter.Capabilities.SixelCellMetrics!.Value.Source);

        driver.RaiseResized(100, 50);

        Assert.IsNotNull(adapter.Capabilities.SixelCellMetrics);
        Assert.AreEqual(SixelCellMetricsSource.Csi16, adapter.Capabilities.SixelCellMetrics!.Value.Source);
    }

    [TestMethod]
    public async Task Resize_InvalidatesDerivedMetrics_FromCsi14Csi18Derivation()
    {
        using var driver = new FakeConsoleDriver(Csi14Reply(960, 800) + Csi18Reply(24, 80));
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);
        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);
        Assert.IsNotNull(adapter.Capabilities.SixelCellMetrics);
        Assert.AreEqual(SixelCellMetricsSource.Derived, adapter.Capabilities.SixelCellMetrics!.Value.Source);

        driver.RaiseResized(100, 50);

        Assert.IsNull(adapter.Capabilities.SixelCellMetrics);
    }

    [TestMethod]
    public async Task Resize_DoesNotInvalidateADirectlyDeclaredMetric()
    {
        using var driver = new FakeConsoleDriver();
        var declared = new SixelCellMetrics(9, 18, SixelCellMetricsSource.Direct, SixelCellMetricsReliability.Authoritative);
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ProbeTimeout);
        adapter.WithSixelSupport(SixelPresentationSupport.Native, declared);
        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        driver.RaiseResized(100, 50);

        Assert.AreEqual(declared, adapter.Capabilities.SixelCellMetrics);
    }
}
