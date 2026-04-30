using Hex1b;
using Hex1b.Widgets;

namespace CloudTermDemo;

/// <summary>
/// Wraps panel content in a bordered container with a title.
/// Panel keybindings (expand/shrink/fullscreen) will be added
/// progressively as the user advances through the tutorial.
/// </summary>
public static class CloudShellPanel
{
    public static Hex1bWidget Build<TParent>(
        WidgetContext<TParent> ctx,
        string title,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> contentBuilder)
        where TParent : Hex1bWidget
    {
        return ctx.Border(contentBuilder).Title(title).Fill();
    }
}
