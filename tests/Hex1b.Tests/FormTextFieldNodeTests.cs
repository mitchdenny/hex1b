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
    public void GetChildren_ReturnsAllChildren()
    {
        var label = new TextBlockNode { Text = "Name" };
        var input = new TextBoxNode();
        var error = new TextBlockNode { Text = "!" };

        var node = new FormTextFieldNode
        {
            LabelChild = label,
            InputChild = input,
            ErrorIndicatorChild = error
        };

        var children = node.GetChildren().ToList();

        Assert.Equal(3, children.Count);
        Assert.Contains(label, children);
        Assert.Contains(input, children);
        Assert.Contains(error, children);
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
    public async Task Reconcile_NoErrorIndicatorWhenValid()
    {
        var widget = new FormTextFieldWidget("field1", "Name");
        var context = ReconcileContext.CreateRoot();

        var node = (FormTextFieldNode)await widget.ReconcileAsync(null, context);

        Assert.Null(node.ErrorIndicatorChild);
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
}
