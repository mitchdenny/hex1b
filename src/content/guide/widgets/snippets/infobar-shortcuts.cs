using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("Use the keyboard shortcuts below"),
        v.InfoBar([
            "F1", "Help",
            "Ctrl+S", "Save",
            "Ctrl+Q", "Quit"
        ])
    ])
));

await app.RunAsync();
