using System.Text;
using Hex1b.Sixel;
using Hex1b.Tokens;

namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    private readonly SixelRasterRouter _sixelRasterRouter = new();
    private SixelEffectiveRoute _lastSixelRoute;
    private bool _lastSixelRouteInitialized;
    private SixelSanitizationPolicy _sixelSanitization = SixelSanitizationPolicy.Disabled;
    private SixelUnsupportedPresentationPolicy _sixelUnsupportedPresentation = SixelUnsupportedPresentationPolicy.Suppress;

    /// <summary>
    /// Raised for an explicit, non-silent diagnostic about Sixel routing, translation,
    /// or policy-affected handling — never raised silently in place of one of these
    /// situations. See <see cref="SixelRasterRouteDiagnosticKind"/> for what is
    /// reported.
    /// </summary>
    /// <remarks>
    /// This is observable regardless of the effective <see cref="SixelEffectiveRoute"/>
    /// (including native passthrough and headless), so a host can detect geometry-only
    /// downgrades, desynchronization, sanitization, and placeholder substitution even
    /// without attaching an <see cref="Sixel.ISixelRasterPresentationSink"/>. Managed
    /// sinks additionally receive the same diagnostics inline in their ordered event
    /// stream via <see cref="Sixel.SixelRasterRouteDiagnostic"/>.
    /// </remarks>
    public event Action<SixelRasterRouteDiagnostic>? SixelRouteDiagnosticRaised;

    private void ConfigureSixelRouting(Hex1bTerminalOptions options)
    {
        _sixelSanitization = options.SixelSanitization ?? SixelSanitizationPolicy.Disabled;
        _sixelUnsupportedPresentation = options.SixelUnsupportedPresentation;
    }

    /// <summary>
    /// Whether the current effective Sixel route (or an attached diagnostics
    /// listener) requires cell-impact tracking for this batch, so the caller knows
    /// whether it can safely skip <see cref="ApplyTokensWithImpacts"/> in favor of the
    /// cheaper impact-free <see cref="ApplyTokens"/> fast path.
    /// </summary>
    private bool SixelRoutingNeedsGraphicsImpacts(SixelEffectiveRoute route) =>
        route is SixelEffectiveRoute.ManagedRasterSink
        || (route is SixelEffectiveRoute.Unsupported && _sixelUnsupportedPresentation == SixelUnsupportedPresentationPolicy.Placeholder)
        || SixelRouteDiagnosticRaised is not null;

    /// <summary>
    /// Whether the effective Sixel route permits Sixel DCS wire bytes — raw or
    /// reconstructed from tokens — to reach the presentation at all. This is what
    /// actually gates Sixel byte forwarding (see <see cref="Hex1bTerminal.PumpWorkloadOutputAsync"/>
    /// and <see cref="FilterSixelWireTokens"/>), independent of whatever generic
    /// raw-byte fast path the presentation would otherwise qualify for.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><see cref="SixelEffectiveRoute.NativePassthrough"/> and
    ///     <see cref="SixelEffectiveRoute.Unsupported"/> always allow it: a native
    ///     upstream is the intended byte-exact recipient, and <c>Unsupported</c>
    ///     preserves pre-#458 passthrough behavior for a presentation with no managed
    ///     sink and no translation route (see <see cref="Sixel.SixelUnsupportedPresentationPolicy"/>
    ///     for what else, if anything, is layered on top).</item>
    ///   <item><see cref="SixelEffectiveRoute.ManagedRasterSink"/> allows it only when
    ///     the presentation's own capabilities are also <see cref="SixelPresentationSupport.Native"/>
    ///     — the documented dual-delivery case where a managed sink observes
    ///     structured events <em>alongside</em>, not instead of, raw bytes reaching a
    ///     real native upstream through the same presentation.</item>
    ///   <item><see cref="SixelEffectiveRoute.Headless"/> never allows it: this
    ///     presentation only ever receives the authoritative model (as structured
    ///     events or neither) — raw, uninterpretable Sixel bytes would serve no
    ///     purpose and would violate the "structured events only" contract this
    ///     route documents.</item>
    /// </list>
    /// </remarks>
    private bool SixelRouteAllowsRawWire(SixelEffectiveRoute route) => route switch
    {
        SixelEffectiveRoute.NativePassthrough => true,
        SixelEffectiveRoute.Unsupported => true,
        SixelEffectiveRoute.ManagedRasterSink => Capabilities.SixelSupport == SixelPresentationSupport.Native,
        _ => false,
    };

    /// <summary>
    /// Diffs this batch's now-current live Sixel placements against what was
    /// previously observed, and delivers the resulting ordered events to whatever
    /// consumes them for the current effective route: a managed raster sink, an
    /// unsupported-presentation placeholder, and/or the
    /// <see cref="SixelRouteDiagnosticRaised"/> event.
    /// </summary>
    /// <param name="route">The effective route computed for this batch.</param>
    /// <param name="appliedTokens">The tokens applied so far this batch.</param>
    /// <param name="wasAlternateScreenBefore">
    /// Whether <see cref="_inAlternateScreen"/> was <see langword="true"/> before this
    /// batch's tokens were applied, used to detect a net screen transition.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    private async ValueTask ProcessSixelRoutingAsync(
        SixelEffectiveRoute route,
        IReadOnlyList<AppliedToken> appliedTokens,
        bool wasAlternateScreenBefore,
        CancellationToken ct)
    {
        if (!_lastSixelRouteInitialized || route != _lastSixelRoute)
        {
            // A route change (including the very first batch) starts dedup/visibility
            // bookkeeping clean without rewriting any already-reported historical
            // placement metrics — the authoritative SixelGraphicsState itself is
            // untouched by this reset.
            _sixelRasterRouter.ResetBookkeeping();
            _lastSixelRoute = route;
            _lastSixelRouteInitialized = true;
        }

        var wasReset = false;
        List<(int Row, int Column, int Width, int Height)>? damagedRegions = null;
        foreach (var applied in appliedTokens)
        {
            if (applied.Token is RisToken)
            {
                wasReset = true;
            }

            if (!applied.HasGraphicsImpacts)
            {
                continue;
            }

            foreach (var impact in applied.GraphicsImpacts)
            {
                if (impact.Kind == TerminalGraphicsImpactKind.SixelDamaged)
                {
                    (damagedRegions ??= []).Add((impact.Y, impact.X, impact.Width, impact.Height));
                }
            }
        }

        SixelRasterScreenTransitionKind? screenTransition = wasAlternateScreenBefore == _inAlternateScreen
            ? null
            : _inAlternateScreen
                ? SixelRasterScreenTransitionKind.EnteredAlternateScreen
                : SixelRasterScreenTransitionKind.ExitedAlternateScreen;

        var events = _sixelRasterRouter.ObserveBatch(
            SixelPlacements,
            (IReadOnlyList<(int, int, int, int)>?)damagedRegions ?? [],
            wasReset,
            screenTransition);

        if (events.Count == 0)
        {
            return;
        }

        foreach (var evt in events)
        {
            if (evt is SixelRasterRouteDiagnostic diagnostic)
            {
                SixelRouteDiagnosticRaised?.Invoke(diagnostic);
            }
        }

        switch (route)
        {
            case SixelEffectiveRoute.ManagedRasterSink when _presentation is ISixelRasterPresentationSink sink:
                await sink.OnSixelRasterEventsAsync(events, ct).ConfigureAwait(false);
                break;

            case SixelEffectiveRoute.Unsupported:
                if (Capabilities.SixelSupport == SixelPresentationSupport.Translated)
                {
                    // Translation was explicitly requested (SixelSupport == Translated),
                    // but Hex1b does not translate Sixel into another wire protocol --
                    // report this distinctly from "no translation was ever requested" so
                    // a host can tell the two Unsupported reasons apart.
                    RaiseTranslationUnavailableDiagnostics(events);
                }

                if (_sixelUnsupportedPresentation == SixelUnsupportedPresentationPolicy.Placeholder)
                {
                    await WriteSixelUnsupportedPlaceholdersAsync(events, ct).ConfigureAwait(false);
                }
                break;
        }
    }

    private void RaiseTranslationUnavailableDiagnostics(IReadOnlyList<SixelRasterEvent> events)
    {
        foreach (var evt in events)
        {
            if (evt is not SixelRasterPlacementUpdated { IsNewPlacement: true } updated)
            {
                continue;
            }

            SixelRouteDiagnosticRaised?.Invoke(new SixelRasterRouteDiagnostic(
                SixelRasterRouteDiagnosticKind.TranslationUnavailable,
                $"Placement {updated.Placement.Sequence} requested Sixel-to-image-protocol translation (SixelSupport.Translated), but no supported translation target is available.",
                updated.Placement.Sequence));
        }
    }

    private async ValueTask WriteSixelUnsupportedPlaceholdersAsync(
        IReadOnlyList<SixelRasterEvent> events,
        CancellationToken ct)
    {
        foreach (var evt in events)
        {
            if (evt is not SixelRasterPlacementUpdated { IsNewPlacement: true } updated)
            {
                continue;
            }

            var placement = updated.Placement;
            var text =
                $"[sixel: {placement.Image.PixelWidth}x{placement.Image.PixelHeight} image not shown \u2014 presentation cannot display graphics]";
            await _presentation.WriteOutputAsync(Encoding.ASCII.GetBytes(text), ct).ConfigureAwait(false);

            SixelRouteDiagnosticRaised?.Invoke(new SixelRasterRouteDiagnostic(
                SixelRasterRouteDiagnosticKind.PlaceholderApplied,
                $"Placement {placement.Sequence} substituted with a diagnostic placeholder; the effective presentation cannot render Sixel graphics and no translation route is available.",
                placement.Sequence));
        }
    }

    /// <summary>
    /// Filters a batch's tokens for Sixel wire delivery, immediately before they
    /// would otherwise be serialized and forwarded to the presentation. Only
    /// Sixel-shaped DCS tokens (and their bounded sanitization-forwarding
    /// counterparts, see <see cref="SixelSanitizedFrameForwardToken"/>) are ever
    /// affected; ordinary text and unrelated DCS framing are returned unchanged.
    /// </summary>
    /// <param name="tokens">The batch's tokens, already passed through presentation filters.</param>
    /// <param name="framedDcs">Structured DCS frames already parsed from raw bytes.</param>
    /// <param name="sixelAllowsRawWire">
    /// Whether the effective Sixel route (see <see cref="SixelRouteAllowsRawWire"/>)
    /// permits Sixel wire bytes to reach the presentation at all this batch. When
    /// <see langword="false"/>, every Sixel-shaped token is removed regardless of the
    /// sanitization policy — the route's own mechanism (structured events or neither)
    /// already carries the authoritative model, and raw or reconstructed Sixel bytes
    /// would be redundant, uninterpretable noise.
    /// </param>
    /// <remarks>
    /// When <paramref name="sixelAllowsRawWire"/> is <see langword="true"/>, this
    /// applies the opt-in <see cref="SixelSanitizationPolicy"/>: a malformed or
    /// geometry-only-downgraded sequence's <see cref="DcsToken"/> is removed per
    /// <see cref="ShouldSanitizeSuppress"/>, and a cancelled/unterminated/retention-limit-exceeded
    /// sequence's bounded forwarding token (see <see cref="TokenizeRawWorkloadOutput"/>
    /// and <see cref="ShouldForwardSanitizedIncompleteSixelFrame"/>) is left untouched
    /// — it was only created in the first place because sanitization opted back into
    /// forwarding that specific outcome.
    /// </remarks>
    private IReadOnlyList<AnsiToken> FilterSixelWireTokens(
        IReadOnlyList<AnsiToken> tokens,
        IReadOnlyDictionary<DcsToken, DcsFrame>? framedDcs,
        bool sixelAllowsRawWire)
    {
        if (sixelAllowsRawWire && (!_sixelSanitization.Enabled || framedDcs is null || framedDcs.Count == 0))
        {
            return tokens;
        }

        List<AnsiToken>? filtered = null;
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (!sixelAllowsRawWire && token is SixelSanitizedFrameForwardToken)
            {
                // Sanitization opted back into forwarding this frame's bytes, but the
                // effective route doesn't deliver Sixel wire bytes at all this batch
                // — route precedence wins, since forwarding an incomplete Sixel
                // sequence to a route that never shows raw Sixel bytes would serve no
                // purpose.
                filtered ??= new List<AnsiToken>(tokens.Take(i));
                continue;
            }

            if (token is DcsToken dcsToken &&
                framedDcs is not null &&
                framedDcs.TryGetValue(dcsToken, out var frame) &&
                frame.Introducer.IsSixel)
            {
                if (!sixelAllowsRawWire)
                {
                    filtered ??= new List<AnsiToken>(tokens.Take(i));
                    continue;
                }

                if (_sixelSanitization.Enabled && ShouldSanitizeSuppress(frame.SixelResult.Outcome, out var reason))
                {
                    filtered ??= new List<AnsiToken>(tokens.Take(i));
                    SixelRouteDiagnosticRaised?.Invoke(new SixelRasterRouteDiagnostic(
                        SixelRasterRouteDiagnosticKind.Suppressed,
                        $"A Sixel DCS sequence was suppressed by the configured sanitization policy ({reason}).",
                        null));
                    continue;
                }
            }

            filtered?.Add(token);
        }

        return filtered ?? tokens;
    }

    private bool ShouldSanitizeSuppress(SixelParseOutcome outcome, out string reason)
    {
        switch (outcome)
        {
            case SixelParseOutcome.Malformed when _sixelSanitization.SuppressMalformed:
                reason = "malformed";
                return true;
            case SixelParseOutcome.LimitDowngraded when _sixelSanitization.SuppressGeometryOnly:
                reason = "geometry-only downgrade";
                return true;
            default:
                reason = "";
                return false;
        }
    }

    /// <summary>
    /// Whether a cancelled, unterminated, or retention-limit-exceeded Sixel DCS
    /// frame's bounded retained bytes should still be forwarded, per the opt-in
    /// <see cref="SixelSanitizationPolicy"/>. These outcomes never produce a
    /// <see cref="DcsToken"/> (see <see cref="TokenizeRawWorkloadOutput"/>), so this
    /// is the only mechanism by which <see cref="SixelSanitizationPolicy.SuppressCancelledOrUnterminated"/>
    /// and <see cref="SixelSanitizationPolicy.SuppressRetentionLimitExceeded"/>
    /// actually take effect.
    /// </summary>
    private bool ShouldForwardSanitizedIncompleteSixelFrame(DcsFrame frame)
    {
        if (!_sixelSanitization.Enabled)
        {
            // With sanitization disabled, these outcomes are unconditionally
            // discarded exactly as before — unaffected, matching this method's
            // narrow scope to the opt-in sanitization policy only.
            return false;
        }

        return frame.RetentionLimitExceeded
            ? !_sixelSanitization.SuppressRetentionLimitExceeded
            : !_sixelSanitization.SuppressCancelledOrUnterminated;
    }

    /// <summary>
    /// Reconstructs the bounded <c>ESC P ... ESC \</c> wire bytes for a frame whose
    /// framing was cancelled, unterminated, or retention-limit-exceeded, wrapped in a
    /// token that forwards them verbatim without contributing any Sixel model state.
    /// </summary>
    private static SixelSanitizedFrameForwardToken CreateSanitizedFrameForwardToken(DcsFrame frame)
    {
        var content = frame.RetainedContent.Span;
        var wireBytes = new byte[content.Length + 4];
        wireBytes[0] = 0x1b;
        wireBytes[1] = (byte)'P';
        content.CopyTo(wireBytes.AsSpan(2));
        wireBytes[^2] = 0x1b;
        wireBytes[^1] = (byte)'\\';
        return new SixelSanitizedFrameForwardToken(wireBytes);
    }
}
