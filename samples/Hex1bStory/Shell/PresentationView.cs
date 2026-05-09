using Hex1b;
using Hex1b.Widgets;

namespace Hex1bStory.Shell;

/// <summary>
/// Frames the active slide with a small border of breathing room and pins
/// the page-number footer to the bottom of the screen.
/// </summary>
internal static class PresentationView
{
    public static Hex1bWidget Build(RootContext ctx, PresenterState state)
    {
        // Caller guarantees Current is set when Mode == Presenting.
        var playlist = state.Current!;
        var slide = playlist.Slides[state.SlideIndex];
        var slideCtx = new SlideContext(ctx, state);

        return ctx.VStack(v =>
        [
            v.Padding(2, 1, slide.Build(slideCtx)).Fill(),
            Footer.Build(v, state),
        ]);
    }
}
