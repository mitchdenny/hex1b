using Hex1b;

var state = new CounterState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text($"Count: {state.Count}"),
            v.Text(""),
            v.HStack(h => [
                h.Button("- Decrement").OnClick(_ => state.Count--),
                h.Text(" "),
                h.Button("+ Increment").OnClick(_ => state.Count++)
            ]),
            v.Text(""),
            v.Button("Reset").OnClick(_ => state.Count = 0)
        ])
    ]).Title("Counter"))
    .Build();

await terminal.RunAsync();

class CounterState
{
    public int Count { get; set; }
}
