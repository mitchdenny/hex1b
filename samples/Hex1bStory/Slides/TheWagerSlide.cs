using Hex1b;
using Hex1b.Widgets;

namespace Hex1bStory.Slides;

/// <summary>
/// "The wager" — a deliberately punchy, centered, mostly-empty slide whose
/// only job is to land the framing of the entire talk: can you build a real
/// framework with agents, or is it just garbage?
/// </summary>
internal sealed class TheWagerSlide : ISlide
{
    public string Title => "The wager";

    public Hex1bWidget Build(SlideContext context)
    {
        var ctx = context.Root;

        return ctx.Center(c => c.VStack(v =>
        [
            v.Text("The wager"),
            v.Text(""),
            v.Text("\"Can you build a real framework from scratch with agents..."),
            v.Text(""),
            v.Text("                ...or is it just a steaming pile of garbage?\""),
            v.Text(""),
            v.Text(""),
            v.Text("December 2025  ·  Copilot CLI  ·  Opus had just landed."),
        ]));
    }
}
