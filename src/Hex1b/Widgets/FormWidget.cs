using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A container widget for building structured forms with text fields, validation,
/// and conditional enablement. FormWidget acts as a panel that lays out its children
/// vertically, with optional label placement configuration.
/// </summary>
/// <remarks>
/// <para>
/// Use the <c>ctx.Form(...)</c> extension method to create a form. The builder callback
/// receives a <see cref="FormContext"/> which provides form-specific extensions like
/// <c>form.TextField(...)</c> alongside standard widget methods.
/// </para>
/// <para>
/// Internally, FormWidget builds a VStack or Grid layout depending on the label placement
/// mode and reconciles it as a single content child on the FormNode.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ctx.Form(form =>
/// {
///     var name = form.TextField("Name").MinWidth(20);
///     var email = form.TextField("Email").EnableWhenValid(name);
///     return [name, email, form.SubmitButton("Save", e => { })];
/// })
/// </code>
/// </example>
public sealed record FormWidget(IReadOnlyList<Hex1bWidget> Children) : Hex1bWidget
{
    /// <summary>
    /// The label placement mode for form fields.
    /// Defaults to <see cref="Widgets.LabelPlacement.Above"/>.
    /// </summary>
    public LabelPlacement LabelPlacement { get; init; } = LabelPlacement.Above;

    /// <summary>
    /// The label width in columns when using <see cref="Widgets.LabelPlacement.Inline"/>.
    /// Defaults to 15.
    /// </summary>
    public int LabelWidth { get; init; } = 15;

    /// <summary>
    /// The field registry tracking cross-field references for validation and enablement.
    /// </summary>
    internal FormFieldRegistry? FieldRegistry { get; init; }

    /// <summary>
    /// Sets the label placement mode for all fields in the form.
    /// </summary>
    public FormWidget WithLabelPlacement(LabelPlacement placement)
        => this with { LabelPlacement = placement };

    /// <summary>
    /// Sets the label width for inline label placement.
    /// </summary>
    public FormWidget WithLabelWidth(int width)
        => this with { LabelWidth = width };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as FormNode ?? new FormNode();

        node.LabelPlacement = LabelPlacement;
        node.LabelWidth = LabelWidth;
        node.FieldRegistry = FieldRegistry;

        // Build the internal layout widget from children
        var layoutWidget = BuildLayout();

        // Reconcile the layout as a single content child
        node.Content = await context.ReconcileChildAsync(node.Content, layoutWidget, node);

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(FormNode);

    /// <summary>
    /// Builds the internal layout widget based on the label placement mode.
    /// </summary>
    private Hex1bWidget BuildLayout()
    {
        // For now, use a simple VStack layout.
        // Inline label placement using Grid will be added when FormTextFieldWidget
        // provides label/input separation.
        return new VStackWidget(Children);
    }
}
