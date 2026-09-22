using Hex1b.Tokens;

namespace Hex1b.Sixel;

/// <summary>
/// Explicit reasons a Sixel parse degraded or was annotated, surfaced via
/// <see cref="SixelDiagnostic"/> on <see cref="Hex1b.SixelData.Diagnostics"/>.
/// </summary>
public enum SixelDiagnosticCode
{
    /// <summary>The DCS introducer is not an accepted Sixel form.</summary>
    RejectedIntroducer,

    /// <summary>The header carried more parameters than the policy allows.</summary>
    ExcessiveHeaderParameters,

    /// <summary>The pixel-aspect-ratio macro is not one this parser supports.</summary>
    UnsupportedAspectMacro,

    /// <summary>A byte outside the accepted Sixel alphabet was encountered.</summary>
    InvalidByte,

    /// <summary>A command ended before it was fully specified.</summary>
    IncompleteCommand,

    /// <summary>A later command replaced an earlier, conflicting one.</summary>
    ReplacedCommand,

    /// <summary>A command carried more parameters than the policy allows.</summary>
    ExcessiveCommandParameters,

    /// <summary>A numeric parameter exceeded the implementation's coordinate limit.</summary>
    NumericLimitExceeded,

    /// <summary>Geometry accumulation saturated at the coordinate limit.</summary>
    GeometrySaturated,

    /// <summary>The raster attributes (DECGRA) command was invalid.</summary>
    InvalidRasterAttributes,

    /// <summary>A palette definition or selection command was invalid.</summary>
    InvalidPaletteCommand,

    /// <summary>A bounded metadata limit (e.g. palette entries) was exceeded.</summary>
    MetadataLimitExceeded,

    /// <summary>Bounded command retention truncated the remaining sequence.</summary>
    CommandRetentionLimitExceeded,

    /// <summary>The DCS sequence ended before a string terminator.</summary>
    UnterminatedSequence,

    /// <summary>The DCS sequence was cancelled by CAN or SUB.</summary>
    CancelledSequence,

    /// <summary>
    /// The retained DCS byte limit was reached. Geometry observation continued,
    /// but the complete payload and raster command stream are unavailable.
    /// </summary>
    RetainedContentLimitExceeded,
}
