namespace Hex1b.Widgets;

/// <summary>
/// The three discrete values a checkbox can hold. This is the value layer of the
/// <see cref="CheckboxState"/> model.
/// </summary>
public enum CheckboxValue
{
    /// <summary>The checkbox is unchecked.</summary>
    Unchecked,

    /// <summary>The checkbox is checked.</summary>
    Checked,

    /// <summary>
    /// The checkbox is in an indeterminate state (partially checked).
    /// Used when a parent represents a group with mixed selection.
    /// </summary>
    Indeterminate,
}
