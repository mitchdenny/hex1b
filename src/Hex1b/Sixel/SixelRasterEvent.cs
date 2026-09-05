using Hex1b;

namespace Hex1b.Sixel;

/// <summary>
/// Identifies the kind of diagnostic carried by a <see cref="SixelRasterRouteDiagnostic"/> event.
/// </summary>
public enum SixelRasterRouteDiagnosticKind
{
    /// <summary>
    /// A placement's raster could not be fully decoded (bounded resource limits were
    /// exceeded) and only geometry/anchor information is available. See
    /// <see cref="Hex1b.SixelPlacement.IsGeometryOnly"/>.
    /// </summary>
    GeometryOnlyDowngrade,

    /// <summary>
    /// A Sixel DCS sequence exceeded the framer's bounded retention limit before even
    /// geometry could be parsed. No placement was created for this sequence; the
    /// terminal recovers at the next valid boundary, and following text/cursor state
    /// may be desynchronized until then.
    /// </summary>
    Desynchronized,

    /// <summary>
    /// An opt-in <see cref="SixelSanitizationPolicy"/> suppressed a Sixel sequence
    /// before it reached a native upstream presentation.
    /// </summary>
    Suppressed,

    /// <summary>
    /// An opt-in <see cref="SixelUnsupportedPresentationPolicy"/> substituted a
    /// diagnostic placeholder for a Sixel sequence because the effective presentation
    /// cannot render Sixel and no translation route is available.
    /// </summary>
    PlaceholderApplied,

    /// <summary>
    /// <see cref="Sixel.SixelPresentationSupport.Translated"/> was selected but Hex1b
    /// implements no Sixel-to-image-protocol translation (an explicit non-goal; see
    /// <see href="https://github.com/mitchdenny/hex1b/issues/458">#458</see>), so the
    /// unsupported-presentation policy was applied instead.
    /// </summary>
    TranslationUnavailable,
}

/// <summary>
/// Base type for an ordered, protocol-neutral raster event delivered to a managed
/// presentation through <see cref="ISixelRasterPresentationSink"/>.
/// </summary>
/// <remarks>
/// <para>
/// This event model is deliberately built around the same public, protocol-neutral
/// types <see href="https://github.com/mitchdenny/hex1b/issues/456">#456</see>
/// already promoted for snapshots — <see cref="Hex1b.SixelData"/> (content identity,
/// pixels, geometry-only/outcome diagnostics) and <see cref="Hex1b.SixelPlacement"/>
/// (placement geometry, painted crop, sequence, damage). No new raster representation
/// is introduced; a managed presentation that wants to render Sixel graphics without
/// reparsing the wire protocol consumes this event stream instead.
/// </para>
/// <para>
/// Events for a single output batch are delivered in a single ordered list via
/// <see cref="ISixelRasterPresentationSink.OnSixelRasterEventsAsync"/>, interleaved
/// with (never ahead of) the corresponding text/cell output for that same batch —
/// see <see href="https://github.com/mitchdenny/hex1b/issues/458">#458</see>.
/// </para>
/// </remarks>
public abstract record SixelRasterEvent
{
    private protected SixelRasterEvent()
    {
    }
}

/// <summary>
/// Raised the first time a raster's content (keyed by <see cref="Hex1b.SixelData.ContentHash"/>)
/// becomes reachable from a live placement. A given content hash is defined at most once
/// per screen lifetime (until released and re-referenced) — content already known to the
/// sink is never retransmitted.
/// </summary>
/// <param name="Image">The raster content. Never mutated after this event is raised.</param>
public sealed record SixelRasterContentDefined(SixelData Image) : SixelRasterEvent;

/// <summary>
/// Raised when a placement is created, or when a previously observed placement's
/// geometry changes (for example due to scrolling, history eviction, reflow, or
/// margin clipping). Placement identity across events is <see cref="Hex1b.SixelPlacement.Sequence"/>.
/// </summary>
/// <param name="Placement">The placement's current geometry and content reference.</param>
/// <param name="IsNewPlacement">
/// <see langword="true"/> the first time this placement's <see cref="Hex1b.SixelPlacement.Sequence"/>
/// is observed; <see langword="false"/> for a subsequent geometry update of the same placement.
/// </param>
public sealed record SixelRasterPlacementUpdated(SixelPlacement Placement, bool IsNewPlacement) : SixelRasterEvent;

/// <summary>
/// Raised when text output or another operation destructively damages pixels within
/// a still-live placement's painted region, without removing the placement itself.
/// </summary>
/// <param name="PlacementSequence">Identifies the damaged placement (<see cref="Hex1b.SixelPlacement.Sequence"/>).</param>
/// <param name="Row">The top row of the damaged region, in the same coordinate space as <see cref="Hex1b.SixelPlacement.Row"/>.</param>
/// <param name="Column">The left column of the damaged region.</param>
/// <param name="Width">The damaged region's width in columns.</param>
/// <param name="Height">The damaged region's height in rows.</param>
public sealed record SixelRasterPlacementDamaged(
    long PlacementSequence,
    int Row,
    int Column,
    int Width,
    int Height) : SixelRasterEvent;

/// <summary>
/// Raised when a placement is no longer part of the active screen's live placement
/// set — for example because it scrolled out of retained history, was erased, or the
/// screen was reset or switched.
/// </summary>
/// <param name="PlacementSequence">Identifies the released placement (<see cref="Hex1b.SixelPlacement.Sequence"/>).</param>
public sealed record SixelRasterPlacementReleased(long PlacementSequence) : SixelRasterEvent;

/// <summary>
/// Raised when a raster's content is no longer referenced by any live placement,
/// mirroring the reachability sweep <see cref="Hex1b.SixelData"/>'s backing store
/// already performs. A sink may release any resources it associated with this
/// content hash.
/// </summary>
/// <param name="ContentHash">The content hash being released (<see cref="Hex1b.SixelData.ContentHash"/>).</param>
public sealed record SixelRasterContentReleased(byte[] ContentHash) : SixelRasterEvent;

/// <summary>
/// Identifies the direction of a <see cref="SixelRasterScreenTransition"/>.
/// </summary>
public enum SixelRasterScreenTransitionKind
{
    /// <summary>The terminal switched into the alternate screen.</summary>
    EnteredAlternateScreen,

    /// <summary>The terminal switched back to the main screen.</summary>
    ExitedAlternateScreen,
}

/// <summary>
/// Raised when the active screen changes. Placements belonging to the screen being
/// left are released (each with its own <see cref="SixelRasterPlacementReleased"/>);
/// placements already live on the screen being entered are (re)announced with fresh
/// <see cref="SixelRasterPlacementUpdated"/> events, since the sink cannot be assumed
/// to have retained per-screen state across the switch.
/// </summary>
/// <param name="Kind">The direction of the transition.</param>
public sealed record SixelRasterScreenTransition(SixelRasterScreenTransitionKind Kind) : SixelRasterEvent;

/// <summary>
/// Raised when a full terminal reset (RIS) clears all Sixel graphics state.
/// Individual <see cref="SixelRasterPlacementReleased"/>/<see cref="SixelRasterContentReleased"/>
/// events are not additionally raised for a reset; this single event means "everything
/// this sink previously knew about the active screen's Sixel graphics is gone."
/// </summary>
public sealed record SixelRasterReset : SixelRasterEvent;

/// <summary>
/// Carries an explicit, non-silent diagnostic about degraded or policy-affected
/// Sixel handling. See <see cref="SixelRasterRouteDiagnosticKind"/> for the situations
/// this reports.
/// </summary>
/// <param name="Kind">The kind of diagnostic.</param>
/// <param name="Message">A human-readable description, safe to log or display.</param>
/// <param name="PlacementSequence">
/// The affected placement's <see cref="Hex1b.SixelPlacement.Sequence"/>, when the
/// diagnostic is specific to one placement; <see langword="null"/> otherwise.
/// </param>
public sealed record SixelRasterRouteDiagnostic(
    SixelRasterRouteDiagnosticKind Kind,
    string Message,
    long? PlacementSequence = null) : SixelRasterEvent;
