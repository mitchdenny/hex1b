using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="FormWidget"/>.
/// Acts as a panel that delegates layout, rendering, and focus to its content child
/// (a VStack or Grid built internally by the FormWidget).
/// Also tracks form-level state: field values, validation results, and enabled states.
/// </summary>
public sealed class FormNode : Hex1bNode
{
    /// <summary>
    /// The reconciled content child node (VStack or Grid layout).
    /// </summary>
    public Hex1bNode? Content { get; set; }

    /// <summary>
    /// The current label placement mode.
    /// </summary>
    internal LabelPlacement LabelPlacement { get; set; }

    /// <summary>
    /// The label width for inline placement.
    /// </summary>
    internal int LabelWidth { get; set; }

    /// <summary>
    /// The field registry for cross-field references.
    /// </summary>
    internal FormFieldRegistry? FieldRegistry { get; set; }

    /// <summary>
    /// Current field values keyed by field ID.
    /// </summary>
    internal Dictionary<string, string> FieldValues { get; } = new();

    /// <summary>
    /// Current validation results keyed by field ID.
    /// </summary>
    internal Dictionary<string, ValidationResult> ValidationResults { get; } = new();

    /// <summary>
    /// Current enabled states keyed by field ID.
    /// </summary>
    internal Dictionary<string, bool> EnabledStates { get; } = new();

    protected override Size MeasureCore(Constraints constraints)
    {
        if (Content == null)
            return constraints.Constrain(Size.Zero);

        return Content.Measure(constraints);
    }

    protected override void ArrangeCore(Rect rect)
    {
        base.ArrangeCore(rect);
        Content?.Arrange(rect);
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Content != null)
        {
            context.RenderChild(Content);
        }
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Content != null)
        {
            foreach (var focusable in Content.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Content != null)
            yield return Content;
    }

    /// <summary>
    /// Updates the value for a field, runs validation if needed.
    /// </summary>
    internal void SetFieldValue(string fieldId, string value)
    {
        FieldValues[fieldId] = value;
    }

    /// <summary>
    /// Sets the validation result for a field.
    /// </summary>
    internal void SetValidationResult(string fieldId, ValidationResult result)
    {
        ValidationResults[fieldId] = result;
    }

    /// <summary>
    /// Gets the validation result for a field, or Valid if not yet validated.
    /// </summary>
    internal ValidationResult GetValidationResult(string fieldId)
    {
        return ValidationResults.TryGetValue(fieldId, out var result) ? result : ValidationResult.Valid;
    }

    /// <summary>
    /// Gets the current value for a field, or empty string if not set.
    /// </summary>
    internal string GetFieldValue(string fieldId)
    {
        return FieldValues.TryGetValue(fieldId, out var value) ? value : "";
    }

    /// <summary>
    /// Checks whether all specified fields are valid.
    /// </summary>
    internal bool AreFieldsValid(IEnumerable<string> fieldIds)
    {
        foreach (var fieldId in fieldIds)
        {
            if (ValidationResults.TryGetValue(fieldId, out var result) && !result.IsValid)
                return false;
        }
        return true;
    }
}
