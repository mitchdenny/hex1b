using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("Download Progress"),
        v.Progress(75),
        v.Text(""),
        v.Text("Upload Progress"),
        v.Progress(30)
    ]))
    .Build();

await terminal.RunAsync();
