using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating validation message widgets inside forms.
/// </summary>
public static class ValidationMessageExtensions
{
    /// <summary>
    /// Creates a validation message widget that displays the first error from the specified fields.
    /// </summary>
    /// <param name="ctx">The form context.</param>
    /// <param name="fields">The form fields to monitor for validation errors.</param>
    public static ValidationMessageWidget ValidationMessageFor(
        this FormContext ctx,
        params FormTextFieldWidget[] fields)
        => new(fields.Select(f => f.FieldId).ToList());
}
