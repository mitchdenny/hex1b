using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1bStory.Slides;

/// <summary>
/// "Bootstrap first" — the workflow lesson. Before writing any features,
/// the agent built the entire delivery pipeline. The slide animates a
/// list of "first commits" with a moving cursor; presenter steps it with
/// arrows or it just sits at the end if they don't.
/// </summary>
internal sealed class BootstrapFirstSlide : ISlide
{
    public string Title => "Bootstrap first";

    private static readonly (string Step, string Note)[] Steps =
    [
        ("CI",                 "Build everything. On every PR. From day one."),
        ("Release pipeline",   "NuGet push wired up before we had anything to push."),
        ("Versioning",         "GitVersion / nbgv decided early, never revisited."),
        ("Samples scaffold",   "An empty folder ready to fill — and fill it did."),
        ("First widget",       "Only NOW do we render a single character."),
    ];

    private int _stepCursor = Steps.Length - 1;

    public Hex1bWidget Build(SlideContext context)
    {
        var ctx = context.Root;

        return ctx.VStack(v =>
        [
            v.Text("Bootstrap first"),
            v.Text("════════════════"),
            v.Text(""),
            v.Text("The first thing the agent built wasn't a widget."),
            v.Text(""),
            v.HStack(row =>
            [
                row.VStack(left => BuildStepList(left)).FixedWidth(40),
                row.Text("  "),
                row.VStack(right =>
                [
                    right.Text("Why?"),
                    right.Text("────"),
                    right.Text(""),
                    right.Text("Clear the path for end-to-end delivery"),
                    right.Text("BEFORE writing the thing you'll deliver."),
                    right.Text(""),
                    right.Text("Agents are great at greenfield code — and"),
                    right.Text("terrible at patching half-broken pipelines."),
                    right.Text(""),
                    right.Text("Get the cycle time down to seconds first."),
                    right.Text("Then iterate on the actual product."),
                ]).Fill(),
            ]).Fill(),
            v.Text(""),
            v.Text("↑↓ step the cursor"),
        ]).InputBindings(b =>
        {
            b.Key(Hex1bKey.UpArrow).Global().Action(c =>
            {
                if (_stepCursor > 0) _stepCursor--;
                c.Invalidate();
            }, "Step back");
            b.Key(Hex1bKey.DownArrow).Global().Action(c =>
            {
                if (_stepCursor < Steps.Length - 1) _stepCursor++;
                c.Invalidate();
            }, "Step forward");
        });
    }

    private Hex1bWidget[] BuildStepList(WidgetContext<VStackWidget> ctx)
    {
        var rows = new Hex1bWidget[Steps.Length * 2];
        for (var i = 0; i < Steps.Length; i++)
        {
            var marker = i < _stepCursor ? "✓"
                       : i == _stepCursor ? "▸"
                       : "·";
            rows[i * 2] = ctx.Text($"  {marker}  {Steps[i].Step}");
            rows[i * 2 + 1] = ctx.Text(i == _stepCursor
                ? $"        {Steps[i].Note}"
                : "");
        }
        return rows;
    }
}
