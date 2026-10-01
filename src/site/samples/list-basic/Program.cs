using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text("Select a fruit:"),
            v.Text(""),
            v.List(["Apple", "Banana", "Cherry", "Date", "Elderberry"])
        ])
    ]).Title("Fruit List"))
    .Build();

await terminal.RunAsync();
