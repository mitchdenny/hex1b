using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// Displays validation error messages for one or more form fields.
/// Place this widget inside a Form builder to show aggregated validation messages.
/// </summary>
/// <remarks>
/// <para>
/// Use <c>form.ValidationMessageFor(field1, field2)</c> to create this widget.
/// It shows the first error message from the specified fields, styled with the
/// <see cref="FormTheme.ValidationErrorColor"/> theme element.
/// </para>
/// </remarks>
public sealed record ValidationMessageWidget : Hex1bWidget
{
    /// <summary>
    /// The field IDs to show validation messages for.
    /// </summary>
    internal IReadOnlyList<string> FieldIds { get; }

    public ValidationMessageWidget(IReadOnlyList<string> fieldIds)
    {
        FieldIds = fieldIds;
    }

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ValidationMessageNode ?? new ValidationMessageNode();
        node.FieldIds = FieldIds;

        // Find the FormNode to get validation results
        var formNode = context.FindAncestor<FormNode>();
        string? errorMessage = null;

        if (formNode != null)
        {
            foreach (var fieldId in FieldIds)
            {
                var result = formNode.GetValidationResult(fieldId);
                if (!result.IsValid)
                {
                    errorMessage = result.ErrorMessage;
                    break;
                }
            }
        }

        // Build the error message widget (or empty if all valid)
        Hex1bWidget? messageWidget = null;
        if (errorMessage != null)
        {
            messageWidget = new ThemePanelWidget(
                t => t.Set(GlobalTheme.ForegroundColor, t.Get(FormTheme.ValidationErrorColor)),
                new TextBlockWidget(errorMessage));
        }

        node.MessageChild = messageWidget != null
            ? await context.ReconcileChildAsync(node.MessageChild, messageWidget, node)
            : null;

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ValidationMessageNode);
}
