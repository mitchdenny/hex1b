using Hex1b;
using Hex1b.Flow;
using Hex1b.Widgets;

namespace FlowDemo.Commands;

/// <summary>
/// Implements the interactive "flowdemo agent init" command, mocking the aspire agent init experience.
/// </summary>
internal static class HandrolledCommand
{
    public static async Task RunAsync()
    {
        var builder = Hex1bTerminal.CreateBuilder()
            .WithHex1bFlow(async flow =>
            {
                await flow.WizardTitleAsync();
                await flow.WizardSpacerAsync();
                await flow.WizardProcessingAsync();
                await flow.WizardSpacerAsync();
                await flow.WizardProcessingAsync();
            });
        
        using var terminal = builder.Build();

        await terminal.RunAsync();
    }


    private static Task<Templates[]> GetTemplatesAsync()
    {
        return Task.FromResult(new[]
        {
            new Templates { TemplateName = "Console App" },
            new Templates { TemplateName = "Web API" },
            new Templates { TemplateName = "Worker Service" },
        });
    }
}

internal class HandrolledStateMachine
{
    public Templates[] AvailableTemplates { get; set; }
}

internal enum HandrolledState
{
    NotStarted,
    Starting   
}

internal class Templates
{
    public string TemplateName { get; set; }
}

internal static class FlowExtensions
{
    public static Task WizardTitleAsync(this Hex1bFlowContext flow)
    {
        return flow.ShowAsync(context => context.WizardTitle("New Aspire Project"));
    }

    public static Task WizardSpacerAsync(this Hex1bFlowContext flow)
    {
        return flow.ShowAsync(context => context.WizardSpacer());
    }

    public static async Task WizardProcessingAsync(this Hex1bFlowContext context)
    {
        var step = context.Step(stepContext => 
            stepContext.Grid(grid => [
                grid.Cell(c => c.Padding(1, 1, 0, 0, p => p.Spinner(SpinnerStyle.Circle))).Column(0),
                grid.Cell(c => c.Padding(0, 0, 0, 0, p => p.Text("Doing stuff!"))).Column(1),
            ]));

        await Task.Delay(2000);

        await step.CompleteAsync(stepContext => 
            stepContext.Grid(grid => [
                grid.Cell(c => c.Padding(1, 1, 0, 0, p => p.Text("\x25cf"))).Column(0),
                grid.Cell(c => c.Padding(0, 0, 0, 0, p => p.Text("Done stuff!"))).Column(1),
            ]));

    }

    public static Hex1bWidget WizardLayout(this RootContext context, int padding)
    {
        return context.Grid(grid => [
           grid.Cell(c => c.Padding(padding, padding, padding, padding, p => p.Text("|"))).Column(0),
           grid.Cell(c => c.Padding(padding, padding, padding, padding, p => p.Text("A Phase"))).Column(1), 
        ]);
    }

    public static Hex1bWidget WizardTitle(this RootContext context, string title)
    {
        return context.Grid(grid => [
           grid.Cell(c => c.Padding(1, 1, 1, 0, p => p.Text("\x25a0"))).Column(0),
           grid.Cell(c => c.Padding(0, 0, 1, 0, p => p.Text(title.ToUpper()))).Column(1), 
        ]);
    }

    public static Hex1bWidget WizardSpacer(this RootContext context)
    {
        return context.Grid(grid => [
           grid.Cell(c => c.Padding(1, 1, 0, 0, p => p.Text("|"))).Column(0),
           grid.Cell(c => c.Padding(0, 0, 0, 0, p => p.Text(""))).Column(1), 
        ]);
    }

    public static async Task<T> StatusAsync<T>(this Hex1bFlowContext context, string text, Func<Task<T>> callback)
    {
        var step = context.Step(stepContext => stepContext.HStack(hstack => [
            hstack.Spinner(SpinnerStyle.Dots),
            hstack.Text(text)
        ]));

        var result = await callback();

        await step.CompleteAsync(completedStep => completedStep.HStack(hstack => [
            hstack.Text("✓"),
            hstack.Text(text.Replace("Initializing", "Completed!"))
        ]));

        return result;
    }
}
