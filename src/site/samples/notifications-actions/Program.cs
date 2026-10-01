using Hex1b;

var state = new AppState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.ZStack(z => [
        z.VStack(v => [
            v.HStack(bar => [
                bar.Text("File Editor"),
                bar.Text("").FillWidth(),
                bar.NotificationIcon()
            ]),
            v.NotificationPanel(
                v.VStack(content => [
                    content.Text($"Status: {state.Status}"),
                    content.Text(""),
                    content.Button("Save File").OnClick(e => {
                        state.Status = "File saved!";
                        e.Context.Notifications.Post(
                            new Notification("File Saved", "document.txt saved successfully")
                                .Timeout(TimeSpan.FromSeconds(5))
                                .PrimaryAction("Undo", async ctx => {
                                    state.Status = "Save undone";
                                    ctx.Dismiss();
                                })
                                .SecondaryAction("Open Folder", async ctx => {
                                    state.Status = "Opening folder...";
                                })
                                .SecondaryAction("View File", async ctx => {
                                    state.Status = "Viewing file...";
                                })
                                .OnDismiss(async ctx => {
                                    // Cleanup when notification is dismissed
                                }));
                    })
                ])
            ).Fill()
        ])
    ]))
    .Build();

await terminal.RunAsync();

class AppState
{
    public string Status { get; set; } = "Ready";
}
