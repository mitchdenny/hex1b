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
    /// The label placement mode for this field.
    /// </summary>
    internal LabelPlacement LabelPlacement { get; set; } = LabelPlacement.Above;

    /// <summary>
    /// The label column width when using <see cref="Widgets.LabelPlacement.Inline"/>.
    /// </summary>
    internal int LabelWidth { get; set; } = 15;

    /// <summary>
    /// Whether an explicit width was set on the field.
    /// When false, the input fills available horizontal space.
    /// </summary>
    internal bool HasExplicitWidth { get; set; }

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

    private Size _labelMeasuredSize;
    private Size _inputMeasuredSize;
    private Size _errorMeasuredSize;

    protected override Size MeasureCore(Constraints constraints)
    {
        if (LabelPlacement == LabelPlacement.Inline)
            return MeasureInline(constraints);

        return MeasureAbove(constraints);
    }

    private Size MeasureAbove(Constraints constraints)
    {
        var totalHeight = 0;

        if (LabelChild != null)
        {
            _labelMeasuredSize = LabelChild.Measure(constraints);
            totalHeight += _labelMeasuredSize.Height;
        }

        // Measure error indicator first to know how much space the input gets
        var errorWidth = 0;
        if (ErrorIndicatorChild != null)
        {
            _errorMeasuredSize = ErrorIndicatorChild.Measure(new Constraints(0, constraints.MaxWidth, 0, 1));
            errorWidth = _errorMeasuredSize.Width;
        }

        var inputAvailable = Math.Max(0, constraints.MaxWidth - errorWidth);
        if (InputChild != null)
        {
            _inputMeasuredSize = InputChild.Measure(new Constraints(0, inputAvailable, 0, 1));
        }

        // When no explicit width, fill the full available width
        var rowWidth = HasExplicitWidth
            ? _inputMeasuredSize.Width + errorWidth
            : constraints.MaxWidth;

        totalHeight += 1;

        return constraints.Constrain(new Size(rowWidth, totalHeight));
    }

    private Size MeasureInline(Constraints constraints)
    {
        var labelCol = Math.Min(LabelWidth, constraints.MaxWidth);
        var remainingWidth = Math.Max(0, constraints.MaxWidth - labelCol);

        if (LabelChild != null)
        {
            _labelMeasuredSize = LabelChild.Measure(new Constraints(0, labelCol, 0, 1));
        }

        var errorWidth = 0;
        if (ErrorIndicatorChild != null)
        {
            _errorMeasuredSize = ErrorIndicatorChild.Measure(new Constraints(0, remainingWidth, 0, 1));
            errorWidth = _errorMeasuredSize.Width;
        }

        var inputAvailable = Math.Max(0, remainingWidth - errorWidth);
        if (InputChild != null)
        {
            _inputMeasuredSize = InputChild.Measure(new Constraints(0, inputAvailable, 0, 1));
        }

        var rowWidth = HasExplicitWidth
            ? labelCol + _inputMeasuredSize.Width + errorWidth
            : constraints.MaxWidth;

        return constraints.Constrain(new Size(rowWidth, 1));
    }

    protected override void ArrangeCore(Rect rect)
    {
        base.ArrangeCore(rect);

        if (LabelPlacement == LabelPlacement.Inline)
            ArrangeInline(rect);
        else
            ArrangeAbove(rect);
    }

    private void ArrangeAbove(Rect rect)
    {
        var y = rect.Y;

        if (LabelChild != null)
        {
            LabelChild.Arrange(new Rect(rect.X, y, rect.Width, 1));
            y += 1;
        }

        var errorWidth = ErrorIndicatorChild != null ? _errorMeasuredSize.Width : 0;
        var inputWidth = HasExplicitWidth
            ? _inputMeasuredSize.Width
            : Math.Max(0, rect.Width - errorWidth);

        var inputX = rect.X;
        if (InputChild != null)
        {
            InputChild.Arrange(new Rect(inputX, y, inputWidth, 1));
            inputX += inputWidth;
        }

        if (ErrorIndicatorChild != null)
        {
            ErrorIndicatorChild.Arrange(new Rect(inputX, y, errorWidth, 1));
        }
    }

    private void ArrangeInline(Rect rect)
    {
        var labelCol = Math.Min(LabelWidth, rect.Width);
        var x = rect.X;

        if (LabelChild != null)
        {
            LabelChild.Arrange(new Rect(x, rect.Y, labelCol, 1));
        }

        x += labelCol;
        var remainingWidth = Math.Max(0, rect.Width - labelCol);
        var errorWidth = ErrorIndicatorChild != null ? _errorMeasuredSize.Width : 0;
        var inputWidth = HasExplicitWidth
            ? Math.Min(_inputMeasuredSize.Width, remainingWidth)
            : Math.Max(0, remainingWidth - errorWidth);

        if (InputChild != null)
        {
            InputChild.Arrange(new Rect(x, rect.Y, inputWidth, 1));
            x += inputWidth;
        }

        if (ErrorIndicatorChild != null)
        {
            ErrorIndicatorChild.Arrange(new Rect(x, rect.Y, errorWidth, 1));
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
