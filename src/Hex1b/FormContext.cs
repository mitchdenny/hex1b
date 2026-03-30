namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Builder context for constructing a <see cref="FormWidget"/>.
/// Extends <see cref="WidgetContext{TParentWidget}"/> so standard widget extension methods
/// (e.g. <c>form.Text(...)</c>, <c>form.Separator()</c>) are available inside forms.
/// Form-specific extensions like <c>form.TextField(...)</c> target this type directly.
/// </summary>
public sealed class FormContext : WidgetContext<FormWidget>
{
    /// <summary>
    /// Registry tracking all form fields for cross-field references.
    /// </summary>
    public FormFieldRegistry FieldRegistry { get; } = new();

    internal FormContext() { }
}
