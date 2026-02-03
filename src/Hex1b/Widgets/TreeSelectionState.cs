namespace Hex1b.Widgets;

/// <summary>
/// Specifies the selection state of a tree item in multi-select mode with cascade selection.
/// </summary>
public enum TreeSelectionState
{
    /// <summary>
    /// The item is not selected (and no children are selected if it's a parent).
    /// </summary>
    None,
    
    /// <summary>
    /// The item is fully selected (and all children are selected if it's a parent).
    /// </summary>
    Selected,
    
    /// <summary>
    /// The item has some (but not all) children selected.
    /// This state only applies to parent items when cascade selection is enabled.
    /// </summary>
    Indeterminate
}
