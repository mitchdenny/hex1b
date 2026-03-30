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
}
