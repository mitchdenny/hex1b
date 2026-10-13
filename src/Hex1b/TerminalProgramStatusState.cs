namespace Hex1b;

/// <summary>Describes the state reported by a program using OSC 7501.</summary>
public enum TerminalProgramStatusState
{
    /// <summary>Waiting for the user's next instruction.</summary>
    Idle,
    /// <summary>Running independently.</summary>
    Working,
    /// <summary>Waiting for user action before continuing.</summary>
    Blocked,
    /// <summary>Finished successfully with a result ready to inspect.</summary>
    Done,
    /// <summary>Failed and stopped.</summary>
    Error
}
