using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text("Select a fruit:"),
            v.Text(""),
            v.HStack(h => [
                h.Text("Fruit: "),
                h.Picker(["Apple", "Banana", "Cherry", "Date", "Elderberry"])
            ])
        ])
    ]).Title("Fruit Picker"))
    .Build();

await terminal.RunAsync();
