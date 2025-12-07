using Hex1b.Layout;

namespace Hex1b;

public sealed class TextBlockNode : Hex1bNode
{
    public string Text { get; set; } = "";

    public override Size Measure(Constraints constraints)
    {
        // TextBlock is single-line, width is text length, height is 1
        var width = Text.Length;
        var height = 1;
        return constraints.Constrain(new Size(width, height));
    }

    public override void Render(Hex1bRenderContext context)
    {
        context.Write(Text);
    }
}
