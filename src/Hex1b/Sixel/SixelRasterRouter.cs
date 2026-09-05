using Hex1b;

namespace Hex1b.Sixel;

/// <summary>
/// Identifies which concrete path Sixel graphics take from Hex1b's authoritative
/// model to a presentation, for a given <see cref="Hex1b.TerminalCapabilities"/> and
/// active presentation adapter.
/// </summary>
/// <remarks>
/// This is the routing decision <see href="https://github.com/mitchdenny/hex1b/issues/458">#458</see>
/// introduces on top of the discovery model <see href="https://github.com/mitchdenny/hex1b/issues/455">#455</see>
/// built (<see cref="SixelPresentationSupport"/>). It always reflects the actual
/// selected path — never merely what the parser/model can represent — so effective
/// capability reported to a hosted workload matches reality.
/// </remarks>
internal enum SixelEffectiveRoute
{
    /// <summary>Raw Sixel DCS bytes reach a real, native upstream terminal unchanged.</summary>
    NativePassthrough,

    /// <summary>
    /// The active presentation implements <see cref="ISixelRasterPresentationSink"/>
    /// and receives ordered, protocol-neutral raster events instead of (or alongside,
    /// if it is also <see cref="SixelPresentationSupport.Native"/>) raw bytes.
    /// </summary>
    ManagedRasterSink,

    /// <summary>There is no real display; Hex1b's own model is authoritative and no output is rendered.</summary>
    Headless,

    /// <summary>
    /// No route can render Sixel graphics for a human to see: no native support, no
    /// managed sink, and no translation target is available (Hex1b does not
    /// translate Sixel into another wire protocol; see
    /// <see cref="SixelPresentationSupport.Translated"/>). The authoritative model
    /// is still retained; see <see cref="SixelUnsupportedPresentationPolicy"/> for
    /// what (if anything) is substituted in the output stream.
    /// </summary>
    Unsupported,
}

/// <summary>
/// Computes the effective Sixel route for a terminal/presentation pairing and
/// incrementally diffs the authoritative live placement set into an ordered
/// <see cref="SixelRasterEvent"/> stream for a <see cref="ISixelRasterPresentationSink"/>
/// to consume.
/// </summary>
/// <remarks>
/// This type owns no terminal state of its own beyond its own dedup/visibility
/// bookkeeping: it is driven imperatively, once per output batch, by
/// <see cref="Hex1bTerminal"/> passing in the batch's now-current live placements
/// (<see cref="Hex1b.SixelPlacement"/> instances mutate <see cref="Hex1b.SixelPlacement.Row"/>
/// in place and are replaced wholesale when their painted crop shrinks, so this type
/// snapshots the geometry it has already reported rather than comparing object
/// references or relying on placements overriding equality).
/// </remarks>
internal sealed class SixelRasterRouter
{
    private readonly record struct GeometrySnapshot(
        int Row,
        int Column,
        int PaintedRowOffset,
        int PaintedRowCount,
        int PaintedColumnOffset,
        int PaintedColumnCount,
        bool IsGeometryOnly)
    {
        internal static GeometrySnapshot From(SixelPlacement placement) => new(
            placement.Row,
            placement.Column,
            placement.PaintedRowOffset,
            placement.PaintedRowCount,
            placement.PaintedColumnOffset,
            placement.PaintedColumnCount,
            placement.IsGeometryOnly);
    }

    private readonly Dictionary<long, GeometrySnapshot> _lastVisible = [];
    private readonly HashSet<byte[]> _definedContentHashes = new(SixelContentHashComparer.Instance);

    /// <summary>
    /// Computes the effective route for the given capability/presentation pairing.
    /// </summary>
    /// <param name="capabilities">The terminal's current capability set.</param>
    /// <param name="presentation">The active presentation adapter.</param>
    /// <remarks>
    /// Hex1b never translates Sixel into a different wire protocol: a
    /// <see cref="SixelPresentationSupport.Translated"/> presentation always
    /// resolves to <see cref="SixelEffectiveRoute.Unsupported"/> (with a
    /// <see cref="SixelRasterRouteDiagnosticKind.TranslationUnavailable"/>
    /// diagnostic), governed by <see cref="SixelUnsupportedPresentationPolicy"/>
    /// like any other unsupported route. Translation was deliberately scoped out —
    /// rewriting one graphics protocol into another on the wire is a meaningful
    /// behavioral decision Hex1b does not make on a host's behalf. A protocol-neutral
    /// managed raster sink (<see cref="ISixelRasterPresentationSink"/>) remains
    /// available for any presentation that wants to render Sixel content through a
    /// different mechanism.
    /// </remarks>
    internal static SixelEffectiveRoute ComputeRoute(
        TerminalCapabilities capabilities,
        IHex1bTerminalPresentationAdapter presentation)
    {
        if (presentation is ISixelRasterPresentationSink)
            return SixelEffectiveRoute.ManagedRasterSink;

        return capabilities.SixelSupport switch
        {
            SixelPresentationSupport.Native => SixelEffectiveRoute.NativePassthrough,
            SixelPresentationSupport.Headless => SixelEffectiveRoute.Headless,
            _ => SixelEffectiveRoute.Unsupported,
        };
    }

    /// <summary>
    /// Resets all dedup/visibility bookkeeping, as if no placement or content had
    /// ever been observed. Used when a managed sink or translator is attached fresh,
    /// or after a reconnect, so historical placement metrics are never rewritten but
    /// bookkeeping itself starts clean.
    /// </summary>
    internal void ResetBookkeeping()
    {
        _lastVisible.Clear();
        _definedContentHashes.Clear();
    }

    /// <summary>
    /// Diffs the given batch's now-current live placements against what this router
    /// last reported, returning an ordered event list (possibly empty) describing
    /// everything that changed.
    /// </summary>
    /// <param name="currentPlacements">The active screen's current live placements.</param>
    /// <param name="damagedRegions">
    /// Cell-granular damage rectangles observed for this batch (from
    /// <see cref="Tokens.TerminalGraphicsImpact"/> entries of kind
    /// <see cref="Tokens.TerminalGraphicsImpactKind.SixelDamaged"/>).
    /// </param>
    /// <param name="wasReset">Whether a full terminal reset (RIS) occurred in this batch.</param>
    /// <param name="screenTransition">The screen transition that occurred in this batch, if any.</param>
    internal List<SixelRasterEvent> ObserveBatch(
        IReadOnlyList<SixelPlacement> currentPlacements,
        IReadOnlyList<(int Row, int Column, int Width, int Height)> damagedRegions,
        bool wasReset,
        SixelRasterScreenTransitionKind? screenTransition)
    {
        var events = new List<SixelRasterEvent>();

        if (wasReset)
        {
            events.Add(new SixelRasterReset());
            _lastVisible.Clear();
            _definedContentHashes.Clear();
        }
        else if (screenTransition is { } kind)
        {
            events.Add(new SixelRasterScreenTransition(kind));
            foreach (var sequence in _lastVisible.Keys)
                events.Add(new SixelRasterPlacementReleased(sequence));
            _lastVisible.Clear();
            // Content identity is independent of which screen references it, so the
            // defined-content set intentionally survives a screen transition: a
            // placement re-announced on the screen being entered does not force a
            // redundant SixelRasterContentDefined if the sink already has that content.
        }

        var currentBySequence = new Dictionary<long, SixelPlacement>(currentPlacements.Count);
        foreach (var placement in currentPlacements)
            currentBySequence[placement.Sequence] = placement;

        // Iterate currentPlacements directly (not the dictionary, whose enumeration
        // order is unspecified) so that overlapping-placement stacking order —
        // ascending SixelPlacement.Sequence, "later sequence paints on top" — is
        // preserved in the emitted event order for the managed sink, which does not
        // re-derive paint order from scratch.
        foreach (var placement in currentPlacements)
        {
            var sequence = placement.Sequence;
            if (!_lastVisible.TryGetValue(sequence, out var previous))
            {
                if (_definedContentHashes.Add(placement.Image.ContentHash))
                    events.Add(new SixelRasterContentDefined(placement.Image));

                events.Add(new SixelRasterPlacementUpdated(placement, IsNewPlacement: true));

                if (placement.IsGeometryOnly)
                {
                    events.Add(new SixelRasterRouteDiagnostic(
                        SixelRasterRouteDiagnosticKind.GeometryOnlyDowngrade,
                        $"Placement {sequence} could not be fully decoded within configured limits; only geometry/anchor information is available.",
                        sequence));
                }
            }
            else if (!previous.Equals(GeometrySnapshot.From(placement)))
            {
                events.Add(new SixelRasterPlacementUpdated(placement, IsNewPlacement: false));
            }
        }

        foreach (var sequence in _lastVisible.Keys)
        {
            if (!currentBySequence.ContainsKey(sequence))
                events.Add(new SixelRasterPlacementReleased(sequence));
        }

        foreach (var (row, column, width, height) in damagedRegions)
        {
            foreach (var placement in currentPlacements)
            {
                if (placement.Row + placement.PaintedRowOffset > row + height - 1
                    || placement.Row + placement.PaintedRowOffset + placement.PaintedRowCount - 1 < row
                    || placement.Column + placement.PaintedColumnOffset > column + width - 1
                    || placement.Column + placement.PaintedColumnOffset + placement.PaintedColumnCount - 1 < column)
                {
                    continue;
                }

                events.Add(new SixelRasterPlacementDamaged(placement.Sequence, row, column, width, height));
            }
        }

        _lastVisible.Clear();
        foreach (var placement in currentPlacements)
            _lastVisible[placement.Sequence] = GeometrySnapshot.From(placement);

        if (_definedContentHashes.Count > 0)
        {
            var stillReferenced = new HashSet<byte[]>(SixelContentHashComparer.Instance);
            foreach (var placement in currentPlacements)
                stillReferenced.Add(placement.Image.ContentHash);

            List<byte[]>? released = null;
            foreach (var hash in _definedContentHashes)
            {
                if (!stillReferenced.Contains(hash))
                    (released ??= []).Add(hash);
            }

            if (released is not null)
            {
                foreach (var hash in released)
                {
                    _definedContentHashes.Remove(hash);
                    events.Add(new SixelRasterContentReleased(hash));
                }
            }
        }

        return events;
    }
}
