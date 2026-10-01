using Hex1b;
using Hex1b.Flow;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bFlow(async flow =>
    {
        // Step 1: Ask for a name
        var name = "";
        var nameStep = flow.Step(ctx =>
            ctx.TextBox()
                .OnSubmit(e =>
                {
                    name = e.Text ?? "";
                    ctx.Step.Complete(y => y.Text($"  ✓ Name: {name}"));
                }),
            options: opts => opts.MaxHeight = 3
        );
        await nameStep.WaitForCompletionAsync();

        // Step 2: Pick a color
        var colors = new[] { "Red", "Green", "Blue" };
        var color = "";
        var colorStep = flow.Step(ctx =>
            ctx.VStack(v => [
                v.Text("Pick a color:"),
                v.List(colors).OnItemActivated(e =>
                {
                    color = colors[e.ActivatedIndex];
                    ctx.Step.Complete(y => y.Text($"  ✓ Color: {color}"));
                })
            ]),
            options: opts => opts.MaxHeight = 6
        );
        await colorStep.WaitForCompletionAsync();

        // After all steps complete, write a final summary
        var summaryStep = flow.Step(ctx =>
            ctx.Text($"Hello {name}, you picked {color}!"));
        await summaryStep.CompleteAsync();
    })
    .Build();

await terminal.RunAsync();
