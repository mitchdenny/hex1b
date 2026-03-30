using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A form text field widget that renders as a label + text input + error indicator.
/// Also serves as a field handle for cross-field references (validation, enablement).
/// </summary>
/// <remarks>
/// <para>
/// FormTextFieldWidget is created via <c>form.TextField("Label")</c> inside a Form builder.
/// It composes a label (TextBlockWidget), a text input (TextBoxWidget), and an optional
/// error indicator into a single self-contained field.
/// </para>
/// <para>
/// The widget also acts as a field handle: the <see cref="FieldId"/> uniquely identifies
/// this field within the form, enabling cross-field features like
/// <see cref="EnableWhenValid"/>.
/// </para>
/// </remarks>
public sealed record FormTextFieldWidget : Hex1bWidget
{
    /// <summary>
    /// Unique identifier for this field within the form.
    /// Preserved across fluent method calls.
    /// </summary>
    public string FieldId { get; }

    /// <summary>
    /// The label text displayed for this field.
    /// </summary>
    public string Label { get; init; }

    /// <summary>
    /// Minimum width of the text input in columns.
    /// </summary>
    public int? MinWidth { get; init; }

    /// <summary>
    /// Maximum width of the text input in columns.
    /// Defaults to MinWidth if not explicitly set.
    /// </summary>
    public int? MaxWidth { get; init; }

    /// <summary>
    /// Initial text value for the field.
    /// </summary>
    public string? InitialValue { get; init; }

    /// <summary>
    /// Validators to run against the field value.
    /// </summary>
    internal IReadOnlyList<Func<string, ValidationResult>> Validators { get; init; } = [];

    /// <summary>
    /// When validation should be triggered.
    /// </summary>
    public ValidateOn ValidateOn { get; init; } = ValidateOn.Change;

    /// <summary>
    /// Field IDs that must all be valid for this field to be enabled.
    /// </summary>
    internal IReadOnlyList<string> EnableWhenValidFieldIds { get; init; } = [];

    /// <summary>
    /// Arbitrary predicate for enabling this field.
    /// </summary>
    internal Func<bool>? EnablePredicate { get; init; }

    /// <summary>
    /// Handler invoked when the text value changes.
    /// </summary>
    internal Func<TextChangedEventArgs, Task>? TextChangedHandler { get; init; }

    /// <summary>
    /// The label placement override for this specific field.
    /// When null, uses the form-level setting.
    /// </summary>
    internal LabelPlacement? LabelPlacementOverride { get; init; }

    public FormTextFieldWidget(string fieldId, string label)
    {
        FieldId = fieldId;
        Label = label;
    }

    /// <summary>
    /// Sets the minimum width of the text input.
    /// </summary>
    public FormTextFieldWidget WithMinWidth(int width)
        => this with { MinWidth = width };

    /// <summary>
    /// Sets the maximum width of the text input.
    /// </summary>
    public FormTextFieldWidget WithMaxWidth(int width)
        => this with { MaxWidth = width };

    /// <summary>
    /// Sets an exact fixed width for the text input (sets both MinWidth and MaxWidth).
    /// </summary>
    public FormTextFieldWidget WithWidth(int width)
        => this with { MinWidth = width, MaxWidth = width };

    /// <summary>
    /// Sets the initial text value.
    /// </summary>
    public FormTextFieldWidget WithInitialValue(string value)
        => this with { InitialValue = value };

    /// <summary>
    /// Adds a validator to this field.
    /// </summary>
    public FormTextFieldWidget Validate(Func<string, ValidationResult> validator)
        => this with { Validators = [.. Validators, validator] };

    /// <summary>
    /// Sets when validation should be triggered for this field.
    /// </summary>
    public FormTextFieldWidget WithValidateOn(ValidateOn mode)
        => this with { ValidateOn = mode };

    /// <summary>
    /// Enables this field only when all specified fields are valid.
    /// </summary>
    public FormTextFieldWidget EnableWhenValid(params FormTextFieldWidget[] fields)
        => this with { EnableWhenValidFieldIds = [.. EnableWhenValidFieldIds, .. fields.Select(f => f.FieldId)] };

    /// <summary>
    /// Enables this field only when the predicate returns true.
    /// </summary>
    public FormTextFieldWidget EnableWhen(Func<bool> predicate)
        => this with { EnablePredicate = predicate };

    /// <summary>
    /// Sets a handler for text change events.
    /// </summary>
    public FormTextFieldWidget OnTextChanged(Action<TextChangedEventArgs> handler)
        => this with { TextChangedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an async handler for text change events.
    /// </summary>
    public FormTextFieldWidget OnTextChanged(Func<TextChangedEventArgs, Task> handler)
        => this with { TextChangedHandler = handler };

    /// <summary>
    /// Overrides the label placement for this specific field.
    /// </summary>
    public FormTextFieldWidget WithLabelPlacement(LabelPlacement placement)
        => this with { LabelPlacementOverride = placement };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as FormTextFieldNode ?? new FormTextFieldNode();

        node.FieldId = FieldId;
        node.Label = Label;
        node.IsEnabled = EvaluateEnabled(context);

        // Resolve label placement from field override or form-level setting
        var formNode = context.FindAncestor<FormNode>();
        node.LabelPlacement = LabelPlacementOverride ?? formNode?.LabelPlacement ?? LabelPlacement.Above;
        node.LabelWidth = formNode?.LabelWidth ?? 15;
        node.HasExplicitWidth = MinWidth.HasValue;

        // Apply initial value only once
        if (!node.HasAppliedInitialValue && InitialValue != null)
        {
            node.CurrentValue = InitialValue;
            node.HasAppliedInitialValue = true;
        }

        // Sync validators
        node.Validators = Validators;
        node.ValidateOn = ValidateOn;

        // Build the label widget
        var labelWidget = new TextBlockWidget(Label);
        node.LabelChild = await context.ReconcileChildAsync(node.LabelChild, labelWidget, node);

        // Build the text box widget with fill mode enabled (no brackets, painted background).
        // MinWidth=1 ensures TextBox avoids bracket-mode measurement when no explicit width is set;
        // the FormTextFieldNode controls the actual rendered width via layout.
        var textBoxMinWidth = MinWidth ?? 1;
        var textBoxMaxWidth = MaxWidth ?? MinWidth;
        var textBox = new TextBoxWidget(node.CurrentValue) { MinWidth = textBoxMinWidth, MaxWidth = textBoxMaxWidth }
            .OnTextChanged(async e =>
            {
                node.CurrentValue = e.NewText;

                if (node.ValidateOn == ValidateOn.Change)
                {
                    node.RunValidation();
                }

                // Update form node state
                var formNode = FindFormNode(node);
                if (formNode != null)
                {
                    formNode.SetFieldValue(FieldId, e.NewText);
                    if (node.ValidateOn == ValidateOn.Change)
                    {
                        formNode.SetValidationResult(FieldId, node.CurrentValidationResult);
                    }
                }

                if (TextChangedHandler != null)
                {
                    await TextChangedHandler(e);
                }
            });

        // Wrap in ThemePanel to enable fill mode rendering
        Hex1bWidget inputWidget = new ThemePanelWidget(
            t => t.Set(Theming.TextBoxTheme.UseFillMode, true),
            textBox);

        node.InputChild = await context.ReconcileChildAsync(node.InputChild, inputWidget, node);

        // Build error indicator (shown when validation fails)
        Hex1bWidget? errorWidget = null;
        if (!node.CurrentValidationResult.IsValid)
        {
            errorWidget = new ThemePanelWidget(
                t => t.Set(Theming.GlobalTheme.ForegroundColor, t.Get(Theming.FormTheme.ValidationErrorColor)),
                new TextBlockWidget(" ✗"));
        }
        node.ErrorIndicatorChild = errorWidget != null
            ? await context.ReconcileChildAsync(node.ErrorIndicatorChild, errorWidget, node)
            : null;

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(FormTextFieldNode);

    /// <summary>
    /// Evaluates whether this field should be enabled based on EnableWhen predicates.
    /// </summary>
    private bool EvaluateEnabled(ReconcileContext context)
    {
        if (EnablePredicate != null && !EnablePredicate())
            return false;

        if (EnableWhenValidFieldIds.Count > 0)
        {
            var formNode = context.FindAncestor<FormNode>();
            if (formNode != null)
            {
                return formNode.AreFieldsValid(EnableWhenValidFieldIds);
            }
        }

        return true;
    }

    /// <summary>
    /// Walks up the parent chain to find the nearest FormNode.
    /// </summary>
    private static FormNode? FindFormNode(Hex1bNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is FormNode formNode)
                return formNode;
            current = current.Parent;
        }
        return null;
    }
}
