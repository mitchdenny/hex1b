using Hex1b;
using Hex1b.Flow;

var state = new SetupState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bFlow(async flow =>
    {
        // Step 1: Project name
        var nameStep = flow.Step(ctx =>
            ctx.VStack(v => [
                v.Text("Enter your project name:"),
                v.TextBox(state.ProjectName)
                    .OnSubmit(e =>
                    {
                        state.ProjectName = e.Text ?? "";
                        ctx.Step.Complete(y =>
                            y.Text($"  ✓ Project: {state.ProjectName}"));
                    })
                    .FillWidth()
            ]),
            options: opts => opts.MaxHeight = 4
        );
        await nameStep.WaitForCompletionAsync();

        // Step 2: Framework
        var fwStep = flow.Step(ctx =>
            ctx.VStack(v => [
                v.Text("Select a framework:"),
                v.List(SetupState.Frameworks)
                    .OnItemActivated(e =>
                    {
                        state.Framework = SetupState.Frameworks[e.ActivatedIndex];
                        ctx.Step.Complete(y =>
                            y.Text($"  ✓ Framework: {state.Framework}"));
                    })
                    .FixedHeight(SetupState.Frameworks.Length + 1)
            ]),
            options: opts => opts.MaxHeight = 8
        );
        await fwStep.WaitForCompletionAsync();

        // Step 3: Confirm
        var confirmStep = flow.Step(ctx =>
            ctx.VStack(v => [
                v.Text($"Create '{state.ProjectName}' with {state.Framework}?"),
                v.HStack(h => [
                    h.Button("Yes").OnClick(_ =>
                    {
                        state.Confirmed = true;
                        ctx.Step.Complete(y =>
                            y.Text($"  ✓ Created {state.ProjectName}!"));
                    }),
                    h.Button("No").OnClick(_ =>
                    {
                        ctx.Step.Complete(y => y.Text("  ✗ Cancelled."));
                    })
                ])
            ]),
            options: opts => opts.MaxHeight = 4
        );
        await confirmStep.WaitForCompletionAsync();
    })
    .Build();

await terminal.RunAsync();

// Return an exit code based on state
return state.Confirmed ? 0 : 1;

class SetupState
{
    public static readonly string[] Frameworks =
        ["ASP.NET Core", "Blazor", "Console App", "Worker Service"];

    public string ProjectName { get; set; } = "";
    public string Framework { get; set; } = "";
    public bool Confirmed { get; set; }
}
