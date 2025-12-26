using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("Application content"),
        v.InfoBar([
            new InfoBarSection("Mode: Insert"),
            new InfoBarSection(" | "),
            new InfoBarSection("ERROR", Hex1bColor.Red, Hex1bColor.Yellow),
            new InfoBarSection(" | "),
            new InfoBarSection("Ln 42, Col 7")
        ])
    ])
));

await app.RunAsync();
