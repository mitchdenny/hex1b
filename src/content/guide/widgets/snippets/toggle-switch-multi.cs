using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("Speed Settings:"),
        v.ToggleSwitch(["Slow", "Normal", "Fast"], selectedIndex: 1)
    ])
));

await app.RunAsync();
