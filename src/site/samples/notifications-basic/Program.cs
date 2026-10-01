using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.ZStack(z => [
        z.VStack(v => [
            v.HStack(bar => [
                bar.Button("Menu"),
                bar.Text("").FillWidth(),
                bar.NotificationIcon()
            ]),
            v.NotificationPanel(
                v.VStack(content => [
                    content.Text("Notification Demo"),
                    content.Text(""),
                    content.Button("Show Notification").OnClick(e => {
                        e.Context.Notifications.Post(
                            new Notification("Hello!", "This is a notification")
                                .Timeout(TimeSpan.FromSeconds(5)));
                    }),
                    content.Text(""),
                    content.Text("Press Alt+N to toggle the notification drawer")
                ])
            ).Fill()
        ])
    ]))
    .Build();

await terminal.RunAsync();
