using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating form text fields.
/// </summary>
public static class FormTextFieldExtensions
{
    /// <summary>
    /// Creates a text field for the form with the specified label.
    /// Returns a <see cref="FormTextFieldWidget"/> that can be configured with fluent methods
    /// and also serves as a field handle for cross-field references.
    /// </summary>
    /// <example>
    /// <code>
    /// var name = form.TextField("Name").WithMinWidth(20).Validate(v => ...);
    /// </code>
    /// </example>
    public static FormTextFieldWidget TextField(this FormContext ctx, string label)
    {
        var fieldId = ctx.FieldRegistry.RegisterField(label);
        return new FormTextFieldWidget(fieldId, label);
    }
}
