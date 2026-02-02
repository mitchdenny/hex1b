using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Notifications Documentation: Lifecycle Events
/// Demonstrates notification timeout and dismiss handlers.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the lifecycleCode sample in:
/// src/content/guide/widgets/notifications.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class NotificationsLifecycleExample(ILogger<NotificationsLifecycleExample> logger) : Hex1bExample
{
    private readonly ILogger<NotificationsLifecycleExample> _logger = logger;

    public override string Id => "notifications-lifecycle";
    public override string Title => "Notifications - Lifecycle Events";
    public override string Description => "Demonstrates notification timeout and dismiss handlers";

    private class DownloadState
    {
        public int DownloadCount { get; set; }
        public string LastEvent { get; set; } = "(none)";
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating notifications lifecycle example widget builder");

        var state = new DownloadState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ZStack(z => [
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
            ]);
        };
    }
}
