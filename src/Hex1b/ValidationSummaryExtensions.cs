using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating <see cref="ValidationSummaryWidget"/> instances.
/// </summary>
public static class ValidationSummaryExtensions
{
    /// <summary>
    /// Creates a validation summary that displays all current validation errors from the form.
    /// Each error is rendered as a styled line showing the field label and error message.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.Form(form =>
    /// {
    ///     var name = form.TextField("Name").Validate(v => ...);
    ///     var email = form.TextField("Email").Validate(v => ...);
    ///     return [name, email, form.ValidationSummary()];
    /// })
    /// </code>
    /// </example>
    public static ValidationSummaryWidget ValidationSummary(this FormContext ctx)
    {
        // Build field ID → label mapping from the registry
        var fieldLabels = new Dictionary<string, string>();
        foreach (var fieldId in ctx.FieldRegistry.FieldIds)
        {
            // Extract label from field ID format: "field_N_Label"
            var parts = fieldId.Split('_', 3);
            var label = parts.Length >= 3 ? parts[2] : fieldId;
            fieldLabels[fieldId] = label;
        }

        return new ValidationSummaryWidget { FieldLabels = fieldLabels };
    }
}
