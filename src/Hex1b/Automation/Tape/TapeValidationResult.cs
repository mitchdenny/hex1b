namespace Hex1b.Automation;

/// <summary>Contains preparation diagnostics without executing a tape.</summary>
public sealed class TapeValidationResult
{
    internal TapeValidationResult(IEnumerable<TapeDiagnostic> diagnostics)
        => Diagnostics = Array.AsReadOnly(diagnostics.ToArray());

    /// <summary>Gets all discovered preparation diagnostics, including errors returned by command callbacks.</summary>
    public IReadOnlyList<TapeDiagnostic> Diagnostics { get; }

    /// <summary>Gets whether preparation found no errors.</summary>
    public bool CanExecute => !Diagnostics.Any(d => d.Severity == TapeDiagnosticSeverity.Error);
}
