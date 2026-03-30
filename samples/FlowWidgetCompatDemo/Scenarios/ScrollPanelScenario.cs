using Hex1b;
using Hex1b.Flow;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace FlowWidgetCompatDemo.Scenarios;

internal class ScrollPanelScenario : IWidgetScenario
{
    public string Name => "ScrollPanel";
    public string Description => "Scrollable content panel with overflow";
    public int? MaxHeight => 15;

    public Hex1bWidget Build(FlowStepContext ctx)
    {
        return ctx.VStack(v =>
        [
            ctx.Text("Scrollable Content Demo"),
            ctx.VScrollPanel(sp =>
            {
                var lines = new Hex1bWidget[35];
                for (var i = 0; i < lines.Length; i++)
                {
                    lines[i] = ctx.Text($"Line {i + 1:D2}: This is scrollable content that extends beyond the visible area.");
                }

                return lines;
            }).FillHeight(),
        ]);
    }
}
