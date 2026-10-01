using Hex1b;
using Hex1b.Theming;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx =>
    {
        // Danger zone theme mutator
        Func<Hex1bTheme, Hex1bTheme> dangerTheme = theme => theme.Clone()
            .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(255, 100, 100))
            .Set(BorderTheme.BorderColor, Hex1bColor.FromRgb(180, 0, 0))
            .Set(BorderTheme.TitleColor, Hex1bColor.Red)
            .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(100, 0, 0))
            .Set(ButtonTheme.ForegroundColor, Hex1bColor.White)
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red)
            .Set(ToggleSwitchTheme.FocusedSelectedBackgroundColor, Hex1bColor.Red)
            .Set(ToggleSwitchTheme.UnfocusedSelectedBackgroundColor, Hex1bColor.FromRgb(100, 0, 0));

        return ctx.VStack(v => [
            v.Border(b => [
                b.Text("  General Settings"),
                b.HStack(h => [
                    h.Text("Telemetry: "),
                    h.ToggleSwitch(["Off", "On"], 1)
                ])
            ]).Title("⚙ Settings"),

            v.Text(""),

            v.ThemePanel(dangerTheme, danger => [
                danger.Border(db => [
                    db.Text("  These actions cannot be undone!"),
                    db.HStack(h => [
                        h.Text("Factory reset: "),
                        h.ToggleSwitch(["No", "Yes"], 0)
                    ]),
                    db.Button("☠ Wipe Everything")
                ]).Title("⚠ DANGER ZONE")
            ])
        ]);
    })
    .Build();

await terminal.RunAsync();
