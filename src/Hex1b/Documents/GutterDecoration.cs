using Hex1b.Theming;

namespace Hex1b.Documents;

/// <summary>
/// An icon or marker displayed in the editor gutter at a specific line.
/// Used for breakpoints, diagnostic indicators, code action lightbulbs, etc.
/// </summary>
/// <param name="Line">The 1-based document line number.</param>
/// <param name="Character">The character to display (e.g., '●', '💡', '▶').</param>
/// <param name="Kind">The kind of decoration, determining default styling.</param>
public record GutterDecoration(
    int Line,
    char Character,
    GutterDecorationKind Kind = GutterDecorationKind.Default)
{
    /// <summary>Optional explicit foreground color. Overrides the kind default.</summary>
    public Hex1bColor? Foreground { get; init; }
}

/// <summary>
/// Predefined gutter decoration kinds with distinct theme-aware colors.
/// </summary>
public enum GutterDecorationKind
{
    /// <summary>General-purpose marker.</summary>
    Default,

    /// <summary>Error indicator (e.g., diagnostic error).</summary>
    Error,

    /// <summary>Warning indicator (e.g., diagnostic warning).</summary>
    Warning,

    /// <summary>Information indicator (e.g., code action available).</summary>
    Info
}
