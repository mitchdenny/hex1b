using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Border(b => [
            b.Text("Background operation in progress...")
        ]).Title("Activity Indicator").FillHeight(),
        v.InfoBar(s => [
            s.Section(x => x.HStack(h => [
                h.Spinner(SpinnerStyle.Dots),
                h.Text(" Saving...")
            ])),
            s.Spacer(),
            s.Section("Ready")
        ])
    ]))
    .Build();

await terminal.RunAsync();
