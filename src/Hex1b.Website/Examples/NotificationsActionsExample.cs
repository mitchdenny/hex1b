using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Notifications Documentation: Action Buttons
/// Demonstrates notifications with primary and secondary actions.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the actionsCode sample in:
/// src/content/guide/widgets/notifications.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class NotificationsActionsExample(ILogger<NotificationsActionsExample> logger) : Hex1bExample
{
    private readonly ILogger<NotificationsActionsExample> _logger = logger;

    public override string Id => "notifications-actions";
    public override string Title => "Notifications - Action Buttons";
    public override string Description => "Demonstrates notifications with primary and secondary actions";

    private class AppState
    {
        public string Status { get; set; } = "Ready";
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating notifications actions example widget builder");

        var state = new AppState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ZStack(z => [
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
            ]);
        };
    }
}
