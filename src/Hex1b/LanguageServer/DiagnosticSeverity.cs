using Hex1b.Documents;

namespace Hex1b.LanguageServer;

/// <summary>
/// Severity level of a diagnostic, matching LSP specification values.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>A compile error or fatal problem.</summary>
    Error = 1,
    /// <summary>A warning that may indicate a problem.</summary>
    Warning = 2,
    /// <summary>An informational message.</summary>
    Information = 3,
    /// <summary>A hint or suggestion.</summary>
    Hint = 4,
}
