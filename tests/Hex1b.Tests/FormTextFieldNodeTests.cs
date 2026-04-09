using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class FormTextFieldNodeTests
{
    [Fact]
    public void RunValidation_ValidValue_ResultIsValid()
    {
        var node = new FormTextFieldNode
        {
            CurrentValue = "hello",
            Validators = [v => ValidationResult.Valid]
        };

        node.RunValidation();

        Assert.True(node.CurrentValidationResult.IsValid);
    }

    [Fact]
    public void RunValidation_InvalidValue_SetsErrorResult()
    {
        var node = new FormTextFieldNode
        {
            CurrentValue = "",
            Validators = [v => string.IsNullOrEmpty(v)
                ? ValidationResult.Error("Required")
                : ValidationResult.Valid]
        };

        node.RunValidation();

        Assert.False(node.CurrentValidationResult.IsValid);
        Assert.Equal("Required", node.CurrentValidationResult.ErrorMessage);
    }

    [Fact]
    public void RunValidation_MultipleValidators_StopsAtFirstError()
    {
        var secondValidatorCalled = false;
        var node = new FormTextFieldNode
        {
            CurrentValue = "",
            Validators = [
                v => ValidationResult.Error("First error"),
                v => { secondValidatorCalled = true; return ValidationResult.Valid; }
            ]
        };

        node.RunValidation();

        Assert.False(node.CurrentValidationResult.IsValid);
        Assert.Equal("First error", node.CurrentValidationResult.ErrorMessage);
        Assert.False(secondValidatorCalled);
    }

    [Fact]
    public void RunValidation_NoValidators_RemainsValid()
    {
        var node = new FormTextFieldNode
        {
            CurrentValue = "anything",
            Validators = []
        };

        node.RunValidation();

        Assert.True(node.CurrentValidationResult.IsValid);
    }

    [Fact]
    public void RunValidation_ValueBecomesValid_ClearsError()
    {
        var node = new FormTextFieldNode
        {
            CurrentValue = "",
            Validators = [v => string.IsNullOrEmpty(v)
                ? ValidationResult.Error("Required")
                : ValidationResult.Valid]
        };

        node.RunValidation();
        Assert.False(node.CurrentValidationResult.IsValid);

        node.CurrentValue = "now valid";
        node.RunValidation();
        Assert.True(node.CurrentValidationResult.IsValid);
    }

    [Fact]
    public void Measure_WithLabelAndInput_IncludesBothRows()
    {
        var labelChild = new TextBlockNode { Text = "Name" };
        var inputChild = new TextBoxNode();

        var node = new FormTextFieldNode
        {
            LabelChild = labelChild,
            InputChild = inputChild
        };

        var size = node.Measure(new Constraints(0, 30, 0, 20));

        // Label row + input row = at least 2 rows
        Assert.True(size.Height >= 2);
    }

    [Fact]
    public void Measure_InputOnly_SingleRow()
    {
        var inputChild = new TextBoxNode();

        var node = new FormTextFieldNode
        {
            InputChild = inputChild
        };

        var size = node.Measure(new Constraints(0, 30, 0, 20));

        // Just input row
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Arrange_LabelAboveInput()
    {
        var labelChild = new TextBlockNode { Text = "Name" };
        var inputChild = new TextBoxNode();

        var node = new FormTextFieldNode
        {
            LabelChild = labelChild,
            InputChild = inputChild
        };

        node.Measure(new Constraints(0, 30, 0, 20));
        node.Arrange(new Rect(0, 0, 30, 5));

        // Label at row 0, input at row 1
        Assert.Equal(0, labelChild.Bounds.Y);
        Assert.Equal(1, inputChild.Bounds.Y);
    }

    [Fact]
    public void Measure_InlineMode_SingleRow()
    {
        var labelChild = new TextBlockNode { Text = "Name" };
        var inputChild = new TextBoxNode();

        var node = new FormTextFieldNode
        {
            LabelPlacement = LabelPlacement.Inline,
            LabelWidth = 10,
            LabelChild = labelChild,
            InputChild = inputChild
        };

        var size = node.Measure(new Constraints(0, 40, 0, 20));

        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Arrange_InlineMode_LabelBesideInput()
    {
        var labelChild = new TextBlockNode { Text = "Name" };
        var inputChild = new TextBoxNode();

        var node = new FormTextFieldNode
        {
            LabelPlacement = LabelPlacement.Inline,
            LabelWidth = 12,
            LabelChild = labelChild,
            InputChild = inputChild
        };

        node.Measure(new Constraints(0, 40, 0, 20));
        node.Arrange(new Rect(0, 0, 40, 5));

        // Same row, label on left, input starts at label width
        Assert.Equal(0, labelChild.Bounds.Y);
        Assert.Equal(0, inputChild.Bounds.Y);
        Assert.Equal(0, labelChild.Bounds.X);
        Assert.Equal(12, inputChild.Bounds.X);
    }

    [Fact]
    public void GetChildren_ReturnsAllChildren()
    {
        var label = new TextBlockNode { Text = "Name" };
        var input = new TextBoxNode();
        var adornment = new TextBlockNode { Text = "!" };

        var node = new FormTextFieldNode
        {
            LabelChild = label,
            InputChild = input,
        };
        node.AdornmentChildren.Add(adornment);

        var children = node.GetChildren().ToList();

        Assert.Equal(3, children.Count);
        Assert.Contains(label, children);
        Assert.Contains(input, children);
        Assert.Contains(adornment, children);
    }

    [Fact]
    public void GetChildren_SkipsNullChildren()
    {
        var input = new TextBoxNode();

        var node = new FormTextFieldNode
        {
            InputChild = input
        };

        var children = node.GetChildren().ToList();

        Assert.Single(children);
        Assert.Same(input, children[0]);
    }

    [Fact]
    public void GetFocusableNodes_ReturnsInputFocusables()
    {
        var input = new TextBoxNode();
        var label = new TextBlockNode { Text = "Name" };

        var node = new FormTextFieldNode
        {
            LabelChild = label,
            InputChild = input
        };

        var focusables = node.GetFocusableNodes().ToList();

        // TextBoxNode is focusable
        Assert.NotEmpty(focusables);
    }

    [Fact]
    public async Task Reconcile_CreatesFormTextFieldNode()
    {
        var widget = new FormTextFieldWidget("field1", "Name");
        var context = ReconcileContext.CreateRoot();

        var node = await widget.ReconcileAsync(null, context);

        Assert.IsType<FormTextFieldNode>(node);
    }

    [Fact]
    public async Task Reconcile_SetsFieldIdAndLabel()
    {
        var widget = new FormTextFieldWidget("field1", "Name");
        var context = ReconcileContext.CreateRoot();

        var node = (FormTextFieldNode)await widget.ReconcileAsync(null, context);

        Assert.Equal("field1", node.FieldId);
        Assert.Equal("Name", node.Label);
    }

    [Fact]
    public async Task Reconcile_CreatesLabelAndInputChildren()
    {
        var widget = new FormTextFieldWidget("field1", "Name");
        var context = ReconcileContext.CreateRoot();

        var node = (FormTextFieldNode)await widget.ReconcileAsync(null, context);

        Assert.NotNull(node.LabelChild);
        Assert.NotNull(node.InputChild);
    }

    [Fact]
    public async Task Reconcile_ReusesExistingNode()
    {
        var widget = new FormTextFieldWidget("field1", "Name");
        var context = ReconcileContext.CreateRoot();

        var node1 = await widget.ReconcileAsync(null, context);
        var node2 = await widget.ReconcileAsync(node1, context);

        Assert.Same(node1, node2);
    }

    [Fact]
    public async Task Reconcile_AppliesInitialValue()
    {
        var widget = new FormTextFieldWidget("field1", "Name")
            .WithInitialValue("John");
        var context = ReconcileContext.CreateRoot();

        var node = (FormTextFieldNode)await widget.ReconcileAsync(null, context);

        Assert.Equal("John", node.CurrentValue);
        Assert.True(node.HasAppliedInitialValue);
    }

    [Fact]
    public async Task Reconcile_InitialValueAppliedOnlyOnce()
    {
        var widget = new FormTextFieldWidget("field1", "Name")
            .WithInitialValue("John");
        var context = ReconcileContext.CreateRoot();

        var node = (FormTextFieldNode)await widget.ReconcileAsync(null, context);
        node.CurrentValue = "Jane"; // User changed value

        // Re-reconcile — initial value should NOT override user's change
        await widget.ReconcileAsync(node, context);

        Assert.Equal("Jane", node.CurrentValue);
    }

    [Fact]
    public async Task Reconcile_NoAdornmentsWhenValid()
    {
        var widget = new FormTextFieldWidget("field1", "Name");
        var context = ReconcileContext.CreateRoot();

        var node = (FormTextFieldNode)await widget.ReconcileAsync(null, context);

        Assert.Empty(node.AdornmentChildren);
    }

    [Fact]
    public async Task Reconcile_SetsValidators()
    {
        Func<string, ValidationResult> validator = v => ValidationResult.Valid;
        var widget = new FormTextFieldWidget("field1", "Name")
            .Validate(validator);
        var context = ReconcileContext.CreateRoot();

        var node = (FormTextFieldNode)await widget.ReconcileAsync(null, context);

        Assert.Single(node.Validators);
    }

    #region Adornment Tests

    [Fact]
    public void EvaluateAdornments_SyncPredicate_SetsVisibilityImmediately()
    {
        // A sync predicate (wrapped in Task.FromResult) should resolve immediately
        // and set the visibility flag.
        var node = new FormTextFieldNode();
        var adornments = new List<FieldAdornment>
        {
            FieldAdornment.CreateSync(_ => true, () => new TextBlockWidget("✓")),
            FieldAdornment.CreateSync(_ => false, () => new TextBlockWidget("✗"))
        };

        node.EvaluateAdornments(adornments, "test");

        // Give async tasks a moment to complete (they're sync-wrapped)
        Thread.Sleep(50);

        Assert.Equal(2, node.AdornmentVisibility.Count);
        Assert.True(node.AdornmentVisibility[0]);
        Assert.False(node.AdornmentVisibility[1]);
    }

    [Fact]
    public async Task EvaluateAdornments_AsyncPredicate_SetsVisibilityWhenResolved()
    {
        // An async predicate should set visibility after it resolves.
        var tcs = new TaskCompletionSource<bool>();
        var node = new FormTextFieldNode();
        var adornments = new List<FieldAdornment>
        {
            new FieldAdornment((_, _) => tcs.Task, () => new TextBlockWidget("!"))
        };

        node.EvaluateAdornments(adornments, "test");

        // Before resolution, visibility should be false (default)
        Assert.Single(node.AdornmentVisibility);
        Assert.False(node.AdornmentVisibility[0]);

        // Resolve the predicate
        tcs.SetResult(true);
        await Task.Delay(50);

        Assert.True(node.AdornmentVisibility[0]);
    }

    [Fact]
    public async Task EvaluateAdornments_CancelsPreviousEvaluation()
    {
        // When EvaluateAdornments is called again, previous in-flight predicates
        // should be cancelled and not update visibility.
        var firstTcs = new TaskCompletionSource<bool>();
        var secondTcs = new TaskCompletionSource<bool>();
        var node = new FormTextFieldNode();

        var adornments1 = new List<FieldAdornment>
        {
            new FieldAdornment(async (_, ct) =>
            {
                await firstTcs.Task;
                ct.ThrowIfCancellationRequested();
                return true;
            }, () => new TextBlockWidget("1"))
        };

        var adornments2 = new List<FieldAdornment>
        {
            new FieldAdornment(async (_, ct) =>
            {
                await secondTcs.Task;
                ct.ThrowIfCancellationRequested();
                return true;
            }, () => new TextBlockWidget("2"))
        };

        // Start first evaluation
        node.EvaluateAdornments(adornments1, "first");

        // Start second evaluation (should cancel the first)
        node.EvaluateAdornments(adornments2, "second");

        // Resolve the first predicate — should be ignored (cancelled)
        firstTcs.SetResult(true);
        await Task.Delay(50);
        Assert.False(node.AdornmentVisibility[0]);

        // Resolve the second predicate — should update visibility
        secondTcs.SetResult(true);
        await Task.Delay(50);
        Assert.True(node.AdornmentVisibility[0]);
    }

    [Fact]
    public void EvaluateAdornments_PredicateException_HidesAdornment()
    {
        // If a predicate throws, the adornment should be hidden (not crash).
        var node = new FormTextFieldNode();
        var adornments = new List<FieldAdornment>
        {
            new FieldAdornment((_, _) => throw new InvalidOperationException("test error"),
                () => new TextBlockWidget("!"))
        };

        node.EvaluateAdornments(adornments, "test");
        Thread.Sleep(50);

        Assert.Single(node.AdornmentVisibility);
        Assert.False(node.AdornmentVisibility[0]);
    }

    [Fact]
    public async Task Reconcile_WithAdornment_SyncPredicateTrue_AddsAdornmentChild()
    {
        // When a field has an adornment with a sync-true predicate,
        // the adornment widget should be reconciled as a child.
        var widget = new FormTextFieldWidget("field1", "Name")
            .Adornment(
                async (value, ct) => !string.IsNullOrEmpty(value),
                () => new TextBlockWidget(" ★"));

        var context = ReconcileContext.CreateRoot();

        // Reconcile with initial value set
        var node = (FormTextFieldNode)await widget
            .WithInitialValue("Hello")
            .ReconcileAsync(null, context);

        // Give async predicate time to resolve
        await Task.Delay(100);

        // Re-reconcile to pick up resolved adornment
        node = (FormTextFieldNode)await widget
            .WithInitialValue("Hello")
            .ReconcileAsync(node, context);

        Assert.NotEmpty(node.AdornmentChildren);
    }

    [Fact]
    public async Task Reconcile_WithAdornment_PredicateFalse_NoAdornmentChild()
    {
        // When the adornment predicate resolves to false, no adornment child should appear.
        var widget = new FormTextFieldWidget("field1", "Name")
            .Adornment(
                async (value, ct) => false,
                () => new TextBlockWidget(" ★"));

        var context = ReconcileContext.CreateRoot();
        var node = (FormTextFieldNode)await widget.ReconcileAsync(null, context);

        await Task.Delay(100);
        node = (FormTextFieldNode)await widget.ReconcileAsync(node, context);

        Assert.Empty(node.AdornmentChildren);
    }

    [Fact]
    public async Task Reconcile_MultipleAdornments_OnlyVisibleOnesRendered()
    {
        // With multiple adornments, only those whose predicate resolved true
        // should appear as children.
        var widget = new FormTextFieldWidget("field1", "Name")
            .Adornment(async (_, _) => true, () => new TextBlockWidget(" A"))
            .Adornment(async (_, _) => false, () => new TextBlockWidget(" B"))
            .Adornment(async (_, _) => true, () => new TextBlockWidget(" C"));

        var context = ReconcileContext.CreateRoot();
        var node = (FormTextFieldNode)await widget.ReconcileAsync(null, context);

        await Task.Delay(100);
        node = (FormTextFieldNode)await widget.ReconcileAsync(node, context);

        // Should have 2 visible adornments (A and C)
        Assert.Equal(2, node.AdornmentChildren.Count);
    }

    [Fact]
    public async Task Reconcile_ValidationAdornment_ShowsErrorIndicator()
    {
        // When a field has validators and the value is invalid,
        // the validation adornment should appear as a child.
        var widget = new FormTextFieldWidget("field1", "Name")
            .Validate(v => string.IsNullOrEmpty(v)
                ? ValidationResult.Error("Required")
                : ValidationResult.Valid);

        var context = ReconcileContext.CreateRoot();
        var node = (FormTextFieldNode)await widget.ReconcileAsync(null, context);

        // Field starts empty — validation should produce an error adornment
        // The validation adornment is sync so it resolves immediately
        await Task.Delay(50);
        node = (FormTextFieldNode)await widget.ReconcileAsync(node, context);

        // Should have the validation adornment visible
        Assert.NotEmpty(node.AdornmentChildren);
    }

    [Fact]
    public void FormTextFieldWidget_Adornment_FluentMethod_AddsToList()
    {
        var widget = new FormTextFieldWidget("field1", "Name")
            .Adornment(async (v, ct) => true, () => new TextBlockWidget("A"))
            .Adornment(async (v, ct) => false, () => new TextBlockWidget("B"));

        Assert.Equal(2, widget.Adornments.Count);
    }

    [Fact]
    public void FormTextFieldWidget_Adornment_WithoutCancellation_Works()
    {
        var widget = new FormTextFieldWidget("field1", "Name")
            .Adornment(async v => true, () => new TextBlockWidget("A"));

        Assert.Single(widget.Adornments);
    }

    #endregion
}
