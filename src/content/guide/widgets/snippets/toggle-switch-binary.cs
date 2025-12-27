using Hex1b;
using Hex1b.Widgets;

string power = "Off";

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("Binary Toggle Example"),
        v.Text(""),
        v.HStack(h => [
            h.Text("Power: "),
            h.ToggleSwitch(["Off", "On"])
                .OnSelectionChanged(args => power = args.SelectedOption)
        ]),
        v.Text($"Current: {power}")
    ])
));

await app.RunAsync();
