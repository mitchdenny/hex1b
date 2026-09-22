using Hex1b.Theming;

namespace Hex1b.Documents;

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
