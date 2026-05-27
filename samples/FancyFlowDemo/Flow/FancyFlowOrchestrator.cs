using FancyFlowDemo.State;
using FancyFlowDemo.Widgets;
using Hex1b;
using Hex1b.Theming;

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
                    // Opening "header" marker — anchors the start of the flow
                    // and gives the gutter somewhere to begin.
                    await flow.ShowAsync(ctx => TombstoneFactory.BuildHeader(ctx, "Create a new app"));

                    try
                    {
                        await RunStepAsync(flow, ctx => ctx.LanguagePrompt(ctx, cancel, selections, Languages), Languages.Length + 9);
                        cancel.ThrowIfCancelled();

                        await RunStepAsync(flow, ctx => ctx.TemplatePromptStep(ctx, cancel, selections, Templates), Templates.Length + 9);
                        cancel.ThrowIfCancelled();

                        await RunStepAsync(flow, ctx => ctx.FolderPrompt(ctx, cancel, selections), 11);
                        cancel.ThrowIfCancelled();

                        await RunStepAsync(flow, ctx => ctx.HostnamePrompt(ctx, cancel, selections, HostnamePatterns), 9);
                        cancel.ThrowIfCancelled();

                        // Simulated work between the final prompt and the success
                        // outcome — keeps the flow feeling like a real scaffolder.
                        await RunStepAsync(flow, ctx => ctx.ProcessingStep(ctx, cancel, selections), 9);
                        cancel.ThrowIfCancelled();

                        await flow.ShowAsync(ctx => TombstoneFactory.BuildOutcome(
                            ctx,
                            TombstoneFactory.SuccessMarker,
                            TombstoneFactory.SuccessColor,
                            "All set — your app is ready.",
                            $"Language : {selections.Language}",
                            $"Template : {selections.TemplateName} ({selections.TemplateId})",
                            $"Folder   : {selections.Folder}",
                            $"Hostname : {selections.HostnamePattern}"));
                    }
                    catch (OperationCanceledException) when (cancel.IsCancelled)
                    {
                        // Render the cancel outcome inline so it sits naturally
                        // beneath whichever prompt the user bailed from, then
                        // rethrow so the outer handler can short-circuit cleanly.
                        await flow.ShowAsync(ctx => TombstoneFactory.BuildOutcome(
                            ctx,
                            TombstoneFactory.CancelMarker,
                            TombstoneFactory.CancelColor,
                            "Cancelled. No app was created."));
                        throw;
                    }
                },
                options =>
                {
                    options.InitialCursorRow = cursorRow;

                    // Emit completed steps as soft-wrap tombstones (proper
                    // logical lines that the host terminal reflows) and
                    // turn on the track-and-clear settle-based resize
                    // pipeline. Without this flag the runner falls back
                    // to the legacy eager-repaint resize path which
                    // re-emits the active step on every resize event —
                    // catastrophic during a drag-resize burst.
                    options.UseSoftWrapTombstones = true;

                    // Hold the live step still during interactive drag-resize.
                    // The runner tracks cursor position internally on every
                    // resize event but does not mutate the screen — letting
                    // the host terminal's own reflow keep the tombstones
                    // above the active step intact. The actual repaint of
                    // the live step is debounced until events settle.
                    options.ResizeSettleDelay = TimeSpan.FromMilliseconds(80);
                })
                .Build()
                .RunAsync();

            cancel.ThrowIfCancelled();
        }
        catch (OperationCanceledException) when (cancel.IsCancelled)
        {
            // Cancel outcome was already rendered inside the flow; nothing
            // more to do — the inline tombstone is the final word.
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
}
