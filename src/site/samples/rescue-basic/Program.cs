using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Rescue(v => [
        v.Text("Application content here"),
        v.Text(""),
        v.Text("Click the button to trigger an error."),
        v.Text("The Rescue widget will catch it and show"),
        v.Text("a fallback UI with error details."),
        v.Text(""),
        v.Button("Click me").OnClick(_ => {
            // If this throws, Rescue catches it
            throw new InvalidOperationException("Oops! Something went wrong.");
        })
    ]))
    .Build();

await terminal.RunAsync();
