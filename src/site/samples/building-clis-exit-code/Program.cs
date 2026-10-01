using Hex1b;
using Hex1b.Flow;

var state = new CommandState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bFlow(async flow =>
    {
        var step = flow.Step(ctx =>
            ctx.VStack(v => [
                v.Text("Delete all files in /tmp?"),
                v.HStack(h => [
                    h.Button("Yes, delete").OnClick(_ =>
                    {
                        state.ExitCode = 0;
                        ctx.Step.Complete(y => y.Text("  ✓ Deleted."));
                    }),
                    h.Button("Cancel").OnClick(_ =>
                    {
                        state.ExitCode = 1;
                        ctx.Step.Complete(y => y.Text("  ✗ Cancelled."));
                    })
                ])
            ]),
            options: opts => opts.MaxHeight = 4
        );
        await step.WaitForCompletionAsync();
    })
    .Build();

await terminal.RunAsync();
return state.ExitCode;

class CommandState
{
    public int ExitCode { get; set; } = 1;
}
