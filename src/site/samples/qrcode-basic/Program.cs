using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("QR Code Example"),
        v.Text(""),
        v.Text("Scan with your phone:"),
        v.QrCode("https://hex1b.dev"),
        v.Text(""),
        v.Text("The QR code encodes: https://hex1b.dev")
    ]))
    .Build();

await terminal.RunAsync();
