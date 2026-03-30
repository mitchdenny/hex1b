using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="ValidationMessageWidget"/>.
/// Displays the validation error message when present.
/// </summary>
public sealed class ValidationMessageNode : Hex1bNode
{
    /// <summary>
    /// The field IDs being monitored.
    /// </summary>
    internal IReadOnlyList<string> FieldIds { get; set; } = [];

    /// <summary>
    /// The child node displaying the error message (null when all fields are valid).
    /// </summary>
    public Hex1bNode? MessageChild { get; set; }

    protected override Size MeasureCore(Constraints constraints)
    {
        if (MessageChild == null)
            return constraints.Constrain(Size.Zero);

        return MessageChild.Measure(constraints);
    }

    protected override void ArrangeCore(Rect rect)
    {
        base.ArrangeCore(rect);
        MessageChild?.Arrange(rect);
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (MessageChild != null)
            context.RenderChild(MessageChild);
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (MessageChild != null)
            yield return MessageChild;
    }
}
