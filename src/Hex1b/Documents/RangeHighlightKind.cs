using Hex1b.Theming;

namespace Hex1b.Documents;

/// <summary>
/// Predefined highlight kinds with distinct theme-aware default colors.
/// </summary>
public enum RangeHighlightKind
{
    /// <summary>General-purpose highlight (e.g., search results).</summary>
    Default,

    /// <summary>Read access of a symbol (e.g., variable reference).</summary>
    ReadAccess,

    /// <summary>Write access of a symbol (e.g., variable assignment).</summary>
    WriteAccess
}
