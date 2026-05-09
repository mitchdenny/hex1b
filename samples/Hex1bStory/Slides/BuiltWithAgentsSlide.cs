using Hex1b;
using Hex1b.Widgets;

namespace Hex1bStory.Slides;

/// <summary>
/// The hook of the talk: how the codebase came together with coding agents
/// in the loop. Replace the bullets with whatever stories you want to tell.
/// </summary>
internal sealed class BuiltWithAgentsSlide : ISlide
{
    public string Title => "Built with agents";

    public Hex1bWidget Build(SlideContext context) =>
        PlaceholderSlide.Build(
            context,
            "Built with agents",
            "Worktree-per-feature workflow with autopilot mode",
            "Repo-local skills (widget-creator, test-fixer, …) keep agents on-pattern",
            "Roslyn analyzers (HEX1B0001+) make conventions enforceable, not aspirational",
            "Tests-as-spec: the unit suite is the contract agents iterate against");
}
