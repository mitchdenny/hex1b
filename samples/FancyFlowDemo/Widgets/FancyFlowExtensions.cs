using FancyFlowDemo.Flow;
using FancyFlowDemo.State;
using Hex1b;
using Hex1b.Flow;
using Hex1b.Widgets;

namespace FancyFlowDemo.Widgets;

/// <summary>
/// Fluent <c>ctx.X(...)</c> entry points for the FancyFlowDemo composites so
/// orchestration code never <c>new</c>'s a widget directly.
/// </summary>
internal static class FancyFlowExtensions
{
    public static TemplatePromptWidget TemplatePrompt<T>(
        this WidgetContext<T> context,
        int stepNumber,
        string title,
        string description,
        Hex1bWidget content) where T : Hex1bWidget
        => new(stepNumber, title, description, content);

    public static LanguagePromptWidget LanguagePrompt<T>(
        this WidgetContext<T> context,
        FlowStepContext step,
        FancyFlowCancellation cancel,
        FancyFlowSelections selections,
        IReadOnlyList<string> languages) where T : Hex1bWidget
        => new(step, cancel, selections, languages);

    public static TemplatePromptStepWidget TemplatePromptStep<T>(
        this WidgetContext<T> context,
        FlowStepContext step,
        FancyFlowCancellation cancel,
        FancyFlowSelections selections,
        IReadOnlyList<(string Id, string Name, string Description)> templates) where T : Hex1bWidget
        => new(step, cancel, selections, templates);

    public static FolderPromptWidget FolderPrompt<T>(
        this WidgetContext<T> context,
        FlowStepContext step,
        FancyFlowCancellation cancel,
        FancyFlowSelections selections) where T : Hex1bWidget
        => new(step, cancel, selections);

    public static HostnamePromptWidget HostnamePrompt<T>(
        this WidgetContext<T> context,
        FlowStepContext step,
        FancyFlowCancellation cancel,
        FancyFlowSelections selections,
        IReadOnlyList<string> patterns) where T : Hex1bWidget
        => new(step, cancel, selections, patterns);
}
