using Hex1b;
using Hex1b.Theming;

var theme = new Hex1bTheme("Custom")
    .Set(ButtonTheme.ForegroundColor, Hex1bColor.White)
    .Set(ButtonTheme.BackgroundColor, Hex1bColor.Blue)
    .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black)
    .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Yellow)
    .Set(ButtonTheme.HoveredForegroundColor, Hex1bColor.Black)
    .Set(ButtonTheme.HoveredBackgroundColor, Hex1bColor.FromRgb(150, 150, 200));

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => { options.Theme = theme; }, (Hex1bApp app) =>
    {

        return ctx => ctx.Button("Themed Button");
    })
    .Build();

await terminal.RunAsync();
