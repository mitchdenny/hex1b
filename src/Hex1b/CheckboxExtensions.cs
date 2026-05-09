using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating CheckboxWidget.
/// </summary>
public static class CheckboxExtensions
{
    /// <summary>
    /// Creates a checkbox with the default unchecked state.
    /// </summary>
    public static CheckboxWidget Checkbox<TParent>(this WidgetContext<TParent> context)
        where TParent : Hex1bWidget
        => new();

    /// <summary>
    /// Creates a checkbox with the specified state.
    /// </summary>
    public static CheckboxWidget Checkbox<TParent>(this WidgetContext<TParent> context, CheckboxState state)
        where TParent : Hex1bWidget
        => new(state);

    /// <summary>
    /// Creates a checkbox with a label.
    /// </summary>
    public static CheckboxWidget Checkbox<TParent>(this WidgetContext<TParent> context, string label)
        where TParent : Hex1bWidget
        => new() { LabelText = label };

    /// <summary>
    /// Creates a checkbox with the specified state and label.
    /// </summary>
    public static CheckboxWidget Checkbox<TParent>(
        this WidgetContext<TParent> context,
        CheckboxState state,
        string label)
        where TParent : Hex1bWidget
        => new(state) { LabelText = label };

    /// <summary>
    /// Creates a checkbox with the specified checked state and label.
    /// </summary>
    public static CheckboxWidget Checkbox<TParent>(
        this WidgetContext<TParent> context,
        bool isChecked,
        string? label = null)
        where TParent : Hex1bWidget
        => new(isChecked ? CheckboxState.Checked : CheckboxState.Unchecked) { LabelText = label };
}
