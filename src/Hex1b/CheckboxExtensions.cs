using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating CheckboxWidget.
/// </summary>
public static class CheckboxExtensions
{
    /// <summary>
    /// Creates a checkbox with the default unchecked value.
    /// </summary>
    public static CheckboxWidget Checkbox<TParent>(this WidgetContext<TParent> context)
        where TParent : Hex1bWidget
        => new();

    /// <summary>
    /// Creates a checkbox with the specified initial value.
    /// </summary>
    public static CheckboxWidget Checkbox<TParent>(this WidgetContext<TParent> context, CheckboxValue value)
        where TParent : Hex1bWidget
        => new(value);

    /// <summary>
    /// Creates a checkbox with a label.
    /// </summary>
    public static CheckboxWidget Checkbox<TParent>(this WidgetContext<TParent> context, string label)
        where TParent : Hex1bWidget
        => new() { LabelText = label };

    /// <summary>
    /// Creates a checkbox with the specified initial value and label.
    /// </summary>
    public static CheckboxWidget Checkbox<TParent>(
        this WidgetContext<TParent> context,
        CheckboxValue value,
        string label)
        where TParent : Hex1bWidget
        => new(value) { LabelText = label };

    /// <summary>
    /// Creates a checkbox with the specified checked value and label.
    /// </summary>
    public static CheckboxWidget Checkbox<TParent>(
        this WidgetContext<TParent> context,
        bool isChecked,
        string? label = null)
        where TParent : Hex1bWidget
        => new(isChecked ? CheckboxValue.Checked : CheckboxValue.Unchecked) { LabelText = label };
}
