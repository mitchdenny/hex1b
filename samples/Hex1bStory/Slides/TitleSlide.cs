using Hex1b;
using Hex1b.Widgets;

namespace Hex1bStory.Slides;

/// <summary>
/// Opening slide: headline + sub-title, centered.
/// Replace the strings here with the version of the title you want to use
/// on the day; the layout will adapt.
/// </summary>
internal sealed class TitleSlide : ISlide
{
    public string Title => "Title";

    public Hex1bWidget Build(SlideContext context)
    {
        var ctx = context.Root;
        return ctx.Center(c => c.VStack(v =>
        [
            v.Text("Hex1b"),
            v.Text(""),
            v.Text("A TUI library built (almost) entirely with coding agents"),
            v.Text(""),
            v.Text("— Mitch Denny"),
        ]));
    }
}
