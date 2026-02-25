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
                await flow.WizardPhaseAsync();

                var statusStep1 = await flow.StatusAsync(" Initializing (1/3)...", async () =>
                {
                    await Task.Delay(5000);
                    return true;
                });

                var statusStep2 = await flow.StatusAsync(" Initializing (2/3)...", async () =>
                {
                    await Task.Delay(5000);
                    return true;
                });     

                var statusStep3 = await flow.StatusAsync(" Initializing (3/3)...", async () =>
                {
                    await Task.Delay(5000);
                    return true;
                });
            });
        
        using var terminal = builder.Build();

        await terminal.RunAsync();
    }
}

internal static class FlowExtensions
{
    public static async Task WizardPhaseAsync(this Hex1bFlowContext context)
    {
        var step = context.Step(stepContext => stepContext.WizardLayout(10));
        await Task.Delay(2000);
        await step.CompleteAsync(completedStep => completedStep.WizardLayout(10));
    }

    public static Hex1bWidget WizardLayout(this RootContext context, int padding)
    {
        return context.Grid(grid => [
           grid.Cell(c => c.Padding(padding, padding, padding, padding, p => p.Text("|"))).Column(0),
           grid.Cell(c => c.Padding(padding, padding, padding, padding, p => p.Text("A Phase"))).Column(1), 
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
