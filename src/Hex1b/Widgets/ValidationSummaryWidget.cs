using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// Renders a summary of all validation errors from the parent form.
/// Displays each error as a themed line of text.
/// When there are no errors, renders nothing.
/// </summary>
/// <remarks>
/// Use <c>form.ValidationSummary()</c> inside a form builder to add an aggregated
/// error display. For custom error rendering, use <c>form.ValidationErrors</c> instead.
/// </remarks>
public sealed record ValidationSummaryWidget : Hex1bWidget
{
    /// <summary>
    /// The field IDs and their labels, for display purposes.
    /// </summary>
    internal IReadOnlyDictionary<string, string> FieldLabels { get; init; } =
        new Dictionary<string, string>();

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        // Find the parent FormNode to access validation state
        var formNode = context.FindAncestor<FormNode>();
        var errors = formNode?.ValidationResults
            .Where(kv => !kv.Value.IsValid)
            .ToList() ?? [];

        if (errors.Count == 0)
        {
            // No errors — render an empty text block
            var emptyWidget = new TextBlockWidget("");
            return await emptyWidget.ReconcileAsync(existingNode, context);
        }

        // Build a VStack of error messages
        var errorWidgets = new List<Hex1bWidget>();
        foreach (var (fieldId, result) in errors)
        {
            var label = FieldLabels.TryGetValue(fieldId, out var l) ? l : fieldId;
            var message = $"{label}: {result.ErrorMessage}";

            errorWidgets.Add(new ThemePanelWidget(
                t => t.Set(GlobalTheme.ForegroundColor, t.Get(FormTheme.ValidationErrorColor)),
                new TextBlockWidget(message)));
        }

        var summaryWidget = new VStackWidget(errorWidgets);
        return await summaryWidget.ReconcileAsync(existingNode, context);
    }

    internal override Type GetExpectedNodeType() => typeof(VStackNode);
}
