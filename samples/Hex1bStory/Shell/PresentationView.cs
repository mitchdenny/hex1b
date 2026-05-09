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

        // Slides receive the OUTER terminal dimensions so they can size
        // embedded widgets (notably TerminalWidgets in the samples graveyard
        // slide) sensibly. Console.Window* matches what Hex1b's TTY workload
        // adapter sees on real terminals; we fall back to a reasonable
        // default in non-interactive contexts so the deck never crashes.
        int width, height;
        try
        {
            width = Console.WindowWidth;
            height = Console.WindowHeight;
        }
        catch
        {
            width = 80;
            height = 24;
        }

        var slideCtx = new SlideContext(ctx, state, width, height);

        return ctx.VStack(v =>
        [
            v.Padding(2, 1, slide.Build(slideCtx)).Fill(),
            Footer.Build(v, state),
        ]);
    }
}
