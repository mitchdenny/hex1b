using Hex1b;

var state = new EventState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("Event Handlers Demo"),
        v.Text(""),
        v.Text($"Errors caught: {state.ErrorCount}"),
        v.Text($"Resets triggered: {state.ResetCount}"),
        v.Text(""),
        v.Rescue(inner => [
            inner.Text("Click the button to trigger an error."),
            inner.Text("Watch the counters above update!"),
            inner.Text(""),
            inner.Button("Trigger Error").OnClick(_ => {
                throw new Exception("Test error");
            })
        ])
        .OnRescue(e => {
            state.ErrorCount++;
            // In a real app: logger.LogError(e.Exception, "Error");
        })
        .OnReset(_ => {
            state.ResetCount++;
            // In a real app: ResetApplicationState();
        })
    ]))
    .Build();

await terminal.RunAsync();

class EventState
{
    public int ErrorCount { get; set; }
    public int ResetCount { get; set; }
}
