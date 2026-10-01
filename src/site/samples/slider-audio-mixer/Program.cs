using Hex1b;

var master = 80.0;
var music = 60.0;
var effects = 90.0;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text("Audio Settings"),
            v.Text(""),
            v.HStack(h => [
                h.Text($"Master:  {master,3:F0}% "),
                h.Slider(80).OnValueChanged(e => master = e.Value).Fill()
            ]),
            v.HStack(h => [
                h.Text($"Music:   {music,3:F0}% "),
                h.Slider(60).OnValueChanged(e => music = e.Value).Fill()
            ]),
            v.HStack(h => [
                h.Text($"Effects: {effects,3:F0}% "),
                h.Slider(90).OnValueChanged(e => effects = e.Value).Fill()
            ]),
            v.Text(""),
            v.Text("Tab to switch, arrows to adjust")
        ])
    ]).Title("Settings"))
    .Build();

await terminal.RunAsync();
