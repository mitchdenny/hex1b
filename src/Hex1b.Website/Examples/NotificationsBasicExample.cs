using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Notifications Documentation: Basic Setup
/// Demonstrates basic notification panel with posting notifications.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/notifications.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class NotificationsBasicExample(ILogger<NotificationsBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<NotificationsBasicExample> _logger = logger;

    public override string Id => "notifications-basic";
    public override string Title => "Notifications - Basic Setup";
    public override string Description => "Demonstrates basic notification panel with posting notifications";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating notifications basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ZStack(z => [
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
            ]);
        };
    }
}
