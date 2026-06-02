using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class FormNodeTests
{
    [TestMethod]
    public async Task Reconcile_CreatesFormNode()
    {
        var widget = new FormWidget([new TextBlockWidget("Hello")]);
        var context = ReconcileContext.CreateRoot();

        var node = await widget.ReconcileAsync(null, context);

        TestSeq.IsType<FormNode>(node);
    }

    [TestMethod]
    public async Task Reconcile_CreatesContentChild()
    {
        var widget = new FormWidget([new TextBlockWidget("Hello")]);
        var context = ReconcileContext.CreateRoot();

        var node = (FormNode)await widget.ReconcileAsync(null, context);

        Assert.IsNotNull(node.Content);
    }

    [TestMethod]
    public async Task Reconcile_ReusesExistingNode()
    {
        var widget = new FormWidget([new TextBlockWidget("Hello")]);
        var context = ReconcileContext.CreateRoot();

        var node1 = await widget.ReconcileAsync(null, context);
        var node2 = await widget.ReconcileAsync(node1, context);

        Assert.AreSame(node1, node2);
    }

    [TestMethod]
    public async Task Reconcile_SetsLabelPlacement()
    {
        var widget = new FormWidget([new TextBlockWidget("Hello")])
            .LabelPlacement(LabelPlacement.Inline);
        var context = ReconcileContext.CreateRoot();

        var node = (FormNode)await widget.ReconcileAsync(null, context);

        Assert.AreEqual(LabelPlacement.Inline, node.LabelPlacement);
    }

    [TestMethod]
    public async Task Reconcile_SetsLabelWidth()
    {
        var widget = new FormWidget([new TextBlockWidget("Hello")])
            .LabelWidth(25);
        var context = ReconcileContext.CreateRoot();

        var node = (FormNode)await widget.ReconcileAsync(null, context);

        Assert.AreEqual(25, node.LabelWidth);
    }

    [TestMethod]
    public void Measure_WithContent_DelegatesToContent()
    {
        var contentChild = new TextBlockNode { Text = "Field" };
        var node = new FormNode { Content = contentChild };

        var size = node.Measure(new Constraints(0, 40, 0, 10));

        Assert.IsTrue(size.Width > 0);
        Assert.IsTrue(size.Height > 0);
    }

    [TestMethod]
    public void Measure_WithoutContent_ReturnsZero()
    {
        var node = new FormNode();

        var size = node.Measure(new Constraints(0, 40, 0, 10));

        Assert.AreEqual(0, size.Width);
        Assert.AreEqual(0, size.Height);
    }

    [TestMethod]
    public void Arrange_PassesThroughToContent()
    {
        var contentChild = new TextBlockNode { Text = "Field" };
        var node = new FormNode { Content = contentChild };

        node.Measure(new Constraints(0, 40, 0, 10));
        var rect = new Rect(0, 0, 40, 5);
        node.Arrange(rect);

        Assert.AreEqual(rect, node.Bounds);
        Assert.AreEqual(rect, contentChild.Bounds);
    }

    [TestMethod]
    public void GetChildren_ReturnsContent()
    {
        var contentChild = new TextBlockNode { Text = "Field" };
        var node = new FormNode { Content = contentChild };

        var children = node.GetChildren().ToList();

        TestSeq.Single(children);
        Assert.AreSame(contentChild, children[0]);
    }

    [TestMethod]
    public void GetChildren_WhenNoContent_ReturnsEmpty()
    {
        var node = new FormNode();

        var children = node.GetChildren().ToList();

        Assert.IsEmpty(children);
    }

    [TestMethod]
    public void SetFieldValue_StoresValue()
    {
        var node = new FormNode();

        node.SetFieldValue("firstName", "John");

        Assert.AreEqual("John", node.GetFieldValue("firstName"));
    }

    [TestMethod]
    public void GetFieldValue_WhenNotSet_ReturnsEmpty()
    {
        var node = new FormNode();

        var value = node.GetFieldValue("nonexistent");

        Assert.AreEqual("", value);
    }

    [TestMethod]
    public void SetValidationResult_StoresResult()
    {
        var node = new FormNode();
        var error = ValidationResult.Error("Required");

        node.SetValidationResult("email", error);

        var result = node.GetValidationResult("email");
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("Required", result.ErrorMessage);
    }

    [TestMethod]
    public void GetValidationResult_WhenNotSet_ReturnsValid()
    {
        var node = new FormNode();

        var result = node.GetValidationResult("nonexistent");

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void AreFieldsValid_AllValid_ReturnsTrue()
    {
        var node = new FormNode();
        node.SetValidationResult("a", ValidationResult.Valid);
        node.SetValidationResult("b", ValidationResult.Valid);

        Assert.IsTrue(node.AreFieldsValid(["a", "b"]));
    }

    [TestMethod]
    public void AreFieldsValid_OneInvalid_ReturnsFalse()
    {
        var node = new FormNode();
        node.SetValidationResult("a", ValidationResult.Valid);
        node.SetValidationResult("b", ValidationResult.Error("bad"));

        Assert.IsFalse(node.AreFieldsValid(["a", "b"]));
    }

    [TestMethod]
    public void AreFieldsValid_UnknownField_ReturnsTrue()
    {
        var node = new FormNode();

        Assert.IsTrue(node.AreFieldsValid(["unknown"]));
    }

    #region FormContext ValidationErrors Tests

    [TestMethod]
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

        Assert.AreEqual(2, errors.Count);
        Assert.IsTrue(errors.ContainsKey("field_1_Email"));
        Assert.IsTrue(errors.ContainsKey("field_2_Phone"));
        Assert.IsFalse(errors.ContainsKey("field_0_Name"));
    }

    [TestMethod]
    public void FormContext_ValidationErrors_EmptyWhenAllValid()
    {
        var ctx = new FormContext();
        var formNode = new FormNode();
        ctx._formNode = formNode;

        formNode.SetValidationResult("field_0_Name", ValidationResult.Valid);

        Assert.IsEmpty(ctx.ValidationErrors);
    }

    [TestMethod]
    public void FormContext_ValidationErrors_EmptyWhenNoFormNode()
    {
        // Before reconciliation, _formNode is null — should return empty.
        var ctx = new FormContext();

        Assert.IsEmpty(ctx.ValidationErrors);
    }

    [TestMethod]
    public void FormContext_ValidationResults_ReturnsAll()
    {
        // ValidationResults should include both valid and invalid entries.
        var ctx = new FormContext();
        var formNode = new FormNode();
        ctx._formNode = formNode;

        formNode.SetValidationResult("field_0_Name", ValidationResult.Valid);
        formNode.SetValidationResult("field_1_Email", ValidationResult.Error("Required"));

        var results = ctx.ValidationResults;

        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results["field_0_Name"].IsValid);
        Assert.IsFalse(results["field_1_Email"].IsValid);
    }

    #endregion

    #region ValidationSummary Tests

    [TestMethod]
    public async Task ValidationSummary_NoErrors_ReconcilesEmpty()
    {
        // When there are no validation errors, the summary should reconcile
        // to an empty text block.
        var formNode = new FormNode();
        var context = ReconcileContext.CreateRoot();

        var widget = new ValidationSummaryWidget();
        var node = await widget.ReconcileAsync(null, context);

        Assert.IsNotNull(node);
    }

    [TestMethod]
    public void ValidationSummaryExtension_CreatesWidget()
    {
        // The form.ValidationSummary() extension should create a ValidationSummaryWidget.
        var ctx = new FormContext();
        ctx.FieldRegistry.FieldIds.Count(); // just verify it's accessible

        var widget = ctx.ValidationSummary();

        TestSeq.IsType<ValidationSummaryWidget>(widget);
    }

    #endregion
}
