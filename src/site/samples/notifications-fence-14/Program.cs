using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.ZStack(z => [
        z.VStack(v => [
            // Header with notification icon
            v.HStack(bar => [
                bar.Button("File"),
                bar.Button("Edit"),
                bar.Text("").FillWidth(),
                bar.NotificationIcon()
            ]),
            
            // Main content wrapped in notification panel
            v.NotificationPanel(
                v.VStack(content => [
                    content.Button("Save").OnClick(e => {
                        e.Context.Notifications.Post(
                            new Notification("Saved", "Document saved")
                                .Timeout(TimeSpan.FromSeconds(3)));
                    }),
                    
                    content.Button("Delete").OnClick(e => {
                        e.Context.Notifications.Post(
                            new Notification("Deleted", "Item moved to trash")
                                .Timeout(TimeSpan.FromSeconds(10))
                                .PrimaryAction("Undo", async ctx => {
                                    // Restore the item
                                    ctx.Dismiss();
                                })
                                .OnDismiss(async ctx => {
                                    // Permanently delete after dismiss
                                }));
                    }),
                    
                    content.Button("Error").OnClick(e => {
                        // No timeout - requires user action
                        e.Context.Notifications.Post(
                            new Notification("Error", "Operation failed")
                                .PrimaryAction("Retry", async ctx => {
                                    // Retry the operation
                                    ctx.Dismiss();
                                })
                                .SecondaryAction("View Details", async ctx => {
                                    // Show error details
                                }));
                    })
                ])
            ).Fill()
        ])
    ]))
    .Build();

await terminal.RunAsync();
