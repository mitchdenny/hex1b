namespace Hex1b.Widgets;

/// <summary>
/// Represents the visual state of a checkbox.
/// </summary>
public enum CheckboxState
{
    /// <summary>
    /// The checkbox is unchecked.
    /// </summary>
    Unchecked,
    
    /// <summary>
    /// The checkbox is checked.
    /// </summary>
    Checked,
    
    /// <summary>
    /// The checkbox is in an indeterminate state (partially checked).
    /// Used when a parent represents a group with mixed selection.
    /// </summary>
    Indeterminate
}
