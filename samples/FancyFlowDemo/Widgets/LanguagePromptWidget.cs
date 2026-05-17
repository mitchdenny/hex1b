using FancyFlowDemo.Flow;
using FancyFlowDemo.State;
using Hex1b;
using Hex1b.Composition;
using Hex1b.Flow;
using Hex1b.Widgets;

namespace FancyFlowDemo.Widgets;

/// <summary>
/// Step 1 — picks the project language. Wraps a <see cref="ListWidget"/> in a
/// <see cref="TemplatePromptWidget"/> for consistent decoration.
/// </summary>
internal sealed record LanguagePromptWidget(
    FlowStepContext Step,
    FancyFlowCancellation Cancel,
    FancyFlowSelections Selections,
    IReadOnlyList<string> Languages) : Hex1bWidget
{
    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        var list = ctx.List(Languages)
            .OnItemActivated(e =>
            {
                Selections.Language = Languages[e.ActivatedIndex];
                Step.Step.Complete(y => TombstoneFactory.Build(y, $"✓ Language: {Selections.Language}"));
            })
            .FixedHeight(Languages.Count + 1);

        var content = PromptBorder.Wrap(ctx, list);

        return ctx.TemplatePrompt(
                stepNumber: 1,
                title: "Pick a language",
                description: "Used for the AppHost project and any sample code.",
                content: content)
            .ExitOnCtrlC(Cancel, Step);
    }
}
