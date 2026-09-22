using Hex1b.Tokens;

namespace Hex1b.Sixel;

/// <summary>
/// The authoritative outcome of parsing a Sixel DCS sequence.
/// </summary>
/// <remarks>
/// Automation code can use this to assert whether a graphic parsed cleanly or
/// degraded, without inspecting raw diagnostics. See
/// <see cref="Hex1b.SixelData.Outcome"/> and <see cref="Hex1b.SixelData.Diagnostics"/>
/// for the explanatory detail behind a non-<see cref="Complete"/> outcome.
/// </remarks>
public enum SixelParseOutcome
{
    /// <summary>The sequence parsed to completion with no downgrades.</summary>
    Complete,

    /// <summary>The upstream stream cancelled the sequence before it terminated.</summary>
    Cancelled,

    /// <summary>The sequence was structurally invalid.</summary>
    Malformed,

    /// <summary>The sequence parsed, but bounded retention limits downgraded it.</summary>
    LimitDowngraded,

    /// <summary>The DCS introducer was not an accepted Sixel form.</summary>
    Rejected,
}
