#pragma warning disable HEX1B_SIXEL // Internal Sixel presentation integration.
using Hex1b.Input;
using Hex1b.Tokens;
using Hex1b.Automation;

namespace Hex1b;

/// <summary>
/// Represents the lifecycle state of a terminal session.
/// </summary>
public enum TerminalState
{
    /// <summary>
    /// The terminal session has not started yet.
    /// </summary>
    NotStarted,
    
    /// <summary>
    /// The terminal session is currently running.
    /// </summary>
    Running,
    
    /// <summary>
    /// The terminal session has completed (process exited).
    /// </summary>
    Completed
}
