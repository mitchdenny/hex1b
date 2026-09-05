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
/// <see cref="Unknown"/> and <see cref="None"/> are deliberately distinct values, not a
/// single collapsed "no support" state: <see cref="Unknown"/> means discovery has not
/// run, has not concluded, or could not reach a confident answer (for example a probe
/// that timed out or returned a malformed reply); <see cref="None"/> means discovery
/// (or a direct declaration) affirmatively concluded the effective presentation cannot
/// render Sixel. Workload-facing feature reporting (for example <c>SixelNode</c>
/// deciding whether to advertise Sixel to a hosted app) must treat both
/// <see cref="Unknown"/> and <see cref="None"/> as "do not advertise" — never assume
/// support, and never silently substitute a specific cell size when support itself is
/// unknown — but code that needs to tell "never checked" apart from "checked and no"
/// (for example diagnostics or a support-status display) can rely on this enum alone,
/// without also inspecting <see cref="SixelCapabilityProbeDiagnostics"/>.
/// </para>
/// <para>
/// <see cref="Unknown"/> is the type's default value (numeric 0), so a
/// <see cref="TerminalCapabilities"/> instance nobody has explicitly configured reads
/// as "unknown," never as "confirmed unsupported."
/// </para>
/// </remarks>
public enum SixelPresentationSupport
{
    /// <summary>
    /// Whether the effective presentation can render Sixel graphics has not been
    /// established: discovery has not run yet, has not concluded, or a probe timed
    /// out or returned a reply that could not be parsed. Workloads must not be told
    /// Sixel is available, but this is distinct from <see cref="None"/> — nothing has
    /// been confirmed either way.
    /// </summary>
    Unknown,

    /// <summary>
    /// Discovery (or a direct declaration) affirmatively concluded that the effective
    /// presentation cannot render Sixel graphics. Workloads must not be told Sixel is
    /// available. Distinct from <see cref="Unknown"/> — this is a confirmed negative
    /// answer, not an absence of an answer.
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
