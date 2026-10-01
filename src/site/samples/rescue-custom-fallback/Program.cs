using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Rescue(v => [
        v.Text("Custom Fallback Demo"),
        v.Text(""),
        v.Text("This uses Fallback() to provide"),
        v.Text("a custom error UI instead of the default."),
        v.Text(""),
        v.Button("Trigger Error").OnClick(_ => {
            throw new InvalidOperationException("Something went wrong!");
        })
    ])
    .Fallback(rescue => rescue.Border(b => [
        b.VStack(inner => [
            inner.Text("🔥 Custom Error Handler 🔥"),
            inner.Text(""),
            inner.Text($"Error Type: {rescue.Exception.GetType().Name}"),
            inner.Text($"Phase: {rescue.ErrorPhase}"),
            inner.Text(""),
            inner.Text("Message:"),
            inner.Text($"  {rescue.Exception.Message}"),
            inner.Text(""),
            inner.Button("🔄 Try Again").OnClick(_ => rescue.Reset()),
        ])
    ]).Title("Oops!")))
    .Build();

await terminal.RunAsync();
