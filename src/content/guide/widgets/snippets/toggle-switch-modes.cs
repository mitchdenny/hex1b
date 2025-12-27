using Hex1b;
using Hex1b.Widgets;

string mode = "Auto";

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("Mode Selection Example"),
        v.Text(""),
        v.HStack(h => [
            h.Text("Mode: "),
            h.ToggleSwitch(["Manual", "Auto", "Scheduled"], selectedIndex: 1)
                .OnSelectionChanged(args => mode = args.SelectedOption)
        ]),
        v.Text($"Selected: {mode}")
    ])
));

await app.RunAsync();
