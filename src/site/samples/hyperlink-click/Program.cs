using Hex1b;

var clickCount = 0;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text($"Link clicked {clickCount} times"),
        v.Text(""),
        v.Hyperlink("Click me!", "https://example.com")
            .OnClick(e => {
                clickCount++;
                Console.WriteLine($"Navigating to: {e.Uri}");
            })
    ]))
    .Build();

await terminal.RunAsync();
