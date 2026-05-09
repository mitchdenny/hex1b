using Hex1b;
using Hex1b.Widgets;

namespace Hex1bStory.Slides;

/// <summary>
/// Cue slide for switching to a live editor / terminal for a quick
/// codebase walk. Keep it sparse so you're not reading off it.
/// </summary>
internal sealed class CodebaseTourSlide : ISlide
{
    public string Title => "Codebase tour";

    public Hex1bWidget Build(SlideContext context) =>
        PlaceholderSlide.Build(
            context,
            "Codebase tour",
            "src/Hex1b/             — the shipped library",
            "src/Hex1b.Analyzers/   — Roslyn rules that enforce conventions",
            "tests/Hex1b.Tests/     — widget + node unit tests",
            "samples/               — runnable demos (you're inside one now!)",
            "apphost.cs             — Aspire orchestration of the samples");
}
