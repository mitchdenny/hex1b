namespace Hex1b.Widgets;

/// <summary>
/// Internal layout descriptor derived from a font's <c>old_layout</c> and (optionally)
/// <c>full_layout</c> header parameters. Captures default mode and the per-axis rule bits.
/// Not part of the public API; <see cref="FigletFont"/> exposes equivalent information through
/// primitive constructor parameters so this DTO does not leak.
/// </summary>
internal readonly record struct FigletLayoutInfo
{
    /// <summary>Bitmask of horizontal smushing rules (bits 1, 2, 4, 8, 16, 32). 0 means no controlled rules.</summary>
    public int HorizontalSmushingRules { get; init; }

    /// <summary>True if the font requests horizontal smushing as the default layout (full_layout bit 128).</summary>
    public bool HorizontalSmushing { get; init; }

    /// <summary>True if the font requests horizontal fitting/kerning as the default layout (full_layout bit 64).</summary>
    public bool HorizontalFitting { get; init; }

    /// <summary>Bitmask of vertical smushing rules (bits 256, 512, 1024, 2048, 4096), shifted to bits 1..16.</summary>
    public int VerticalSmushingRules { get; init; }

    /// <summary>True if the font requests vertical smushing as the default layout (full_layout bit 16384).</summary>
    public bool VerticalSmushing { get; init; }

    /// <summary>True if the font requests vertical fitting as the default layout (full_layout bit 8192).</summary>
    public bool VerticalFitting { get; init; }
}
