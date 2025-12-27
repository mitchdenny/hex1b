using Hex1b;
using Hex1b.Widgets;

string theme = "Light";
string sound = "On";

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("Settings Panel"),
        v.Text(""),
        v.HStack(h => [
            h.Text("Theme:  ").FixedWidth(12),
            h.ToggleSwitch(["Light", "Dark"])
                .OnSelectionChanged(args => theme = args.SelectedOption)
        ]),
        v.HStack(h => [
            h.Text("Sound:  ").FixedWidth(12),
            h.ToggleSwitch(["Off", "On"], selectedIndex: 1)
                .OnSelectionChanged(args => sound = args.SelectedOption)
        ])
    ])
));

await app.RunAsync();
