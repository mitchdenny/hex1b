using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("Click an icon:"),
        v.Text(""),
        v.HStack(h => [
            h.Icon("▶️").OnClick(_ => Console.WriteLine("Play!")),
            h.Text(" "),
            h.Icon("⏸️").OnClick(_ => Console.WriteLine("Pause!")),
            h.Text(" "),
            h.Icon("⏹️").OnClick(_ => Console.WriteLine("Stop!"))
        ])
    ]))
    .Build();

await terminal.RunAsync();
