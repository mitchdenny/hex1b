using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Hex1b.Automation;

/// <summary>
/// Options for Find pattern methods.
/// </summary>
public readonly struct FindOptions
{
    /// <summary>
    /// Default options: cursor at end of match, match cells included.
    /// </summary>
    public static readonly FindOptions Default = new(FindCursorPosition.End, true);
    
    private readonly FindCursorPosition _cursorPosition;
    private readonly bool _includeMatchInCells;
    private readonly bool _isExplicitlySet;
    
    /// <summary>
    /// Where to leave the cursor after matching.
    /// Default is End (after the match) for natural pattern chaining.
    /// </summary>
    public FindCursorPosition CursorPosition => _isExplicitlySet ? _cursorPosition : FindCursorPosition.End;
    
    /// <summary>
    /// Whether to include the matched cells in the result.
    /// When default-initialized, returns true (the default behavior).
    /// </summary>
    public bool IncludeMatchInCells => _isExplicitlySet ? _includeMatchInCells : true;
    
    /// <summary>
    /// Creates FindOptions with the specified settings.
    /// </summary>
    public FindOptions(FindCursorPosition cursorPosition = FindCursorPosition.End, bool includeMatchInCells = true)
    {
        _cursorPosition = cursorPosition;
        _includeMatchInCells = includeMatchInCells;
        _isExplicitlySet = true;
    }
}
