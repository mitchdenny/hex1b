using Hex1b;

var state = new DownloadState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.ZStack(z => [
        z.VStack(v => [
            v.HStack(bar => [
                bar.Text("Downloads"),
                bar.Text("").FillWidth(),
                bar.NotificationIcon()
            ]),
            v.NotificationPanel(
                v.VStack(content => [
                    content.Text($"Downloads: {state.DownloadCount}"),
                    content.Text($"Last event: {state.LastEvent}"),
                    content.Text(""),
                    content.Button("Start Download").OnClick(e => {
                        state.DownloadCount++;
                        e.Context.Notifications.Post(
                            new Notification("Downloading...", $"File {state.DownloadCount}.zip")
                                .Timeout(TimeSpan.FromSeconds(8))
                                .OnTimeout(async ctx => {
                                    state.LastEvent = "Download notification timed out";
                                })
                                .OnDismiss(async ctx => {
                                    state.LastEvent = "Download notification dismissed";
                                })
                                .PrimaryAction("Cancel", async ctx => {
                                    state.LastEvent = "Download cancelled";
                                    ctx.Dismiss();
                                }));
                    })
                ])
            ).Fill()
        ])
    ]))
    .Build();

await terminal.RunAsync();

class DownloadState
{
    public int DownloadCount { get; set; }
    public string LastEvent { get; set; } = "(none)";
}
