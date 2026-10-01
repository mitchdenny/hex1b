using Hex1b;

var state = new CounterState();

var app = new Hex1bApp(ctx =>
    ctx.Border(b => [
        b.Text($"Button pressed {state.Count} times"),
        b.Text(""),
        b.Button("Click me!").OnClick(_ => state.Count++)
    ]).Title("Counter Demo")
);

await app.RunAsync();

// Define a simple state class to hold our counter
class CounterState
{
    public int Count { get; set; }
}
