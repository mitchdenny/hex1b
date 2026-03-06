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

/// <summary>
/// A diagnostic message for a specific location in a document.
/// </summary>
/// <param name="DocumentUri">The URI of the document this diagnostic applies to.</param>
/// <param name="Message">The diagnostic message text.</param>
/// <param name="Severity">The severity level.</param>
/// <param name="Start">Start position (1-based line and column).</param>
/// <param name="End">End position (1-based line and column).</param>
/// <param name="Source">Optional source identifier (e.g., "typescript", "eslint").</param>
/// <param name="Code">Optional diagnostic code.</param>
public record DiagnosticInfo(
    string DocumentUri,
    string Message,
    DiagnosticSeverity Severity,
    DocumentPosition Start,
    DocumentPosition End,
    string? Source = null,
    string? Code = null)
{
    /// <summary>The file name portion of the document URI.</summary>
    public string FileName => System.IO.Path.GetFileName(
        DocumentUri.StartsWith("file://", StringComparison.Ordinal)
            ? DocumentUri["file://".Length..]
            : DocumentUri);

    /// <summary>A short severity label for display.</summary>
    public string SeverityLabel => Severity switch
    {
        DiagnosticSeverity.Error => "Error",
        DiagnosticSeverity.Warning => "Warning",
        DiagnosticSeverity.Information => "Info",
        DiagnosticSeverity.Hint => "Hint",
        _ => "Unknown",
    };

    /// <summary>An icon representing the severity.</summary>
    public string SeverityIcon => Severity switch
    {
        DiagnosticSeverity.Error => "🔴",
        DiagnosticSeverity.Warning => "🟡",
        DiagnosticSeverity.Information => "🔵",
        DiagnosticSeverity.Hint => "💡",
        _ => "·",
    };
}
