namespace Hex1b.Sixel;

/// <summary>
/// Describes how the effective upstream presentation can render Sixel graphics.
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately separate from Hex1b's own parser/model support for the Sixel
/// protocol (which is unconditional — Hex1b always understands and models Sixel DCS
/// sequences regardless of what sits downstream). <see cref="SixelPresentationSupport"/>
/// instead answers "can the bytes Hex1b would emit actually be turned into pixels for a
/// human to see, one way or another?" That is a property of the effective presentation
/// path, discovered by <see href="https://github.com/mitchdenny/hex1b/issues/455">#455</see>.
/// </para>
/// <para>
/// <see cref="None"/> covers both "not yet probed" and "confirmed unsupported" — the
/// finer distinction between those two lives in probe diagnostics
/// (see <see cref="SixelCapabilityProbeDiagnostics"/>), not in this enum. Workload-facing
/// feature reporting (for example <c>SixelNode</c> deciding whether to advertise Sixel
/// to a hosted app) must treat <see cref="None"/> as "do not advertise," never assume
/// support, and never silently substitute a specific cell size when support itself is
/// unknown.
/// </para>
/// </remarks>
public enum SixelPresentationSupport
{
    /// <summary>
    /// The effective presentation cannot render Sixel graphics, or support has not
    /// been established. Workloads must not be told Sixel is available.
    /// </summary>
    None,

    /// <summary>
    /// The upstream terminal understands the Sixel protocol natively and Hex1b's
    /// Sixel DCS bytes reach it unmodified (raw passthrough).
    /// </summary>
    Native,

    /// <summary>
    /// Sixel graphics are rendered by translating Hex1b's raster output into a
    /// different image protocol (for example Kitty Graphics Protocol or the iTerm2
    /// inline image protocol) before reaching the presentation.
    /// </summary>
    /// <remarks>
    /// Implementing the translation itself is explicitly out of scope for #455 (see
    /// <see href="https://github.com/mitchdenny/hex1b/issues/458">#458</see>); this
    /// value exists so the capability model has a place to record it once that work
    /// lands.
    /// </remarks>
    Translated,

    /// <summary>
    /// There is no real display; Hex1bTerminal's own authoritative screen/graphics
    /// model is the sole source of truth (for example headless test harnesses).
    /// </summary>
    Headless,
}
