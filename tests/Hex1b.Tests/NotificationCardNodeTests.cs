using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for NotificationCardNode rendering and behavior.
/// </summary>
public class NotificationCardNodeTests
{
    [Fact]
    public void NotificationCardNode_Measure_ReturnsReasonableSize()
    {
        var notification = new Notification("Test Title", "Test body text");
        var node = new NotificationCardNode
        {
            Notification = notification,
            Title = notification.Title,
            Body = notification.Body
        };

        var size = node.Measure(Constraints.Unbounded);

        // Should have non-zero dimensions
        Assert.True(size.Width > 0, "Width should be positive");
        Assert.True(size.Height > 0, "Height should be positive");
    }

    [Fact]
    public async Task NotificationCardNode_WithAction_CreatesChildrenViaReconciliation()
    {
        var notification = new Notification("Title", "Body")
            .PrimaryAction("View Details", _ => Task.CompletedTask);
        
        var stack = new NotificationStack();
        var cardWidget = new NotificationCardWidget(notification, stack);
        
        // Reconcile through the widget to create children
        var context = ReconcileContext.CreateRoot();
        var node = (NotificationCardNode)await cardWidget.ReconcileAsync(null, context);

        // Verify children were created
        Assert.NotNull(node.DismissButton);
        Assert.NotNull(node.ActionButton);
        Assert.Equal("View Details", node.ActionButton.PrimaryLabel);
    }

    [Fact]
    public async Task NotificationCardNode_WithAction_MeasuresWithChildren()
    {
        var notification = new Notification("Title", "Body")
            .PrimaryAction("View", _ => Task.CompletedTask);
        
        var stack = new NotificationStack();
        var cardWidget = new NotificationCardWidget(notification, stack);
        
        // Reconcile through the widget
        var context = ReconcileContext.CreateRoot();
        var node = (NotificationCardNode)await cardWidget.ReconcileAsync(null, context);

        var size = node.Measure(Constraints.Unbounded);

        // With title + body + action + progress, should have reasonable height
        Assert.True(size.Height >= 3, $"Height should be at least 3, was {size.Height}");
    }

    [Fact]
    public async Task NotificationCardNode_Children_ContainsButtonsAfterReconciliation()
    {
        var notification = new Notification("Title", "Body")
            .PrimaryAction("View", _ => Task.CompletedTask);
        
        var stack = new NotificationStack();
        var cardWidget = new NotificationCardWidget(notification, stack);
        
        // Reconcile through the widget
        var context = ReconcileContext.CreateRoot();
        var node = (NotificationCardNode)await cardWidget.ReconcileAsync(null, context);

        // GetChildren should return the button nodes
        var children = node.GetChildren().ToList();
        Assert.NotEmpty(children);
        Assert.Contains(children, c => c is ButtonNode);
        Assert.Contains(children, c => c is SplitButtonNode);
    }

    [Fact]
    public async Task NotificationCardNode_DismissButton_AlwaysCreated()
    {
        // Even without primary action, dismiss button should exist
        var notification = new Notification("Title", "Body");
        
        var stack = new NotificationStack();
        var cardWidget = new NotificationCardWidget(notification, stack);
        
        var context = ReconcileContext.CreateRoot();
        var node = (NotificationCardNode)await cardWidget.ReconcileAsync(null, context);

        Assert.NotNull(node.DismissButton);
        Assert.Null(node.ActionButton); // No action button without primary action
    }

    [Fact]
    public async Task NotificationCard_RendersActionButtonLabel()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 20)
            .Build();

        using var app = new Hex1bApp(
            ctx =>
            {
                return ctx.NotificationPanel(
                    ctx.VStack(v => [
                        v.Button("Post Notification").OnClick(e =>
                        {
                            e.Context.Notifications.Post(
                                new Notification("Test Alert", "Something happened")
                                    .WithTimeout(TimeSpan.FromSeconds(30))
                                    .PrimaryAction("View Details", async c => c.Dismiss()));
                        })
                    ])
                );
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Post Notification"), TimeSpan.FromSeconds(2), "button")
            // Press enter to click the button
            .Key(Hex1bKey.Enter)
            .Wait(TimeSpan.FromMilliseconds(100))
            .WaitUntil(s => s.ContainsText("Test Alert") && s.ContainsText("View Details"), TimeSpan.FromSeconds(2), "notification with action")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Verify notification content and action button are visible
        Assert.True(snapshot.ContainsText("Test Alert"), "Should show notification title");
        Assert.True(snapshot.ContainsText("View Details"), "Should show action button label");
    }

    [Fact]
    public async Task NotificationCard_SplitButtonDropdown_OpensSuccessfully()
    {
        // This test verifies that the dropdown on a SplitButton inside a NotificationCard
        // can open successfully (requires proper parent chain to find popup host)
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 20)
            .Build();

        Exception? caughtException = null;
        var notificationPosted = false;

        using var app = new Hex1bApp(
            ctx =>
            {
                return ctx.NotificationPanel(
                    ctx.VStack(v => [
                        v.Button("Post").OnClick(e =>
                        {
                            notificationPosted = true;
                            e.Context.Notifications.Post(
                                new Notification("Test", "Body")
                                    .WithTimeout(TimeSpan.FromSeconds(30))
                                    .PrimaryAction("Action", async c => c.Dismiss())
                                    .SecondaryAction("Option A", async c => c.Dismiss())
                                    .SecondaryAction("Option B", async c => c.Dismiss()));
                        })
                    ])
                );
            },
            new Hex1bAppOptions 
            { 
                WorkloadAdapter = workload,
                OnRescue = args => caughtException = args.Exception
            }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Post"), TimeSpan.FromSeconds(2), "post button")
            // Click Post button to create notification
            .Key(Hex1bKey.Enter)
            .Wait(TimeSpan.FromMilliseconds(200))
            .WaitUntil(s => s.ContainsText("Action") && s.ContainsText("▼"), TimeSpan.FromSeconds(2), "notification with dropdown")
            // Tab to the action button in the notification
            .Key(Hex1bKey.Tab)
            .Wait(TimeSpan.FromMilliseconds(100))
            // Press Down arrow to open dropdown
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("after_down")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Verify notification was actually posted
        Assert.True(notificationPosted, "Notification should have been posted");
        
        // Verify no "popup host" exception was thrown - the parent chain is correctly set up
        if (caughtException != null)
        {
            Assert.DoesNotContain("popup host", caughtException.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task NotificationPanel_AltN_TogglesDrawer()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 20)
            .Build();

        var notificationPosted = false;

        using var app = new Hex1bApp(
            ctx =>
            {
                return ctx.NotificationPanel(
                    ctx.VStack(v => [
                        v.Button("Post").OnClick(e =>
                        {
                            notificationPosted = true;
                            e.Context.Notifications.Post(
                                new Notification("Test Notification", "This is a test")
                                    .WithTimeout(TimeSpan.FromSeconds(30)));
                        })
                    ])
                );
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Post"), TimeSpan.FromSeconds(2), "post button")
            // Post a notification
            .Key(Hex1bKey.Enter)
            .Wait(TimeSpan.FromMilliseconds(200))
            .WaitUntil(s => s.ContainsText("Test Notification"), TimeSpan.FromSeconds(2), "notification")
            // Press Alt+N to open drawer
            .Alt().Key(Hex1bKey.N)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("drawer_open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(notificationPosted, "Notification should have been posted");
        // Drawer should show "Notifications (1)" header
        Assert.True(snapshot.ContainsText("Notifications (1)"), "Drawer should show notification count");
    }
}
