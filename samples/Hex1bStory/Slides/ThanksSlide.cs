using Hex1b;
using Hex1b.Widgets;

namespace Hex1bStory.Slides;

/// <summary>
/// Closing slide. Centered, minimal — leaves space for live Q&amp;A.
/// </summary>
internal sealed class ThanksSlide : ISlide
{
    public string Title => "Thanks";

    public Hex1bWidget Build(SlideContext context)
    {
        var ctx = context.Root;
        return ctx.Center(c => c.VStack(v =>
        [
            v.Text("Thanks!"),
            v.Text(""),
            v.Text("Questions?"),
            v.Text(""),
            v.Text("github.com/mitchdenny/hex1b"),
        ]));
    }
}
