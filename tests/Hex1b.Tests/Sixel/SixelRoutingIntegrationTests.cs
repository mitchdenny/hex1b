using System.Text;
using Hex1b.Sixel;
using Hex1b.Tokens;

namespace Hex1b.Tests.Sixel;

/// <summary>
/// Terminal-level coverage for stage #458's routing matrix: native byte-exact
/// passthrough, the managed raster sink, KGP translation, the unsupported-
/// presentation policy, and opt-in sanitization, driven end to end through
/// <see cref="Hex1bTerminal"/> with raw Sixel DCS fixtures (never
/// <c>SixelWidget</c>/<c>SixelEncoder</c>).
/// </summary>
[TestClass]
public class SixelRoutingIntegrationTests
{
    // A tiny, hand-authored one-pixel-band Sixel graphic: define color register 1 as
    // red, then paint one sixel column. Small enough to embed inline like the
    // existing SixelRasterIntegrationTests fixtures.
    private const string SmallRedGraphic = "\x1bP0;1q#1;2;100;0;0#1@\x1b\\";

    // DECGRA declares a pixel extent far beyond any reasonable buffer, forcing the
    // rasterizer to refuse pixel allocation while still retaining bounded
    // geometry/anchor information (SixelRasterStatus.GeometryOnly). Reused verbatim
    // from SixelRasterIntegrationTests.GeometryOnlyGraphic_StillOccupiesCellsWithoutPixels.
    private const string GeometryOnlyGraphic = "\x1bP0;1q\"1;1;999999999;999999999#1@\x1b\\";

    // DECGRA with five parameters is structurally invalid (accepts at most Pan;Pad;Ph;Pv),
    // which SixelParser marks malformed without aborting the DCS framing itself: the
    // sequence still terminates normally (DcsSequenceStatus.Complete) with
    // SixelParseOutcome.Malformed, exactly the "malformed but still framed" case
    // sanitization/native-forwarding invariants need to distinguish from
    // cancelled/unterminated/retention-limit-exceeded framing.
    private const string MalformedGraphic = "\x1bP0;1q\"1;1;1;1;1#1;2;100;0;0#1@\x1b\\";

    // A Sixel-shaped DCS introducer ("...q" final byte) whose payload is cut off by
    // CAN (0x18) rather than a normal ST terminator — DcsSequenceStatus.Cancelled.
    // Used unterminated (below) by simply omitting any terminator at all instead.
    private const string CancelledSixelPrefix = "\x1bP0;1q#1;2;100;0;0";

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    // Native passthrough invariants ------------------------------------------------

    [TestMethod]
    public async Task NativeRoute_ForwardsBytesExactlyAtEverySplitBoundary()
    {
        var payload = Bytes(SmallRedGraphic);
        for (var split = 0; split < payload.Length; split++)
        {
            await using var terminal = SixelRoutingTestTerminal.Create(sixelSupport: SixelPresentationSupport.Native);
            var chunkSizes = split == 0 ? new[] { payload.Length } : new[] { split, payload.Length - split };
            await terminal.FeedAsync(payload, chunkSizes, TestContext.Current.CancellationToken);
            await terminal.WaitForPresentationLengthAsync(payload.Length, TestContext.Current.CancellationToken);

            CollectionAssert.AreEqual(payload, terminal.PresentationBytes, $"split boundary {split}");
        }
    }

    [TestMethod]
    public async Task NativeRoute_MalformedSixel_ForwardsBytesUnchangedAndUnmutated()
    {
        // Internal parser/raster failure must never mutate or delay default native
        // output: even though the payload is malformed at the Sixel semantic level,
        // native passthrough (sanitization disabled) forwards it byte-for-byte.
        var payload = Bytes("before" + MalformedGraphic + "after");
        await using var terminal = SixelRoutingTestTerminal.Create(sixelSupport: SixelPresentationSupport.Native);

        await terminal.FeedAsync(payload, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForPresentationLengthAsync(payload.Length, TestContext.Current.CancellationToken);

        CollectionAssert.AreEqual(payload, terminal.PresentationBytes);
    }

    [TestMethod]
    public async Task NativeRoute_GeometryOnlyDowngrade_IsObservableViaDiagnosticsWithoutAffectingNativeBytes()
    {
        var payload = Bytes(GeometryOnlyGraphic);
        await using var terminal = SixelRoutingTestTerminal.Create(sixelSupport: SixelPresentationSupport.Native);

        await terminal.FeedAsync(payload, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForPresentationLengthAsync(payload.Length, TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Diagnostics.Any(d => d.Kind == SixelRasterRouteDiagnosticKind.GeometryOnlyDowngrade),
            "geometry-only diagnostic",
            TestContext.Current.CancellationToken);

        CollectionAssert.AreEqual(payload, terminal.PresentationBytes);
        Assert.IsTrue(terminal.Diagnostics.Any(d => d.Kind == SixelRasterRouteDiagnosticKind.GeometryOnlyDowngrade));
    }

    // Managed raster sink -----------------------------------------------------------

    [TestMethod]
    public async Task ManagedSink_ReceivesOrderedContentDefinedThenPlacementUpdated()
    {
        await using var terminal = SixelRoutingTestTerminal.Create(asManagedSink: true);

        await terminal.FeedAsync(Bytes(SmallRedGraphic), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForEventCountAsync(2, TestContext.Current.CancellationToken);

        var events = terminal.RasterEvents;
        var defined = TestSeq.IsType<SixelRasterContentDefined>(events[0]);
        var updated = TestSeq.IsType<SixelRasterPlacementUpdated>(events[1]);
        Assert.IsTrue(updated.IsNewPlacement);
        CollectionAssert.AreEqual(defined.Image.ContentHash, updated.Placement.Image.ContentHash);
    }

    [TestMethod]
    public async Task ManagedSink_IdenticalContentSecondPlacement_DeduplicatesContentDefinition()
    {
        await using var terminal = SixelRoutingTestTerminal.Create(asManagedSink: true, width: 40, height: 20);

        await terminal.FeedAsync(Bytes(SmallRedGraphic), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForEventCountAsync(2, TestContext.Current.CancellationToken);

        // Same content, painted at a different anchor: a fresh placement is
        // announced, but content is only ever defined once per session.
        await terminal.FeedAsync(
            Bytes("\x1b[2;1H" + SmallRedGraphic),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForEventCountAsync(3, TestContext.Current.CancellationToken);

        var events = terminal.RasterEvents;
        Assert.AreEqual(1, events.OfType<SixelRasterContentDefined>().Count());
        Assert.AreEqual(2, events.OfType<SixelRasterPlacementUpdated>().Count(e => e.IsNewPlacement));
    }

    [TestMethod]
    public async Task ManagedSink_ScrollOffScreen_ReleasesPlacementAndContent()
    {
        await using var terminal = SixelRoutingTestTerminal.Create(asManagedSink: true, width: 20, height: 3);

        await terminal.FeedAsync(Bytes(SmallRedGraphic), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForEventCountAsync(2, TestContext.Current.CancellationToken);

        // Scroll the single-line viewport with no scrollback so the placement is no
        // longer reachable from any live screen state.
        var newlines = string.Concat(Enumerable.Repeat("\n", 10));
        await terminal.FeedAsync(Bytes(newlines), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForEventCountAsync(4, TestContext.Current.CancellationToken);

        var events = terminal.RasterEvents;
        Assert.IsTrue(events.Any(e => e is SixelRasterPlacementReleased));
        Assert.IsTrue(events.Any(e => e is SixelRasterContentReleased));
    }

    [TestMethod]
    public async Task ManagedSink_NativeSupport_AlsoForwardsRawBytesAlongsideEvents()
    {
        // Positive control for the raw-wire gate: a managed sink whose presentation
        // also declares Native Sixel support is the documented dual-delivery case
        // (see ISixelRasterPresentationSink's remarks) — it must keep receiving raw
        // bytes exactly as before, alongside the structured event stream.
        var payload = Bytes(SmallRedGraphic);
        await using var terminal = SixelRoutingTestTerminal.Create(
            asManagedSink: true,
            sixelSupport: SixelPresentationSupport.Native);

        await terminal.FeedAsync(payload, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForEventCountAsync(2, TestContext.Current.CancellationToken);
        await terminal.WaitForPresentationLengthAsync(payload.Length, TestContext.Current.CancellationToken);

        CollectionAssert.AreEqual(payload, terminal.PresentationBytes);
        Assert.HasCount(2, terminal.RasterEvents);
    }

    [TestMethod]
    public async Task ManagedSink_Headless_NeverReceivesRawSixelWireBytes()
    {
        // Regression for the confirmed defect: a managed sink whose presentation
        // declares Headless (non-Native) support must receive only the structured
        // raster event stream, never raw Sixel DCS bytes, regardless of the fact that
        // ComputeRoute selects ManagedRasterSink for any ISixelRasterPresentationSink.
        await using var terminal = SixelRoutingTestTerminal.Create(
            asManagedSink: true,
            sixelSupport: SixelPresentationSupport.Headless);

        await terminal.FeedAsync(
            Bytes("before" + SmallRedGraphic + "after"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForEventCountAsync(2, TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.PresentationText.Contains("after"),
            "text after the graphic",
            TestContext.Current.CancellationToken);

        var text = terminal.PresentationText;
        Assert.Contains("before", text);
        Assert.Contains("after", text);
        Assert.DoesNotContain("\x1bP", text);
        Assert.IsTrue(terminal.RasterEvents.OfType<SixelRasterContentDefined>().Any());
        Assert.IsTrue(terminal.RasterEvents.OfType<SixelRasterPlacementUpdated>().Any());
    }

    [TestMethod]
    public async Task ManagedSink_Translated_NeverReceivesRawSixelWireBytesAtAnySplitBoundary()
    {
        // Same regression as the Headless case, but for Translated support and
        // exercised at every possible chunk split boundary — the gate must hold
        // regardless of arbitrary workload chunking, not just whole-payload feeds.
        var payload = Bytes("before" + SmallRedGraphic + "after");
        for (var split = 1; split < payload.Length; split++)
        {
            await using var terminal = SixelRoutingTestTerminal.Create(
                asManagedSink: true,
                sixelSupport: SixelPresentationSupport.Translated);

            await terminal.FeedAsync(
                payload,
                chunkSizes: [split, payload.Length - split],
                cancellationToken: TestContext.Current.CancellationToken);
            await terminal.WaitForAsync(
                _ => terminal.PresentationText.Contains("after"),
                "text after the graphic",
                TestContext.Current.CancellationToken);

            Assert.DoesNotContain("\x1bP", terminal.PresentationText, $"split boundary {split}");
        }
    }

    // KGP translation ------------------------------------------------------------

    [TestMethod]
    public async Task KgpTranslatedRoute_TransmitsImageThenPlacementWithReservedIdBit()
    {
        await using var terminal = SixelRoutingTestTerminal.Create(
            sixelSupport: SixelPresentationSupport.Translated,
            supportsKgp: true);

        await terminal.FeedAsync(Bytes(SmallRedGraphic), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForPresentationLengthAsync(1, TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.PresentationText.Contains("a=p"),
            "KGP placement command",
            TestContext.Current.CancellationToken);

        var text = terminal.PresentationText;
        Assert.Contains("\x1b_Ga=t,f=32", text);
        Assert.Contains("\x1b_Ga=p,i=", text);

        // Workload-authored KGP IDs are typically small; translated IDs always carry
        // the reserved high bit (0x8000_0000), so a naive small-integer collision is
        // structurally impossible.
        var idIndex = text.IndexOf("a=p,i=", StringComparison.Ordinal) + "a=p,i=".Length;
        var idEnd = text.IndexOf(',', idIndex);
        var imageId = uint.Parse(text[idIndex..idEnd]);
        Assert.IsTrue((imageId & 0x8000_0000) != 0, "Translated image IDs must carry the reserved high bit.");
    }

    [TestMethod]
    public async Task KgpTranslatedRoute_ScrollOffScreen_EmitsDeleteCommands()
    {
        await using var terminal = SixelRoutingTestTerminal.Create(
            sixelSupport: SixelPresentationSupport.Translated,
            supportsKgp: true,
            width: 20,
            height: 3);

        await terminal.FeedAsync(Bytes(SmallRedGraphic), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.PresentationText.Contains("a=p"),
            "initial KGP placement",
            TestContext.Current.CancellationToken);

        var newlines = string.Concat(Enumerable.Repeat("\n", 10));
        await terminal.FeedAsync(Bytes(newlines), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.PresentationText.Contains("a=d,d=i"),
            "KGP placement delete",
            TestContext.Current.CancellationToken);

        Assert.Contains("\x1b_Ga=d,d=i,", terminal.PresentationText);
    }

    // Unsupported presentation policy -----------------------------------------------

    [TestMethod]
    public async Task UnsupportedRoute_PlaceholderPolicy_WritesDiagnosticPlaceholder()
    {
        await using var terminal = SixelRoutingTestTerminal.Create(
            sixelSupport: SixelPresentationSupport.None,
            unsupportedPresentation: SixelUnsupportedPresentationPolicy.Placeholder);

        await terminal.FeedAsync(Bytes(SmallRedGraphic), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.PresentationText.Contains("[sixel:"),
            "placeholder text",
            TestContext.Current.CancellationToken);

        Assert.Contains("not shown", terminal.PresentationText);
        Assert.IsTrue(terminal.Diagnostics.Any(d => d.Kind == SixelRasterRouteDiagnosticKind.PlaceholderApplied));
    }

    [TestMethod]
    public async Task UnsupportedRoute_SuppressPolicy_WritesNoPlaceholderText()
    {
        await using var terminal = SixelRoutingTestTerminal.Create(
            sixelSupport: SixelPresentationSupport.None,
            unsupportedPresentation: SixelUnsupportedPresentationPolicy.Suppress);

        var payload = Bytes(SmallRedGraphic);
        await terminal.FeedAsync(payload, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForPresentationLengthAsync(payload.Length, TestContext.Current.CancellationToken);

        // Suppress preserves pre-#458 behavior exactly: raw bytes are still
        // byte-exact forwarded (unconditional passthrough), but nothing *additional*
        // (no placeholder text) is written for a route with no display.
        Assert.DoesNotContain("[sixel:", terminal.PresentationText);
    }

    [TestMethod]
    public async Task TranslatedRoute_WithoutKgp_RaisesTranslationUnavailableDiagnostic()
    {
        await using var terminal = SixelRoutingTestTerminal.Create(
            sixelSupport: SixelPresentationSupport.Translated,
            supportsKgp: false);

        await terminal.FeedAsync(Bytes(SmallRedGraphic), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Diagnostics.Any(d => d.Kind == SixelRasterRouteDiagnosticKind.TranslationUnavailable),
            "translation-unavailable diagnostic",
            TestContext.Current.CancellationToken);

        Assert.IsTrue(terminal.Diagnostics.Any(d => d.Kind == SixelRasterRouteDiagnosticKind.TranslationUnavailable));
    }

    [TestMethod]
    public async Task ManagedSink_AlternateScreenEntry_ReleasesMainPlacementThenReannouncesOnExit()
    {
        await using var terminal = SixelRoutingTestTerminal.Create(asManagedSink: true);

        await terminal.FeedAsync(Bytes(SmallRedGraphic), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForEventCountAsync(2, TestContext.Current.CancellationToken);

        await terminal.FeedAsync(Bytes("\x1b[?1049h"), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.RasterEvents.Any(e => e is SixelRasterScreenTransition t
                && t.Kind == SixelRasterScreenTransitionKind.EnteredAlternateScreen),
            "entered-alternate-screen transition",
            TestContext.Current.CancellationToken);

        var afterEnter = terminal.RasterEvents;
        Assert.IsTrue(afterEnter.Any(e => e is SixelRasterPlacementReleased), "the main-screen placement must be released on entry.");

        await terminal.FeedAsync(Bytes("\x1b[?1049l"), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.RasterEvents.Any(e => e is SixelRasterScreenTransition t
                && t.Kind == SixelRasterScreenTransitionKind.ExitedAlternateScreen),
            "exited-alternate-screen transition",
            TestContext.Current.CancellationToken);

        var afterExit = terminal.RasterEvents;
        Assert.IsTrue(
            afterExit.Count(e => e is SixelRasterPlacementUpdated) >= 2,
            "the main-screen placement must be re-announced when the alternate screen is exited.");
    }

    [TestMethod]
    public async Task ManagedSink_Reset_EmitsSixelRasterReset()
    {
        await using var terminal = SixelRoutingTestTerminal.Create(asManagedSink: true);

        await terminal.FeedAsync(Bytes(SmallRedGraphic), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForEventCountAsync(2, TestContext.Current.CancellationToken);

        // ESC c (RIS) is not decoded into a RisToken on the raw-byte workload path;
        // drive it via a pre-tokenized RisToken, matching the convention already
        // used by SixelRasterIntegrationTests/SixelTerminalSemanticsTests.
        await terminal.FeedPreTokenizedAsync(
            Bytes("\x1bc"),
            [RisToken.Instance],
            TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.RasterEvents.Any(e => e is SixelRasterReset),
            "reset event",
            TestContext.Current.CancellationToken);

        Assert.IsTrue(terminal.RasterEvents.Any(e => e is SixelRasterReset));
    }

    [TestMethod]
    public async Task ManagedSink_TextOverwrite_DamagesLivePlacement()
    {
        await using var terminal = SixelRoutingTestTerminal.Create(asManagedSink: true);

        await terminal.FeedAsync(Bytes(SmallRedGraphic), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForEventCountAsync(2, TestContext.Current.CancellationToken);

        // The graphic was painted at the origin; writing text back at the origin
        // overwrites the same cell the placement occupies.
        await terminal.FeedAsync(Bytes("\x1b[1;1HX"), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.RasterEvents.Any(e => e is SixelRasterPlacementDamaged or SixelRasterPlacementReleased),
            "damage or release from text overwrite",
            TestContext.Current.CancellationToken);

        Assert.IsTrue(terminal.RasterEvents.Any(e => e is SixelRasterPlacementDamaged or SixelRasterPlacementReleased));
    }

    [TestMethod]
    public async Task KgpTranslatedRoute_IdenticalContentTwice_DoesNotRetransmitImage()
    {
        await using var terminal = SixelRoutingTestTerminal.Create(
            sixelSupport: SixelPresentationSupport.Translated,
            supportsKgp: true,
            width: 40,
            height: 20);

        await terminal.FeedAsync(Bytes(SmallRedGraphic), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.PresentationText.Contains("a=p"),
            "first KGP placement",
            TestContext.Current.CancellationToken);

        var afterFirst = terminal.PresentationText;
        var firstTransmitCount = CountOccurrences(afterFirst, "a=t,f=32");
        Assert.AreEqual(1, firstTransmitCount);

        await terminal.FeedAsync(
            Bytes("\x1b[2;1H" + SmallRedGraphic),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => CountOccurrences(terminal.PresentationText, "a=p,i=") >= 2,
            "second KGP placement",
            TestContext.Current.CancellationToken);

        // Same raster content, painted at a fresh anchor: a second placement command
        // is transmitted, but the image content itself is defined only once.
        Assert.AreEqual(1, CountOccurrences(terminal.PresentationText, "a=t,f=32"));
        Assert.AreEqual(2, CountOccurrences(terminal.PresentationText, "a=p,i="));
    }

    [TestMethod]
    public async Task RouteChangeAwayFromKgpTranslated_ReleasesStalePlacementAndImage()
    {
        // Regression for the confirmed defect: switching the effective route away
        // from KgpTranslated on the same live presentation must release every
        // outstanding translated placement/image before bookkeeping resets, so the
        // presentation is never left with stale KGP graphics it can no longer be
        // told about.
        await using var terminal = SixelRoutingTestTerminal.Create(
            sixelSupport: SixelPresentationSupport.Translated,
            supportsKgp: true);

        await terminal.FeedAsync(Bytes(SmallRedGraphic), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.PresentationText.Contains("a=p"),
            "initial KGP placement",
            TestContext.Current.CancellationToken);

        Assert.Contains("a=t,f=32", terminal.PresentationText);
        Assert.DoesNotContain("a=d,d=i,", terminal.PresentationText);

        // The presentation connection itself never changes (see Hex1bTerminal's
        // readonly _presentation field) — simulate a post-discovery capability
        // update that moves the effective route away from KgpTranslated entirely.
        terminal.SetSixelSupport(SixelPresentationSupport.Native);

        await terminal.FeedAsync(Bytes("x"), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.PresentationText.Contains("a=d,d=i,"),
            "KGP placement delete on route change",
            TestContext.Current.CancellationToken);

        var text = terminal.PresentationText;
        Assert.Contains("\x1b_Ga=d,d=i,", text);
        Assert.Contains("\x1b_Ga=d,d=I,", text);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    // Sanitization -------------------------------------------------------------

    [TestMethod]
    public async Task Sanitization_SuppressesMalformedGraphic_PreservesOrdinaryText()
    {
        await using var terminal = SixelRoutingTestTerminal.Create(
            sixelSupport: SixelPresentationSupport.Native,
            sanitization: SixelSanitizationPolicy.Enable());

        await terminal.FeedAsync(
            Bytes("before" + MalformedGraphic + "after"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.PresentationText.Contains("after"),
            "text after suppressed graphic",
            TestContext.Current.CancellationToken);

        var text = terminal.PresentationText;
        Assert.Contains("before", text);
        Assert.Contains("after", text);
        Assert.DoesNotContain("\x1bP", text);
        Assert.IsTrue(terminal.Diagnostics.Any(d => d.Kind == SixelRasterRouteDiagnosticKind.Suppressed));
    }

    [TestMethod]
    public async Task Sanitization_Disabled_DefaultPreservesByteExactPassthrough()
    {
        var payload = Bytes("before" + MalformedGraphic + "after");
        await using var terminal = SixelRoutingTestTerminal.Create(sixelSupport: SixelPresentationSupport.Native);

        await terminal.FeedAsync(payload, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForPresentationLengthAsync(payload.Length, TestContext.Current.CancellationToken);

        CollectionAssert.AreEqual(payload, terminal.PresentationBytes);
    }

    [TestMethod]
    public async Task Sanitization_GeometryOnlyDefaultNotSuppressed_UnlessOptedIn()
    {
        var payload = Bytes(GeometryOnlyGraphic);
        await using var terminal = SixelRoutingTestTerminal.Create(
            sixelSupport: SixelPresentationSupport.Native,
            sanitization: SixelSanitizationPolicy.Enable());

        await terminal.FeedAsync(payload, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForPresentationLengthAsync(payload.Length, TestContext.Current.CancellationToken);

        // SuppressGeometryOnly defaults to false: a geometry-only downgrade is a
        // legitimate, non-malformed outcome, so sanitization leaves it untouched
        // unless a host separately opts in.
        CollectionAssert.AreEqual(payload, terminal.PresentationBytes);
    }

    [TestMethod]
    public async Task Sanitization_SuppressCancelledOrUnterminatedDefaultTrue_DropsCancelledFrame()
    {
        // Regression for the confirmed defect: enabling sanitization with the
        // (default) SuppressCancelledOrUnterminated=true must still drop a cancelled
        // Sixel frame's bytes entirely, preserving legacy/default behavior.
        var payload = BuildCancelledSixelPayload("before", "after");
        await using var terminal = SixelRoutingTestTerminal.Create(
            sixelSupport: SixelPresentationSupport.Native,
            sanitization: SixelSanitizationPolicy.Enable());

        await terminal.FeedAsync(payload, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.PresentationText.Contains("after"),
            "text after the cancelled graphic",
            TestContext.Current.CancellationToken);

        var text = terminal.PresentationText;
        Assert.Contains("before", text);
        Assert.Contains("after", text);
        Assert.DoesNotContain("\x1bP", text);
    }

    [TestMethod]
    public async Task Sanitization_SuppressCancelledOrUnterminatedFalse_ForwardsBoundedBytesAndPreservesText()
    {
        // Regression for the confirmed defect: SuppressCancelledOrUnterminated=false
        // must forward the cancelled frame's bounded retained bytes verbatim instead
        // of unconditionally discarding them, while leaving surrounding ordinary text
        // completely unaffected.
        var payload = BuildCancelledSixelPayload("before", "after");
        await using var terminal = SixelRoutingTestTerminal.Create(
            sixelSupport: SixelPresentationSupport.Native,
            sanitization: SixelSanitizationPolicy.Enable() with { SuppressCancelledOrUnterminated = false });

        await terminal.FeedAsync(payload, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.PresentationText.Contains("after"),
            "text after the forwarded cancelled graphic",
            TestContext.Current.CancellationToken);

        var text = terminal.PresentationText;
        Assert.Contains("before", text);
        Assert.Contains("after", text);
        // The reconstructed forward carries the introducer/payload bytes retained up
        // to cancellation, wrapped back into a self-contained DCS sequence.
        Assert.Contains("\x1bP", text);
        Assert.Contains(CancelledSixelPrefix, text);
    }

    [TestMethod]
    public async Task Sanitization_SuppressRetentionLimitExceededDefaultTrue_DropsOversizedFrame()
    {
        // Regression for the confirmed defect: enabling sanitization with the
        // (default) SuppressRetentionLimitExceeded=true must still drop an
        // oversized Sixel frame's bytes entirely, preserving legacy/default behavior.
        var payload = BuildOversizedSixelPayload("before", "after");
        await using var terminal = SixelRoutingTestTerminal.Create(
            sixelSupport: SixelPresentationSupport.Native,
            sanitization: SixelSanitizationPolicy.Enable());

        await terminal.FeedAsync(payload, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.PresentationText.Contains("after"),
            "text after the oversized graphic",
            TestContext.Current.CancellationToken);

        var text = terminal.PresentationText;
        Assert.Contains("before", text);
        Assert.Contains("after", text);
        Assert.DoesNotContain("\x1bP", text);
    }

    [TestMethod]
    public async Task Sanitization_SuppressRetentionLimitExceededFalse_ForwardsBoundedRetainedBytes()
    {
        // Regression for the confirmed defect: SuppressRetentionLimitExceeded=false
        // must forward the bounded bytes retained up to the framer's retention limit
        // instead of unconditionally discarding them, while unrelated text remains
        // unaffected.
        var payload = BuildOversizedSixelPayload("before", "after");
        await using var terminal = SixelRoutingTestTerminal.Create(
            sixelSupport: SixelPresentationSupport.Native,
            sanitization: SixelSanitizationPolicy.Enable() with { SuppressRetentionLimitExceeded = false });

        await terminal.FeedAsync(payload, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.PresentationText.Contains("after"),
            "text after the forwarded oversized graphic",
            TestContext.Current.CancellationToken);

        var text = terminal.PresentationText;
        Assert.Contains("before", text);
        Assert.Contains("after", text);
        Assert.Contains("\x1bP", text);
    }

    // Builds "<prefix><Sixel-shaped DCS introducer + partial payload><CAN><suffix>":
    // CAN (0x18) cancels the DCS mid-stream (DcsSequenceStatus.Cancelled) rather than
    // terminating it normally, and text resumes immediately afterward.
    private static byte[] BuildCancelledSixelPayload(string prefix, string suffix)
    {
        var bytes = new List<byte>();
        bytes.AddRange(Bytes(prefix));
        bytes.AddRange(Bytes(CancelledSixelPrefix));
        bytes.Add(0x18);
        bytes.AddRange(Bytes(suffix));
        return [.. bytes];
    }

    // Builds "<prefix><Sixel-shaped DCS introducer + payload past the default 1 MiB
    // retention limit><ST><suffix>": the frame completes normally, but
    // DcsFrame.RetentionLimitExceeded is set because its content exceeded the
    // framer's bounded retention buffer (DcsByteStreamParser.DefaultRetentionLimit).
    private static byte[] BuildOversizedSixelPayload(string prefix, string suffix)
    {
        const int oversizedPayloadLength = 1_100_000;
        var bytes = new List<byte>();
        bytes.AddRange(Bytes(prefix));
        bytes.AddRange(Bytes("\x1bP0;1q"));
        bytes.AddRange(Enumerable.Repeat((byte)'~', oversizedPayloadLength));
        bytes.AddRange(Bytes("\x1b\\"));
        bytes.AddRange(Bytes(suffix));
        return [.. bytes];
    }
}
