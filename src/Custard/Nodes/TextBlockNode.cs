namespace Custard;

public sealed class TextBlockNode : CustardNode
{
    public string Text { get; set; } = "";

    public override void Render(CustardRenderContext context)
    {
        context.Write(Text);
    }
}
