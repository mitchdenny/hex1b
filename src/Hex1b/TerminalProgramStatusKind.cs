namespace Hex1b;

/// <summary>Describes the action needed by a blocked program.</summary>
public enum TerminalProgramStatusKind
{
    /// <summary>Approval to perform an operation.</summary>
    Permission,
    /// <summary>An answer to a question.</summary>
    Question,
    /// <summary>Authentication or credentials.</summary>
    Auth
}
