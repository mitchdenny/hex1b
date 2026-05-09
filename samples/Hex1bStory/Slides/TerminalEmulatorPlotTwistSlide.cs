using Hex1b;
using Hex1b.Widgets;

namespace Hex1bStory.Slides;

/// <summary>
/// "Plot twist: the bigger win wasn't the framework." Visually contrasts the
/// (small) TUI framework portion of Hex1b with the (much larger) terminal
/// emulator underneath, to set up the conformance / Aspire / multi-headed
/// PTY beats that follow.
/// </summary>
internal sealed class TerminalEmulatorPlotTwistSlide : ISlide
{
    public string Title => "Plot twist";

    public Hex1bWidget Build(SlideContext context)
    {
        var ctx = context.Root;

        return ctx.VStack(v =>
        [
            v.Text("Plot twist"),
            v.Text("══════════"),
            v.Text(""),
            v.Text("The bigger win wasn't the TUI framework."),
            v.Text(""),
            v.Center(c => c.VStack(stack =>
            [
                stack.Border(stack.Padding(2, 0,
                    stack.Text("TUI framework")))
                    .Title(" what we set out to build "),
                stack.Text(""),
                stack.Border(stack.Padding(8, 1, stack.VStack(p =>
                [
                    p.Text("Terminal emulator"),
                    p.Text(""),
                    p.Text("xterm + vte + kitty + ghostty"),
                    p.Text("VT220 → modern, scrollback, mouse,"),
                    p.Text("OSC, sixel, PTY plumbing, the lot."),
                ])))
                    .Title(" what nobody else has "),
            ])),
            v.Text(""),
            v.Text("Most TUI frameworks sit on someone else's terminal."),
            v.Text("Hex1b owns its own — and that turns out to matter."),
        ]);
    }
}
