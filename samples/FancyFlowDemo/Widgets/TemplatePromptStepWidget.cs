using FancyFlowDemo.Flow;
using FancyFlowDemo.State;
using Hex1b;
using Hex1b.Composition;
using Hex1b.Flow;
using Hex1b.Widgets;

namespace FancyFlowDemo.Widgets;

/// <summary>
/// Step 2 — picks the project template.
/// </summary>
internal sealed record TemplatePromptStepWidget(
    FlowStepContext Step,
    FancyFlowCancellation Cancel,
    FancyFlowSelections Selections,
    IReadOnlyList<(string Id, string Name, string Description)> Templates) : Hex1bWidget
{
    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        var labels = Templates.Select(t => $"{t.Name}  —  {t.Description}").ToArray();

        var list = ctx.List(labels)
            .OnItemActivated(e =>
            {
                var picked = Templates[e.ActivatedIndex];
                Selections.TemplateId = picked.Id;
                Selections.TemplateName = picked.Name;
                Step.Step.Complete(y => TombstoneFactory.Build(y, $"✓ Template: {picked.Name}"));
            })
            .FixedHeight(Templates.Count + 1);

        var content = PromptBorder.Wrap(ctx, list);

        return ctx.TemplatePrompt(
                stepNumber: 2,
                title: "Pick a template",
                description: "The starter project layout that will be scaffolded.",
                content: content)
            .ExitOnCtrlC(Cancel, Step);
    }
}
