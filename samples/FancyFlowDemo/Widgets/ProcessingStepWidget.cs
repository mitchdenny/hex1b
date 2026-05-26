using FancyFlowDemo.Flow;
using FancyFlowDemo.State;
using Hex1b;
using Hex1b.Composition;
using Hex1b.Flow;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace FancyFlowDemo.Widgets;

/// <summary>
/// Step 5 — a simulated "doing the work" stage that sits between the final
/// prompt and the success outcome. Cycles through a series of phases with a
/// spinner and then auto-completes the flow step, emitting a tombstone that
/// matches the other steps' visual rhythm.
/// </summary>
internal sealed record ProcessingStepWidget(
    FlowStepContext Step,
    FancyFlowCancellation Cancel,
    FancyFlowSelections Selections) : Hex1bWidget
{
    // Tuneable simulated work: each phase shows for this many milliseconds
    // before advancing. The total dwell time is the sum of all phases.
    private static readonly (string Label, int DwellMs)[] Phases =
    [
        ("Resolving template…",     700),
        ("Scaffolding project…",    900),
        ("Restoring dependencies…", 1100),
        ("Wiring up AppHost…",      600),
    ];

    private sealed class ProcessingState
    {
        public int PhaseIndex;
        public bool Started;
    }

    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        var state = ctx.UseState(() => new ProcessingState());

        // Kick off the work the first time this composite is built. The
        // background task drives PhaseIndex forward, calls Step.Invalidate
        // after each phase, and finally completes the step with a normal
        // hollow-diamond tombstone.
        if (!state.Started)
        {
            state.Started = true;
            _ = RunPhasesAsync(state, Step);
        }

        var phaseLabel = state.PhaseIndex < Phases.Length
            ? Phases[state.PhaseIndex].Label
            : Phases[^1].Label;

        var body = ctx.HStack(h =>
        [
            h.ThemePanel(
                t => t.Set(SpinnerTheme.ForegroundColor, TemplatePromptWidget.ActiveColor),
                h.Spinner()),
            h.Text(" "),
            h.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, TemplatePromptWidget.BodyColor),
                h.Text(phaseLabel)),
        ]);

        var content = PromptBorder.Wrap(ctx, body);

        return ctx.TemplatePrompt(
                stepNumber: 5,
                title: "Creating your app",
                description: "Sit tight — this only takes a moment.",
                content: content);
    }

    private static async Task RunPhasesAsync(ProcessingState state, FlowStepContext stepContext)
    {
        try
        {
            for (var i = 0; i < Phases.Length; i++)
            {
                state.PhaseIndex = i;
                stepContext.Step.Invalidate();
                await Task.Delay(Phases[i].DwellMs).ConfigureAwait(false);
            }

            stepContext.Step.Complete(y => TombstoneFactory.Build(y, "✓ Project created"));
        }
        catch
        {
            // Background failures during the simulated phases shouldn't bring
            // down the flow — just leave the step in its current phase.
        }
    }
}
