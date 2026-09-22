using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Specifies where to position the cursor after a Find match.
/// </summary>
public enum FindCursorPosition
{
    /// <summary>
    /// Position cursor at the start of the match (first character).
    /// </summary>
    Start,
    
    /// <summary>
    /// Position cursor at the end of the match (last character).
    /// </summary>
    End
}
