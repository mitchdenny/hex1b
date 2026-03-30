using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="FormTextFieldWidget"/>.
/// Manages the label, text input, and error indicator children.
/// </summary>
public sealed class FormTextFieldNode : Hex1bNode
{
    /// <summary>
    /// The field's unique ID within the form.
    /// </summary>
    internal string FieldId { get; set; } = "";

    /// <summary>
    /// The field's label text.
    /// </summary>
    internal string Label { get; set; } = "";

    /// <summary>
    /// Whether this field is enabled.
    /// </summary>
    internal bool IsEnabled { get; set; } = true;

    /// <summary>
    /// The current text value of the field.
    /// </summary>
    internal string CurrentValue { get; set; } = "";

    /// <summary>
    /// Whether the initial value has been applied.
    /// </summary>
    internal bool HasAppliedInitialValue { get; set; }

    /// <summary>
    /// The validators for this field.
    /// </summary>
    internal IReadOnlyList<Func<string, ValidationResult>> Validators { get; set; } = [];

    /// <summary>
    /// When validation is triggered.
    /// </summary>
    internal ValidateOn ValidateOn { get; set; } = ValidateOn.Change;

    /// <summary>
    /// The current validation result.
    /// </summary>
    internal ValidationResult CurrentValidationResult { get; set; } = ValidationResult.Valid;

    /// <summary>
    /// The label child node.
    /// </summary>
    public Hex1bNode? LabelChild { get; set; }

    /// <summary>
    /// The text input child node.
    /// </summary>
    public Hex1bNode? InputChild { get; set; }

    /// <summary>
    /// The error indicator child node (null when valid).
    /// </summary>
    public Hex1bNode? ErrorIndicatorChild { get; set; }

    /// <summary>
    /// Runs all validators against the current value and updates the validation result.
    /// </summary>
    internal void RunValidation()
    {
        foreach (var validator in Validators)
        {
            var result = validator(CurrentValue);
            if (!result.IsValid)
            {
                CurrentValidationResult = result;
                return;
            }
        }
        CurrentValidationResult = ValidationResult.Valid;
    }

    private Size _inputMeasuredSize;
    private Size _errorMeasuredSize;

    protected override Size MeasureCore(Constraints constraints)
    {
        var totalWidth = 0;
        var totalHeight = 0;

        if (LabelChild != null)
        {
            var labelSize = LabelChild.Measure(constraints);
            totalWidth = Math.Max(totalWidth, labelSize.Width);
            totalHeight += labelSize.Height;
        }

        // Input and error indicator on the same line
        var inputWidth = 0;

        if (InputChild != null)
        {
            _inputMeasuredSize = InputChild.Measure(new Constraints(0, constraints.MaxWidth, 0, 1));
            inputWidth += _inputMeasuredSize.Width;
        }

        if (ErrorIndicatorChild != null)
        {
            _errorMeasuredSize = ErrorIndicatorChild.Measure(
                new Constraints(0, Math.Max(0, constraints.MaxWidth - inputWidth), 0, 1));
            inputWidth += _errorMeasuredSize.Width;
        }

        totalWidth = Math.Max(totalWidth, inputWidth);
        totalHeight += 1; // input row

        return constraints.Constrain(new Size(totalWidth, totalHeight));
    }

    protected override void ArrangeCore(Rect rect)
    {
        base.ArrangeCore(rect);

        var y = rect.Y;

        if (LabelChild != null)
        {
            LabelChild.Arrange(new Rect(rect.X, y, rect.Width, 1));
            y += 1;
        }

        // Input and error on the same line
        var inputX = rect.X;

        if (InputChild != null)
        {
            InputChild.Arrange(new Rect(inputX, y, _inputMeasuredSize.Width, 1));
            inputX += _inputMeasuredSize.Width;
        }

        if (ErrorIndicatorChild != null)
        {
            ErrorIndicatorChild.Arrange(new Rect(inputX, y, _errorMeasuredSize.Width, 1));
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (LabelChild != null)
            context.RenderChild(LabelChild);

        if (InputChild != null)
            context.RenderChild(InputChild);

        if (ErrorIndicatorChild != null)
            context.RenderChild(ErrorIndicatorChild);
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // Only the input is focusable
        if (InputChild != null)
        {
            foreach (var focusable in InputChild.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (LabelChild != null) yield return LabelChild;
        if (InputChild != null) yield return InputChild;
        if (ErrorIndicatorChild != null) yield return ErrorIndicatorChild;
    }
}
