namespace Hex1b.Sixel;

/// <summary>
/// Governs what an explicit, opt-in host policy does with malformed, rejected,
/// oversized, or limit-downgraded Sixel data before it would otherwise reach a native
/// upstream presentation unchanged.
/// </summary>
/// <remarks>
/// <para>
/// The default remains byte-exact immediate passthrough — nothing here is enabled
/// unless a host explicitly opts in via <see cref="Hex1bTerminalOptions.SixelSanitization"/>.
/// </para>
/// <para>
/// Enabling sanitization is honestly incompatible with immediate forwarding: deciding
/// whether a Sixel DCS sequence should be suppressed requires knowing its final
/// outcome, which is only known once the sequence terminates. When sanitization is
/// enabled, Hex1b buffers bytes for the duration of any in-progress DCS string
/// (Sixel or not — the byte stream framer cannot know a sequence's final dispatch
/// byte until it terminates) and flushes them verbatim, suppresses them, or replaces
/// them, once the outcome is known. This trades immediate forwarding latency,
/// bounded by <see cref="Hex1b.Sixel.SixelCompatibilityPolicy"/>'s existing retention
/// limits, for the ability to filter unwanted content. Ordinary text and DCS
/// sequences unrelated to Sixel are never buffered or affected.
/// </para>
/// </remarks>
public sealed record SixelSanitizationPolicy
{
    /// <summary>
    /// A policy that performs no sanitization: Sixel data is always forwarded
    /// byte-exact and immediately, exactly as if this policy did not exist. This is
    /// the default.
    /// </summary>
    public static readonly SixelSanitizationPolicy Disabled = new();

    /// <summary>
    /// Whether sanitization is active. When <see langword="false"/> (the default),
    /// every other property on this record is ignored and passthrough behaves exactly
    /// as it does with no policy configured at all.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Suppress a Sixel DCS sequence whose framing was cancelled (aborted mid-string)
    /// or left unterminated. Defaults to <see langword="true"/> when <see cref="Enabled"/>.
    /// </summary>
    public bool SuppressCancelledOrUnterminated { get; init; } = true;

    /// <summary>
    /// Suppress a Sixel DCS sequence whose content was malformed. Defaults to
    /// <see langword="true"/> when <see cref="Enabled"/>.
    /// </summary>
    public bool SuppressMalformed { get; init; } = true;

    /// <summary>
    /// Suppress a Sixel DCS sequence whose byte stream exceeded the framer's bounded
    /// retention limit (see <see cref="Hex1b.Sixel.SixelCompatibilityPolicy"/>) before
    /// it could be fully retained. Defaults to <see langword="true"/> when <see cref="Enabled"/>.
    /// </summary>
    public bool SuppressRetentionLimitExceeded { get; init; } = true;

    /// <summary>
    /// Suppress a Sixel DCS sequence that parsed successfully but was downgraded to
    /// geometry-only raster output because it exceeded a bounded resource limit
    /// (see <see cref="Hex1b.SixelPlacement.IsGeometryOnly"/>). Defaults to
    /// <see langword="false"/> when <see cref="Enabled"/> — a geometry-only
    /// placement is a legitimate, non-malformed outcome, so hosts must opt in
    /// separately to also suppress it.
    /// </summary>
    public bool SuppressGeometryOnly { get; init; }

    /// <summary>
    /// Creates a copy of <see cref="Disabled"/> with <see cref="Enabled"/> set to
    /// <see langword="true"/> and the default suppression choices described on each
    /// property.
    /// </summary>
    public static SixelSanitizationPolicy Enable() => Disabled with { Enabled = true };
}
