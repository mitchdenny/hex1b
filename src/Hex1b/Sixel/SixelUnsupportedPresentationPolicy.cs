namespace Hex1b.Sixel;

/// <summary>
/// Governs what happens to Sixel graphics when the effective presentation route is
/// <c>Unsupported</c> — no real display can render Sixel, no managed raster sink is
/// attached, and no translation target (currently only KGP) is available.
/// </summary>
/// <remarks>
/// Hex1b's authoritative Sixel model (<see cref="Hex1b.SixelGraphicsState"/>,
/// placements, snapshots) is never discarded regardless of this policy — it governs
/// only what, if anything, is written to the presentation in place of the graphics a
/// human cannot see. See
/// <see href="https://github.com/mitchdenny/hex1b/issues/458">#458</see>.
/// </remarks>
public enum SixelUnsupportedPresentationPolicy
{
    /// <summary>
    /// Emit no substitute output for unsupported Sixel content beyond the raw bytes
    /// that byte-exact passthrough already forwards (which most real terminals and
    /// harnesses silently ignore as an unrecognized DCS sequence). This is the
    /// default and preserves today's behavior exactly.
    /// </summary>
    Suppress,

    /// <summary>
    /// Write a short, human-readable diagnostic placeholder to the presentation in
    /// place of each Sixel graphic that cannot be rendered (for example
    /// <c>[sixel: 320x200 image not shown — presentation cannot display graphics]</c>),
    /// so a user watching an unsupported presentation is not left wondering whether
    /// something silently failed.
    /// </summary>
    Placeholder,
}
