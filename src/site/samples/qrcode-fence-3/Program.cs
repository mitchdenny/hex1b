using Hex1b;
using Hex1b.Theming;

var theme = new Hex1bTheme("Custom")
    .Set(GlobalTheme.ForegroundColor, Hex1bColor.Green)
    .Set(GlobalTheme.BackgroundColor, Hex1bColor.Black);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => { options.Theme = theme; }, (Hex1bApp app) =>
    {

        return ctx => ctx.VStack(tp => [
            tp.Text("Green QR Code:"),
            tp.QrCode("https://hex1b.dev")
        ]);
    })
    .Build();

await terminal.RunAsync();
