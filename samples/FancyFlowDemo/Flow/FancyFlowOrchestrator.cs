using FancyFlowDemo.State;
using FancyFlowDemo.Widgets;
using Hex1b;

namespace FancyFlowDemo.Flow;

/// <summary>
/// Drives the FancyFlowDemo prompt sequence end-to-end. The orchestrator owns
/// the terminal builder, the flow callback, and the post-flow summary so the
/// <c>Program.cs</c> entry point can stay a single line.
/// </summary>
internal static class FancyFlowOrchestrator
{
    private static readonly string[] Languages = ["C#", "TypeScript", "Python", "F#"];

    private static readonly (string Id, string Name, string Description)[] Templates =
    [
        ("aspire-starter",       "Starter (ASP.NET Core + Blazor)",   "An ASP.NET Core API and a Blazor frontend."),
        ("aspire-react-starter", "Starter (ASP.NET Core + React)",    "An ASP.NET Core API and a Vite/React frontend."),
        ("aspire-py-starter",    "Starter (FastAPI + React)",         "A FastAPI backend and a Vite/React frontend."),
        ("aspire-empty",         "Empty AppHost",                     "Just an Aspire AppHost — bring your own services."),
    ];

    private static readonly string[] HostnamePatterns = ["localhost", "*.dev.localhost"];

    public static async Task RunAsync()
    {
        var cancel = new FancyFlowCancellation();
        var selections = new FancyFlowSelections();

        try
        {
            var cursorRow = Console.GetCursorPosition().Top;

            // Note: deliberately not `await using` — disposing the terminal emits
            // the alternate-screen exit sequence (ESC[?1049l) which most terminals
            // interpret as "restore the pre-flow buffer", visually clearing the
            // inline flow output we just wrote. Letting the terminal go out of
            // scope without explicit dispose keeps the rendered flow on screen,
            // matching the FlowDemo/NewCommand pattern.
            await Hex1bTerminal.CreateBuilder()
                .WithScrollback()
                .WithHex1bFlow(async flow =>
                {
                    // Blank bar-only row above the first prompt to give the gutter
                    // somewhere to "start" before any tombstone is emitted.
                    await flow.ShowAsync(ctx => TombstoneFactory.BuildBarRow(ctx));

                    await RunStepAsync(flow, ctx => ctx.LanguagePrompt(ctx, cancel, selections, Languages), Languages.Length + 9);
                    cancel.ThrowIfCancelled();

                    await RunStepAsync(flow, ctx => ctx.TemplatePromptStep(ctx, cancel, selections, Templates), Templates.Length + 9);
                    cancel.ThrowIfCancelled();

                    await RunStepAsync(flow, ctx => ctx.FolderPrompt(ctx, cancel, selections), 11);
                    cancel.ThrowIfCancelled();

                    await RunStepAsync(flow, ctx => ctx.HostnamePrompt(ctx, cancel, selections, HostnamePatterns), 9);
                    cancel.ThrowIfCancelled();
                },
                options =>
                {
                    options.InitialCursorRow = cursorRow;
                })
                .Build()
                .RunAsync();

            cancel.ThrowIfCancelled();

            PrintSummary(selections);
        }
        catch (OperationCanceledException) when (cancel.IsCancelled)
        {
            Console.WriteLine();
            Console.WriteLine("✗ Cancelled. Goodbye!");
        }
    }

    private static Task RunStepAsync(
        Hex1b.Flow.Hex1bFlowContext flow,
        Func<Hex1b.Flow.FlowStepContext, Hex1b.Widgets.Hex1bWidget> builder,
        int maxHeight)
    {
        var step = flow.Step(builder, opts => opts.MaxHeight = maxHeight);
        return step.WaitForCompletionAsync();
    }

    private static void PrintSummary(FancyFlowSelections selections)
    {
        Console.WriteLine();
        Console.WriteLine("✨ All set! Here's what we'll create:");
        Console.WriteLine();
        Console.WriteLine($"   Language : {selections.Language}");
        Console.WriteLine($"   Template : {selections.TemplateName} ({selections.TemplateId})");
        Console.WriteLine($"   Folder   : {selections.Folder}");
        Console.WriteLine($"   Hostname : {selections.HostnamePattern}");
        Console.WriteLine();
    }
}
