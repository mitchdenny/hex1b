namespace Hex1b.Automation;

/// <summary>Identifies the stage that produced a Tape diagnostic.</summary>
public enum TapeDiagnosticStage
{
    /// <summary>Recognizing source syntax.</summary>
    Parsing,
    /// <summary>Resolving source includes.</summary>
    Resolution,
    /// <summary>Preparing command results and input sequences.</summary>
    Compilation,
    /// <summary>Validating execution capabilities and configuration.</summary>
    Preflight,
    /// <summary>Executing commands.</summary>
    Execution,
    /// <summary>Capturing execution artifacts.</summary>
    Capture
}
