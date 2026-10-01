using Hex1b;
using Hex1b.Flow;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bFlow(async flow =>
    {
        var status = "Initializing...";
        var done = false;

        // Start the step — UI renders immediately
        var step = flow.Step(ctx => ctx.HStack(h => [
            h.Spinner(),
            h.Text($" {status}")
        ]),
            options: opts => opts.MaxHeight = 2
        );

        // Do background work in the flow callback itself
        var steps = new[]
        {
            "Installing packages...",
            "Compiling project...",
            "Running tests..."
        };

        foreach (var s in steps)
        {
            status = s;
            step.Invalidate();
            await Task.Delay(1000);
        }

        done = true;
        step.Invalidate();
        await Task.Delay(300);
        await step.CompleteAsync(y => y.Text("  ✓ All tasks complete!"));
    })
    .Build();

await terminal.RunAsync();
