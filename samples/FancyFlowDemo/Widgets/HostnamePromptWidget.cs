using FancyFlowDemo.Flow;
using FancyFlowDemo.State;
using Hex1b;
using Hex1b.Composition;
using Hex1b.Flow;
using Hex1b.Input;
using Hex1b.Widgets;

namespace FancyFlowDemo.Widgets;

/// <summary>
/// Step 4 — picks the hostname pattern via a <see cref="ToggleSwitchWidget"/>.
/// Left/right arrows switch options; Enter confirms.
/// </summary>
internal sealed record HostnamePromptWidget(
    FlowStepContext Step,
    FancyFlowCancellation Cancel,
    FancyFlowSelections Selections,
    IReadOnlyList<string> Patterns) : Hex1bWidget
{
    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        var initialIndex = Math.Max(0, Patterns.ToList().IndexOf(Selections.HostnamePattern));

        var toggle = ctx.ToggleSwitch(Patterns, initialIndex)
            .OnSelectionChanged(e =>
            {
                Selections.HostnamePattern = e.SelectedOption;
            })
            .InputBindings(b =>
            {
                b.Key(Hex1bKey.Enter).Action(_ =>
                {
                    Step.Step.Complete(y => TombstoneFactory.Build(y, $"✓ Hostname: {Selections.HostnamePattern}"));
                }, "Confirm");
            });

        var content = PromptBorder.Wrap(ctx, toggle);

        return ctx.TemplatePrompt(
                stepNumber: 4,
                title: "Pick a hostname pattern",
                description: "Use ←/→ to switch. Enter to confirm.",
                content: content)
            .ExitOnCtrlC(Cancel, Step);
    }
}
