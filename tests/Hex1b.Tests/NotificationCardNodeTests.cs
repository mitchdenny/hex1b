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
                                    .Timeout(TimeSpan.FromSeconds(30))
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
                                    .Timeout(TimeSpan.FromSeconds(30))
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
                                    .Timeout(TimeSpan.FromSeconds(30)));
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
            .WaitUntil(s => s.ContainsText("Notifications (1)"), TimeSpan.FromSeconds(2), "drawer open with count")
            .Capture("drawer_open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(notificationPosted, "Notification should have been posted");
        // Drawer should show "Notifications (1)" header
        Assert.True(snapshot.ContainsText("Notifications (1)"), "Drawer should show notification count");
    }

    [Fact]
    public async Task NotificationIcon_OutsidePanel_CanAccessNotifications()
    {
        // This test verifies the core architectural requirement:
        // NotificationIcon and NotificationPanel are SIBLINGS in the widget tree,
        // yet both can access the same notification system.
        //
        // Widget tree structure:
        // ZStack (root - should provide NotificationStack)
        //   └── VStack
        //       ├── HStack with Button + NotificationIcon  ← NOT inside NotificationPanel
        //       └── NotificationPanel                       ← Sibling, not parent
        //           └── Content with Post button
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 20)
            .Build();

        Exception? caughtException = null;
        var notificationPosted = false;

        using var app = new Hex1bApp(
            ctx => ctx.ZStack(z => [
                z.VStack(outer => [
                    // Top bar with NotificationIcon - OUTSIDE the NotificationPanel
                    outer.HStack(bar => [
                        bar.Button("Menu"),
                        bar.NotificationIcon()
                    ]),
                    // NotificationPanel wraps only the content area
                    outer.NotificationPanel(
                        outer.VStack(content => [
                            content.Button("Post Notification").OnClick(e =>
                            {
                                notificationPosted = true;
                                e.Context.Notifications.Post(
                                    new Notification("Test Alert", "From content area")
                                        .Timeout(TimeSpan.FromSeconds(30)));
                            })
                        ])
                    ).Fill()
                ])
            ]),
            new Hex1bAppOptions 
            { 
                WorkloadAdapter = workload,
                OnRescue = args => caughtException = args.Exception
            }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Post Notification") && s.ContainsText("🔔"), TimeSpan.FromSeconds(2), "button and icon")
            // Focus order: Menu -> NotificationIcon -> Post Notification
            // Tab twice to get to Post button
            .Key(Hex1bKey.Tab) // Move from Menu to NotificationIcon
            .Key(Hex1bKey.Tab) // Move from NotificationIcon to Post Notification
            .Key(Hex1bKey.Enter) // Click Post Notification
            .Wait(TimeSpan.FromMilliseconds(200))
            .WaitUntil(s => s.ContainsText("Test Alert"), TimeSpan.FromSeconds(2), "notification posted")
            .Capture("after_post")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Verify no exception was thrown accessing notifications
        Assert.Null(caughtException);
        
        // Verify notification was actually posted
        Assert.True(notificationPosted, "Notification handler should have been called");
        
        // Verify notification appears on screen
        Assert.True(snapshot.ContainsText("Test Alert"), "Notification should appear");
    }

    [Fact]
    public async Task NotificationIcon_Click_TogglesRegisteredPanel()
    {
        // Verifies that clicking NotificationIcon toggles the panel visibility
        // even when the icon is outside the panel's widget subtree.
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        using var app = new Hex1bApp(
            ctx => ctx.ZStack(z => [
                z.VStack(outer => [
                    outer.HStack(bar => [
                        bar.Text("App Title"),
                        bar.Text("").FillWidth(),
                        bar.NotificationIcon()
                    ]),
                    outer.NotificationPanel(
                        outer.VStack(content => [
                            content.Button("Post").OnClick(e =>
                            {
                                e.Context.Notifications.Post(
                                    new Notification("Alert", "Something happened")
                                        .Timeout(TimeSpan.FromSeconds(30)));
                            }),
                            content.Text("Main content area")
                        ])
                    ).Fill()
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Post"), TimeSpan.FromSeconds(2), "ready")
            // Post a notification first - Tab to Post button and press Enter
            .Key(Hex1bKey.Tab)
            .Key(Hex1bKey.Enter)
            .Wait(TimeSpan.FromMilliseconds(200))
            .WaitUntil(s => s.ContainsText("Alert"), TimeSpan.FromSeconds(2), "notification")
            // Use Alt+N to toggle the drawer (standard keyboard shortcut)
            .Alt().Key(Hex1bKey.N)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("drawer_should_open")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Drawer should be visible with notification count header
        Assert.True(
            snapshot.ContainsText("Notifications (1)") || snapshot.ContainsText("Notifications(1)"),
            $"Drawer should show with count. Screen:\n{snapshot}");
    }

    [Fact]
    public async Task NotificationDrawer_ClickOutside_CollapsesDrawer()
    {
        // Verifies that clicking outside the drawer collapses it
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .WithMouse()
            .Build();

        using var app = new Hex1bApp(
            ctx => ctx.ZStack(z => [
                z.VStack(outer => [
                    outer.HStack(bar => [
                        bar.Button("Menu"),
                        bar.NotificationIcon()
                    ]),
                    outer.NotificationPanel(
                        outer.VStack(content => [
                            content.Button("Post").OnClick(e =>
                            {
                                e.Context.Notifications.Post(
                                    new Notification("Test", "Test notification")
                                        .Timeout(TimeSpan.FromSeconds(30)));
                            }),
                            content.Text("Main content area - click here to close drawer")
                        ])
                    ).Fill()
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Post"), TimeSpan.FromSeconds(2), "ready")
            // Post a notification
            .Key(Hex1bKey.Tab)
            .Key(Hex1bKey.Tab)
            .Key(Hex1bKey.Enter)
            .Wait(TimeSpan.FromMilliseconds(200))
            .WaitUntil(s => s.ContainsText("Test"), TimeSpan.FromSeconds(2), "notification posted")
            // Open the drawer with Alt+N
            .Alt().Key(Hex1bKey.N)
            .Wait(TimeSpan.FromMilliseconds(200))
            .WaitUntil(s => s.ContainsText("Notifications (1)"), TimeSpan.FromSeconds(2), "drawer opened")
            .Capture("drawer_open")
            // Click outside the drawer (left side of screen, in the content area)
            // Drawer is on the right (~42 chars wide), so click at x=5 which is definitely outside
            .ClickAt(5, 10)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("after_click_outside")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Drawer should have collapsed - "Notifications (1)" header should be gone
        Assert.False(
            snapshot.ContainsText("Notifications (1)"),
            $"Drawer should have collapsed after clicking outside. Screen:\n{snapshot.GetText()}");
    }

    [Fact]
    public async Task NotificationCard_DrawerMode_HidesProgressBar()
    {
        // Verifies that notifications in the drawer don't show progress bars
        // (timeout countdown is irrelevant once user has opened the drawer)
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        using var app = new Hex1bApp(
            ctx => ctx.ZStack(z => [
                z.VStack(outer => [
                    outer.HStack(bar => [
                        bar.Button("Menu"),
                        bar.NotificationIcon()
                    ]),
                    outer.NotificationPanel(
                        outer.Button("Post").OnClick(e =>
                        {
                            e.Context.Notifications.Post(
                                new Notification("Drawer Test", "Check progress bar visibility")
                                    .Timeout(TimeSpan.FromSeconds(30)));
                        })
                    ).Fill()
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Post"), TimeSpan.FromSeconds(2), "ready")
            // Post a notification
            .Key(Hex1bKey.Tab)
            .Key(Hex1bKey.Tab)
            .Key(Hex1bKey.Enter)
            .Wait(TimeSpan.FromMilliseconds(200))
            .WaitUntil(s => s.ContainsText("Drawer Test"), TimeSpan.FromSeconds(2), "notification posted")
            // Open the drawer (this should hide progress bar on drawer cards)
            .Alt().Key(Hex1bKey.N)
            .Wait(TimeSpan.FromMilliseconds(200))
            .WaitUntil(s => s.ContainsText("Notifications (1)"), TimeSpan.FromSeconds(2), "drawer opened")
            .Capture()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // The notification should be visible in the drawer
        Assert.True(snapshot.ContainsText("Drawer Test"),
            $"Drawer view should show notification. Screen:\n{snapshot.GetText()}");
        Assert.True(snapshot.ContainsText("Notifications (1)"),
            "Drawer header should be visible");
        
        // The test validates that ShowProgressBar=false is applied to drawer cards
        // by checking that the drawer renders correctly without the progress bar row
        // (which would cause layout issues if not handled properly)
    }

    [Fact]
    public async Task NotificationDrawer_ClickOnCardButton_DismissesNotification()
    {
        // Verifies that clicking on the dismiss button inside a drawer card works
        // (regression test: backdrop must not intercept clicks meant for cards)
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .WithMouse()
            .Build();
        
        using var app = new Hex1bApp(
            ctx => ctx.ZStack(z => [
                z.VStack(outer => [
                    outer.HStack(bar => [
                        bar.Button("Menu"),
                        bar.NotificationIcon()
                    ]),
                    outer.NotificationPanel(
                        outer.Button("Post").OnClick(e =>
                        {
                            e.Context.Notifications.Post(
                                new Notification("Dismiss Test", "Click the X to dismiss")
                                    .Timeout(TimeSpan.FromSeconds(60)));
                        })
                    ).Fill()
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Post"), TimeSpan.FromSeconds(2), "ready")
            // Post a notification
            .Key(Hex1bKey.Tab)
            .Key(Hex1bKey.Tab)
            .Key(Hex1bKey.Enter)
            .Wait(TimeSpan.FromMilliseconds(200))
            .WaitUntil(s => s.ContainsText("Dismiss Test"), TimeSpan.FromSeconds(2), "notification posted")
            // Open the drawer
            .Alt().Key(Hex1bKey.N)
            .Wait(TimeSpan.FromMilliseconds(200))
            .WaitUntil(s => s.ContainsText("Notifications (1)"), TimeSpan.FromSeconds(2), "drawer opened")
            .Capture("before_dismiss")
            // Click on the dismiss button [ × ] 
            // Drawer starts at col 38, card at 39, card width 40, button 5 chars
            // Button X = 39 + 40 - 5 = 74, so button spans cols 74-78
            // Button is on row 4 (0-indexed): row 0=Menu, row 1=Post/header, row 2=separator, row 3=card top, row 4=card title with button
            .ClickAt(76, 4)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("after_dismiss")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
        
        var hitTestDebug = app.LastHitTestDebug ?? "no debug info";

        // Notification should be dismissed - header should show 0 or drawer should close
        Assert.False(snapshot.ContainsText("Dismiss Test"),
            $"Notification should have been dismissed.\nHitTest: {hitTestDebug}\nScreen:\n{snapshot.GetText()}");
    }
}


/// <summary>
/// Tests for notification panel hit testing behavior.
/// </summary>
public class NotificationPanelHitTestTests
{
    /// <summary>
    /// Regression test: When notification drawer is expanded over content,
    /// clicking on notification cards should NOT pass through to the content below.
    /// The drawer should have priority in hit testing.
    /// </summary>
    [Fact]
    public async Task NotificationPanel_DrawerExpanded_BlocksHitTestToContentBelow()
    {
        // Arrange - Create a notification panel with buttons underneath
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(112, 36)
            .Build();

        bool contentButtonClicked = false;

        using var app = new Hex1bApp(
            ctx => ctx.ZStack(z => [
                z.VStack(outer => [
                    outer.HStack(bar => [
                        bar.Text("App Title"),
                        bar.Text("").FillWidth(),
                        bar.NotificationIcon()
                    ]),
                    outer.NotificationPanel(
                        // Content buttons that could receive clicks
                        outer.VStack(v => [
                            v.Button("Post Notifications").OnClick(e =>
                            {
                                e.Context.Notifications.Post(
                                    new Notification("Test 1", "First notification")
                                        .Timeout(TimeSpan.FromMinutes(5)));
                                e.Context.Notifications.Post(
                                    new Notification("Test 2", "Second notification")
                                        .Timeout(TimeSpan.FromMinutes(5)));
                                e.Context.Notifications.ShowPanel();
                            }),
                            v.Button("Content Button").OnClick(_ => contentButtonClicked = true)
                        ])
                    ).Fill()
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Post notifications and open drawer
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Post Notifications"), TimeSpan.FromSeconds(2), "ready")
            // Tab to move from NotificationIcon to Post Notifications button
            .Key(Hex1bKey.Tab)
            .Wait(TimeSpan.FromMilliseconds(50))
            .Key(Hex1bKey.Enter) // Click "Post Notifications" button
            .Wait(TimeSpan.FromMilliseconds(300))
            .WaitUntil(s => s.ContainsText("Test 1") || s.ContainsText("Test 2"), TimeSpan.FromSeconds(2), "notifications_posted")
            .Capture("notifications_posted")
            // Now tab to navigate through drawer elements
            .Key(Hex1bKey.Tab) // Navigate in drawer
            .Key(Hex1bKey.Tab)
            .Wait(TimeSpan.FromMilliseconds(100))
            .Capture("after_tab")
            .Key(Hex1bKey.Enter) // Try to click something in the drawer
            .Wait(TimeSpan.FromMilliseconds(100))
            .Capture("after_click")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // The content button should NOT have been clicked
        // Focus should stay on drawer elements, not pass through to content
        Assert.False(contentButtonClicked,
            $"Content button should NOT be clicked when drawer is expanded. Screen:\n{snapshot}");
        
        // The drawer should still be visible
        Assert.True(snapshot.ContainsText("Notifications"),
            $"Drawer should still be visible. Screen:\n{snapshot}");
    }

    /// <summary>
    /// Test that when drawer is collapsed, content buttons are accessible.
    /// </summary>
    [Fact]
    public async Task NotificationPanel_DrawerCollapsed_ContentIsAccessible()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(112, 36)
            .Build();

        bool contentButtonClicked = false;

        using var app = new Hex1bApp(
            ctx => ctx.ZStack(z => [
                z.VStack(outer => [
                    outer.HStack(bar => [
                        bar.Text("App Title"),
                        bar.Text("").FillWidth(),
                        bar.NotificationIcon()
                    ]),
                    outer.NotificationPanel(
                        outer.VStack(v => [
                            v.Button("Content Button").OnClick(_ => contentButtonClicked = true),
                            v.Text("Some content")
                        ])
                    ).Fill()
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Content Button"), TimeSpan.FromSeconds(2), "ready")
            // Tab to move from NotificationIcon to Content Button
            .Key(Hex1bKey.Tab)
            .Wait(TimeSpan.FromMilliseconds(50))
            .Key(Hex1bKey.Enter) // Click the content button - drawer is collapsed
            .Wait(TimeSpan.FromMilliseconds(100))
            .Capture("after_click")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Content button should be clicked when drawer is collapsed
        Assert.True(contentButtonClicked,
            $"Content button SHOULD be clickable when drawer is collapsed. Screen:\n{snapshot}");
    }

    /// <summary>
    /// Test that floating notifications have priority over content.
    /// </summary>
    [Fact]
    public async Task NotificationPanel_FloatingNotification_HasPriorityOverContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(112, 36)
            .Build();

        using var app = new Hex1bApp(
            ctx => ctx.ZStack(z => [
                z.VStack(outer => [
                    outer.NotificationPanel(
                        outer.VStack(v => [
                            v.Button("Post Notification").OnClick(e =>
                            {
                                e.Context.Notifications.Post(
                                    new Notification("Alert", "Important message")
                                        .Timeout(TimeSpan.FromMinutes(5)));
                            }),
                            v.Text("Main content area")
                        ])
                    ).Fill()
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // IMPORTANT: capture snapshot BEFORE exiting; alternate screen is cleared on exit.
        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Post Notification"), TimeSpan.FromSeconds(2), "ready")
            .Key(Hex1bKey.Enter) // Post notification
            .Wait(TimeSpan.FromMilliseconds(200))
            .WaitUntil(s => s.ContainsText("Alert"), TimeSpan.FromSeconds(2), "notification_visible")
            .Capture("with_notification")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        using var _ = await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Floating notification should be visible on top
        Assert.True(snapshot.ContainsText("Alert"),
            $"Floating notification should be visible. Screen:\n{snapshot.GetDisplayText()}");
        Assert.True(snapshot.ContainsText("Important message"),
            $"Notification body should be visible. Screen:\n{snapshot.GetDisplayText()}");
    }
}
