using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("Unfocused:"),
        v.ToggleSwitch(["Off", "On"], selectedIndex: 1),
        v.Text(""),
        v.Text("Focused:"),
        v.ToggleSwitch(["Light", "Dark"], selectedIndex: 1)
    ])
));

await app.RunAsync();
