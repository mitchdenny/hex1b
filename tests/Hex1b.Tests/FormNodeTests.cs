using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class FormNodeTests
{
    [Fact]
    public async Task Reconcile_CreatesFormNode()
    {
        var widget = new FormWidget([new TextBlockWidget("Hello")]);
        var context = ReconcileContext.CreateRoot();

        var node = await widget.ReconcileAsync(null, context);

        Assert.IsType<FormNode>(node);
    }

    [Fact]
    public async Task Reconcile_CreatesContentChild()
    {
        var widget = new FormWidget([new TextBlockWidget("Hello")]);
        var context = ReconcileContext.CreateRoot();

        var node = (FormNode)await widget.ReconcileAsync(null, context);

        Assert.NotNull(node.Content);
    }

    [Fact]
    public async Task Reconcile_ReusesExistingNode()
    {
        var widget = new FormWidget([new TextBlockWidget("Hello")]);
        var context = ReconcileContext.CreateRoot();

        var node1 = await widget.ReconcileAsync(null, context);
        var node2 = await widget.ReconcileAsync(node1, context);

        Assert.Same(node1, node2);
    }

    [Fact]
    public async Task Reconcile_SetsLabelPlacement()
    {
        var widget = new FormWidget([new TextBlockWidget("Hello")])
            .WithLabelPlacement(LabelPlacement.Inline);
        var context = ReconcileContext.CreateRoot();

        var node = (FormNode)await widget.ReconcileAsync(null, context);

        Assert.Equal(LabelPlacement.Inline, node.LabelPlacement);
    }

    [Fact]
    public async Task Reconcile_SetsLabelWidth()
    {
        var widget = new FormWidget([new TextBlockWidget("Hello")])
            .WithLabelWidth(25);
        var context = ReconcileContext.CreateRoot();

        var node = (FormNode)await widget.ReconcileAsync(null, context);

        Assert.Equal(25, node.LabelWidth);
    }

    [Fact]
    public void Measure_WithContent_DelegatesToContent()
    {
        var contentChild = new TextBlockNode { Text = "Field" };
        var node = new FormNode { Content = contentChild };

        var size = node.Measure(new Constraints(0, 40, 0, 10));

        Assert.True(size.Width > 0);
        Assert.True(size.Height > 0);
    }

    [Fact]
    public void Measure_WithoutContent_ReturnsZero()
    {
        var node = new FormNode();

        var size = node.Measure(new Constraints(0, 40, 0, 10));

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void Arrange_PassesThroughToContent()
    {
        var contentChild = new TextBlockNode { Text = "Field" };
        var node = new FormNode { Content = contentChild };

        node.Measure(new Constraints(0, 40, 0, 10));
        var rect = new Rect(0, 0, 40, 5);
        node.Arrange(rect);

        Assert.Equal(rect, node.Bounds);
        Assert.Equal(rect, contentChild.Bounds);
    }

    [Fact]
    public void GetChildren_ReturnsContent()
    {
        var contentChild = new TextBlockNode { Text = "Field" };
        var node = new FormNode { Content = contentChild };

        var children = node.GetChildren().ToList();

        Assert.Single(children);
        Assert.Same(contentChild, children[0]);
    }

    [Fact]
    public void GetChildren_WhenNoContent_ReturnsEmpty()
    {
        var node = new FormNode();

        var children = node.GetChildren().ToList();

        Assert.Empty(children);
    }

    [Fact]
    public void SetFieldValue_StoresValue()
    {
        var node = new FormNode();

        node.SetFieldValue("firstName", "John");

        Assert.Equal("John", node.GetFieldValue("firstName"));
    }

    [Fact]
    public void GetFieldValue_WhenNotSet_ReturnsEmpty()
    {
        var node = new FormNode();

        var value = node.GetFieldValue("nonexistent");

        Assert.Equal("", value);
    }

    [Fact]
    public void SetValidationResult_StoresResult()
    {
        var node = new FormNode();
        var error = ValidationResult.Error("Required");

        node.SetValidationResult("email", error);

        var result = node.GetValidationResult("email");
        Assert.False(result.IsValid);
        Assert.Equal("Required", result.ErrorMessage);
    }

    [Fact]
    public void GetValidationResult_WhenNotSet_ReturnsValid()
    {
        var node = new FormNode();

        var result = node.GetValidationResult("nonexistent");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void AreFieldsValid_AllValid_ReturnsTrue()
    {
        var node = new FormNode();
        node.SetValidationResult("a", ValidationResult.Valid);
        node.SetValidationResult("b", ValidationResult.Valid);

        Assert.True(node.AreFieldsValid(["a", "b"]));
    }

    [Fact]
    public void AreFieldsValid_OneInvalid_ReturnsFalse()
    {
        var node = new FormNode();
        node.SetValidationResult("a", ValidationResult.Valid);
        node.SetValidationResult("b", ValidationResult.Error("bad"));

        Assert.False(node.AreFieldsValid(["a", "b"]));
    }

    [Fact]
    public void AreFieldsValid_UnknownField_ReturnsTrue()
    {
        var node = new FormNode();

        Assert.True(node.AreFieldsValid(["unknown"]));
    }

    #region FormContext ValidationErrors Tests

    [Fact]
    public void FormContext_ValidationErrors_ReturnsOnlyErrors()
    {
        // FormContext.ValidationErrors should only include fields with errors, not valid ones.
        var ctx = new FormContext();
        var formNode = new FormNode();
        ctx._formNode = formNode;

        formNode.SetValidationResult("field_0_Name", ValidationResult.Valid);
        formNode.SetValidationResult("field_1_Email", ValidationResult.Error("Required"));
        formNode.SetValidationResult("field_2_Phone", ValidationResult.Error("Invalid"));

        var errors = ctx.ValidationErrors;

        Assert.Equal(2, errors.Count);
        Assert.True(errors.ContainsKey("field_1_Email"));
        Assert.True(errors.ContainsKey("field_2_Phone"));
        Assert.False(errors.ContainsKey("field_0_Name"));
    }

    [Fact]
    public void FormContext_ValidationErrors_EmptyWhenAllValid()
    {
        var ctx = new FormContext();
        var formNode = new FormNode();
        ctx._formNode = formNode;

        formNode.SetValidationResult("field_0_Name", ValidationResult.Valid);

        Assert.Empty(ctx.ValidationErrors);
    }

    [Fact]
    public void FormContext_ValidationErrors_EmptyWhenNoFormNode()
    {
        // Before reconciliation, _formNode is null — should return empty.
        var ctx = new FormContext();

        Assert.Empty(ctx.ValidationErrors);
    }

    [Fact]
    public void FormContext_ValidationResults_ReturnsAll()
    {
        // ValidationResults should include both valid and invalid entries.
        var ctx = new FormContext();
        var formNode = new FormNode();
        ctx._formNode = formNode;

        formNode.SetValidationResult("field_0_Name", ValidationResult.Valid);
        formNode.SetValidationResult("field_1_Email", ValidationResult.Error("Required"));

        var results = ctx.ValidationResults;

        Assert.Equal(2, results.Count);
        Assert.True(results["field_0_Name"].IsValid);
        Assert.False(results["field_1_Email"].IsValid);
    }

    #endregion

    #region ValidationSummary Tests

    [Fact]
    public async Task ValidationSummary_NoErrors_ReconcilesEmpty()
    {
        // When there are no validation errors, the summary should reconcile
        // to an empty text block.
        var formNode = new FormNode();
        var context = ReconcileContext.CreateRoot();

        var widget = new ValidationSummaryWidget();
        var node = await widget.ReconcileAsync(null, context);

        Assert.NotNull(node);
    }

    [Fact]
    public void ValidationSummaryExtension_CreatesWidget()
    {
        // The form.ValidationSummary() extension should create a ValidationSummaryWidget.
        var ctx = new FormContext();
        ctx.FieldRegistry.FieldIds.Count(); // just verify it's accessible

        var widget = ctx.ValidationSummary();

        Assert.IsType<ValidationSummaryWidget>(widget);
    }

    #endregion
}
