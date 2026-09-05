using System.Text;
using Hex1b.Kgp;
using Hex1b.Sixel;
using Hex1b.Tokens;

namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    private readonly SixelRasterRouter _sixelRasterRouter = new();
    private readonly KgpSixelTranslator _kgpSixelTranslator = new();
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
        route is SixelEffectiveRoute.ManagedRasterSink or SixelEffectiveRoute.KgpTranslated
        || (route is SixelEffectiveRoute.Unsupported && _sixelUnsupportedPresentation == SixelUnsupportedPresentationPolicy.Placeholder)
        || SixelRouteDiagnosticRaised is not null;

    /// <summary>
    /// Diffs this batch's now-current live Sixel placements against what was
    /// previously observed, and delivers the resulting ordered events to whatever
    /// consumes them for the current effective route: a managed raster sink, the KGP
    /// translator, an unsupported-presentation placeholder, and/or the
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
            _kgpSixelTranslator.ResetBookkeeping();
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

            case SixelEffectiveRoute.KgpTranslated:
                var placementsBySequence = new Dictionary<long, SixelPlacement>(SixelPlacementCount);
                foreach (var placement in SixelPlacements)
                {
                    placementsBySequence[placement.Sequence] = placement;
                }

                await _kgpSixelTranslator.ApplyAsync(
                    events,
                    placementsBySequence,
                    _presentation,
                    _cursorY,
                    _cursorX,
                    ct).ConfigureAwait(false);
                break;

            case SixelEffectiveRoute.Unsupported:
                if (Capabilities.SixelSupport == SixelPresentationSupport.Translated)
                {
                    // Translation was explicitly requested (SixelSupport == Translated)
                    // but no supported translation target is available (currently only
                    // KGP) -- report this distinctly from "no translation was ever
                    // requested" so a host can tell the two Unsupported reasons apart.
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
    /// Applies the opt-in <see cref="SixelSanitizationPolicy"/> to a batch's tokens,
    /// immediately before they would otherwise be serialized and forwarded to the
    /// presentation. Only Sixel-shaped DCS tokens are ever affected; ordinary text and
    /// unrelated DCS framing are returned unchanged.
    /// </summary>
    /// <remarks>
    /// Cancelled, unterminated, and retention-limit-exceeded Sixel sequences never
    /// produce a <see cref="DcsToken"/> in the first place (see
    /// <see cref="TokenizeRawWorkloadOutput"/>), so once immediate raw-byte forwarding
    /// is disabled for sanitization, those outcomes are already excluded from the
    /// token stream with no further action needed here; this method only needs to
    /// additionally filter the two outcomes that do still produce a token: malformed
    /// content and limit-downgraded (geometry-only) content.
    /// </remarks>
    private IReadOnlyList<AnsiToken> ApplySixelSanitization(
        IReadOnlyList<AnsiToken> tokens,
        IReadOnlyDictionary<DcsToken, DcsFrame>? framedDcs)
    {
        if (!_sixelSanitization.Enabled || framedDcs is null || framedDcs.Count == 0)
        {
            return tokens;
        }

        List<AnsiToken>? filtered = null;
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token is DcsToken dcsToken &&
                framedDcs.TryGetValue(dcsToken, out var frame) &&
                frame.Introducer.IsSixel &&
                ShouldSanitizeSuppress(frame.SixelResult.Outcome, out var reason))
            {
                filtered ??= new List<AnsiToken>(tokens.Take(i));
                SixelRouteDiagnosticRaised?.Invoke(new SixelRasterRouteDiagnostic(
                    SixelRasterRouteDiagnosticKind.Suppressed,
                    $"A Sixel DCS sequence was suppressed by the configured sanitization policy ({reason}).",
                    null));
                continue;
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
}
