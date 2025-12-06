using Custard.Layout;

namespace Custard;

public sealed class TextBlockNode : CustardNode
{
    public string Text { get; set; } = "";

    public override Size Measure(Constraints constraints)
    {
        // TextBlock is single-line, width is text length, height is 1
        var width = Text.Length;
        var height = 1;
        return constraints.Constrain(new Size(width, height));
    }

    public override void Render(CustardRenderContext context)
    {
        context.Write(Text);
    }
}
