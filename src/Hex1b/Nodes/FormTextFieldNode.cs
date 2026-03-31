using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="FormTextFieldWidget"/>.
/// Manages the label, text input, and adornment children.
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
    /// Resolved adornment child nodes (only those whose predicate resolved true).
    /// </summary>
    internal List<Hex1bNode> AdornmentChildren { get; } = new();

    /// <summary>
    /// Tracks the resolved visibility state for each adornment by index.
    /// </summary>
    internal List<bool> AdornmentVisibility { get; } = new();

    /// <summary>
    /// Cancellation token source for in-flight adornment predicate evaluations.
    /// Cancelled and replaced whenever the field value changes.
    /// </summary>
    internal CancellationTokenSource? AdornmentCts { get; set; }

    /// <summary>
    /// The field value that was last used for adornment evaluation.
    /// Used to detect when re-evaluation is needed.
    /// </summary>
    internal string? LastAdornmentEvaluationValue { get; set; }

    /// <summary>
    /// The number of adornments configured on the widget.
    /// Used to detect when the adornment list changes.
    /// </summary>
    internal int AdornmentCount { get; set; }

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

    /// <summary>
    /// Evaluates all adornment predicates asynchronously.
    /// Cancels any in-flight evaluations from previous values.
    /// When predicates resolve, marks the node dirty to trigger re-render.
    /// </summary>
    internal void EvaluateAdornments(IReadOnlyList<FieldAdornment> adornments, string fieldValue)
    {
        // Cancel any in-flight evaluations
        AdornmentCts?.Cancel();
        AdornmentCts?.Dispose();
        AdornmentCts = new CancellationTokenSource();
        var ct = AdornmentCts.Token;

        LastAdornmentEvaluationValue = fieldValue;
        AdornmentCount = adornments.Count;

        // Resize visibility list to match adornment count
        while (AdornmentVisibility.Count < adornments.Count)
            AdornmentVisibility.Add(false);
        while (AdornmentVisibility.Count > adornments.Count)
            AdornmentVisibility.RemoveAt(AdornmentVisibility.Count - 1);

        // Evaluate each predicate
        for (var i = 0; i < adornments.Count; i++)
        {
            var index = i;
            var adornment = adornments[i];

            _ = EvaluateAdornmentAsync(adornment, fieldValue, index, ct);
        }
    }

    private async Task EvaluateAdornmentAsync(
        FieldAdornment adornment, string fieldValue, int index, CancellationToken ct)
    {
        try
        {
            var result = await adornment.Predicate(fieldValue, ct);

            if (ct.IsCancellationRequested)
                return;

            if (index < AdornmentVisibility.Count && AdornmentVisibility[index] != result)
            {
                AdornmentVisibility[index] = result;
                MarkDirty();
                AppInvalidate?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when field value changes during evaluation
        }
        catch
        {
            // Predicate failed — hide the adornment
            if (!ct.IsCancellationRequested && index < AdornmentVisibility.Count)
            {
                AdornmentVisibility[index] = false;
                MarkDirty();
                AppInvalidate?.Invoke();
            }
        }
    }

    private Size _labelMeasuredSize;
    private Size _inputMeasuredSize;
    private int _adornmentsTotalWidth;
    private List<Size> _adornmentMeasuredSizes = new();

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

        // Measure adornments first to know how much space the input gets
        _adornmentsTotalWidth = 0;
        _adornmentMeasuredSizes.Clear();
        foreach (var adornment in AdornmentChildren)
        {
            var size = adornment.Measure(new Constraints(0, constraints.MaxWidth, 0, 1));
            _adornmentMeasuredSizes.Add(size);
            _adornmentsTotalWidth += size.Width;
        }

        var inputAvailable = Math.Max(0, constraints.MaxWidth - _adornmentsTotalWidth);
        if (InputChild != null)
        {
            _inputMeasuredSize = InputChild.Measure(new Constraints(0, inputAvailable, 0, constraints.MaxHeight));
        }

        // When no explicit width, fill the full available width
        var rowWidth = HasExplicitWidth
            ? _inputMeasuredSize.Width + _adornmentsTotalWidth
            : constraints.MaxWidth;

        totalHeight += _inputMeasuredSize.Height;

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

        _adornmentsTotalWidth = 0;
        _adornmentMeasuredSizes.Clear();
        foreach (var adornment in AdornmentChildren)
        {
            var size = adornment.Measure(new Constraints(0, remainingWidth, 0, 1));
            _adornmentMeasuredSizes.Add(size);
            _adornmentsTotalWidth += size.Width;
        }

        var inputAvailable = Math.Max(0, remainingWidth - _adornmentsTotalWidth);
        if (InputChild != null)
        {
            _inputMeasuredSize = InputChild.Measure(new Constraints(0, inputAvailable, 0, constraints.MaxHeight));
        }

        var rowWidth = HasExplicitWidth
            ? labelCol + _inputMeasuredSize.Width + _adornmentsTotalWidth
            : constraints.MaxWidth;

        var rowHeight = Math.Max(1, _inputMeasuredSize.Height);
        return constraints.Constrain(new Size(rowWidth, rowHeight));
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

        var inputWidth = HasExplicitWidth
            ? _inputMeasuredSize.Width
            : Math.Max(0, rect.Width - _adornmentsTotalWidth);
        var inputHeight = _inputMeasuredSize.Height;

        var x = rect.X;
        if (InputChild != null)
        {
            InputChild.Arrange(new Rect(x, y, inputWidth, inputHeight));
            x += inputWidth;
        }

        for (var i = 0; i < AdornmentChildren.Count; i++)
        {
            var adornWidth = _adornmentMeasuredSizes[i].Width;
            AdornmentChildren[i].Arrange(new Rect(x, y, adornWidth, 1));
            x += adornWidth;
        }
    }

    private void ArrangeInline(Rect rect)
    {
        var labelCol = Math.Min(LabelWidth, rect.Width);
        var x = rect.X;
        var inputHeight = Math.Max(1, _inputMeasuredSize.Height);

        if (LabelChild != null)
        {
            LabelChild.Arrange(new Rect(x, rect.Y, labelCol, 1));
        }

        x += labelCol;
        var remainingWidth = Math.Max(0, rect.Width - labelCol);
        var inputWidth = HasExplicitWidth
            ? Math.Min(_inputMeasuredSize.Width, remainingWidth)
            : Math.Max(0, remainingWidth - _adornmentsTotalWidth);

        if (InputChild != null)
        {
            InputChild.Arrange(new Rect(x, rect.Y, inputWidth, inputHeight));
            x += inputWidth;
        }

        for (var i = 0; i < AdornmentChildren.Count; i++)
        {
            var adornWidth = _adornmentMeasuredSizes[i].Width;
            AdornmentChildren[i].Arrange(new Rect(x, rect.Y, adornWidth, 1));
            x += adornWidth;
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (LabelChild != null)
            context.RenderChild(LabelChild);

        if (InputChild != null)
            context.RenderChild(InputChild);

        foreach (var adornment in AdornmentChildren)
            context.RenderChild(adornment);
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
        foreach (var adornment in AdornmentChildren)
            yield return adornment;
    }
}
