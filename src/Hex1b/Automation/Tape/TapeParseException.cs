namespace Hex1b.Automation;

/// <summary>Indicates that Tape source contains syntax errors.</summary>
public sealed class TapeParseException : Exception
{
    /// <summary>Creates an exception with an immutable diagnostic snapshot.</summary>
    /// <param name="diagnostics">The parsing diagnostics.</param>
    public TapeParseException(IEnumerable<TapeDiagnostic> diagnostics)
        : base("The Tape source contains syntax errors.")
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>Gets all diagnostics from the failed parse.</summary>
    public IReadOnlyList<TapeDiagnostic> Diagnostics { get; }
}
