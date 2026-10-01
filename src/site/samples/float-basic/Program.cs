using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Float(v.Text("📍 Marker at (2, 1)")).Absolute(2, 1),
        v.Float(v.Text("📍 Marker at (30, 5)")).Absolute(30, 5),
        v.Float(v.Text("📍 Marker at (10, 9)")).Absolute(10, 9),
        v.Float(v.Border(b => [
            b.Text("Boxed content")
        ]).Title("Info")).Absolute(45, 3),
    ]))
    .Build();

await terminal.RunAsync();
