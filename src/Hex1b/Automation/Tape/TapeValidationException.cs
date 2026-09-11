namespace Hex1b.Automation;

/// <summary>Reports a tape rejected before any execution begins.</summary>
public sealed class TapeValidationException : Exception
{
    internal TapeValidationException(IEnumerable<TapeDiagnostic> diagnostics)
        : this(diagnostics.ToArray()) { }

    private TapeValidationException(TapeDiagnostic[] diagnostics)
        : base(string.Join(Environment.NewLine, diagnostics.Select(d => d.Message)))
        => Diagnostics = Array.AsReadOnly(diagnostics);

    /// <summary>Gets source-aware reasons why the tape cannot execute.</summary>
    public IReadOnlyList<TapeDiagnostic> Diagnostics { get; }
}
