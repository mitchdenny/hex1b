using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Border(b => [
            b.Text("Main content area")
        ], title: "App").Fill(),
        v.InfoBar("Ready")
    ])
));

await app.RunAsync();
