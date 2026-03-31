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

    /// <summary>
    /// Gets all current validation errors across all fields in the form.
    /// Keys are field IDs, values are the <see cref="ValidationResult"/> for each field.
    /// Only fields with validation errors are included.
    /// </summary>
    /// <remarks>
    /// This property reads from the live <see cref="Nodes.FormNode"/> state during rendering.
    /// It is populated after form fields have been reconciled and validated.
    /// Use this to build custom error displays, or use <c>form.ValidationSummary()</c> for a default rendering.
    /// </remarks>
    public IReadOnlyDictionary<string, ValidationResult> ValidationErrors
    {
        get
        {
            if (_formNode == null)
                return new Dictionary<string, ValidationResult>();

            return _formNode.ValidationResults
                .Where(kv => !kv.Value.IsValid)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }
    }

    /// <summary>
    /// Gets all current validation results (both valid and invalid) across all fields.
    /// </summary>
    public IReadOnlyDictionary<string, ValidationResult> ValidationResults
    {
        get
        {
            if (_formNode == null)
                return new Dictionary<string, ValidationResult>();

            return new Dictionary<string, ValidationResult>(_formNode.ValidationResults);
        }
    }

    /// <summary>
    /// Reference to the live FormNode for accessing validation state.
    /// Set during reconciliation.
    /// </summary>
    internal Nodes.FormNode? _formNode;

    internal FormContext() { }
}
