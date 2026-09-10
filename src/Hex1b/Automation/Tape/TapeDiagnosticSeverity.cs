namespace Hex1b.Automation;

/// <summary>Indicates the importance of a Tape diagnostic.</summary>
public enum TapeDiagnosticSeverity
{
    /// <summary>Provides additional information.</summary>
    Information,
    /// <summary>Identifies a non-fatal issue.</summary>
    Warning,
    /// <summary>Identifies a failure.</summary>
    Error
}
