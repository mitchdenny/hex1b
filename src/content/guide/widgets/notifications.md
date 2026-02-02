<script setup>
const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.ZStack(z => [
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

await terminal.RunAsync();`

const actionsCode = `using Hex1b;

var state = new AppState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.ZStack(z => [
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
}`

const lifecycleCode = `using Hex1b;

var state = new DownloadState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.ZStack(z => [
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
}`
</script>

# Notifications

The notification system provides a way to display transient messages to users. Notifications appear as floating cards in the corner of the screen and can include action buttons, timeouts, and a slide-out drawer for reviewing all notifications.

## Overview

The notification system consists of:

| Component | Purpose |
|-----------|---------|
| `NotificationPanel` | Container that hosts content and displays notifications |
| `NotificationIcon` | Bell icon (🔔) that shows count and toggles the drawer |
| `Notification` | The notification data with title, body, actions, and lifecycle |
| `NotificationStack` | Manages the collection of active notifications |

## Basic Setup

To enable notifications in your app, wrap your content in a `NotificationPanel` inside a `ZStack`:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="notifications-basic" exampleTitle="Notifications - Basic Setup" />

::: tip Layout Pattern
The `ZStack` → `VStack` → `NotificationPanel` pattern ensures notifications float above your content. Place the `NotificationIcon` in your header/menu bar for easy access.
:::

## Posting Notifications

Post notifications from any event handler using `e.Context.Notifications.Post()`:

```csharp
ctx.Button("Save").OnClick(e => {
    SaveFile();
    e.Context.Notifications.Post(
        new Notification("Saved!", "File saved successfully"));
})
```

### Notification Properties

Create notifications with a title and optional body:

```csharp
// Title only
new Notification("Operation complete")

// Title and body
new Notification("Download Complete", "file.zip (2.4 MB) downloaded successfully")
```

### Timeouts

By default, notifications stay visible until dismissed. Add a timeout to auto-hide them:

```csharp
new Notification("Auto-save", "Document saved")
    .Timeout(TimeSpan.FromSeconds(5))
```

When a notification times out:
- It disappears from the floating view
- It remains in the notification drawer
- The `OnTimeout` handler is called (if set)

### Permanent Notifications

Omit `.Timeout()` for notifications that require user attention:

```csharp
// This stays visible until the user dismisses it
new Notification("Action Required", "Please review the pending changes")
    .PrimaryAction("Review", async ctx => OpenReview())
```

## Action Buttons

Notifications can have a primary action and multiple secondary actions displayed in a split button:

<CodeBlock lang="csharp" :code="actionsCode" command="dotnet run" example="notifications-actions" exampleTitle="Notifications - Action Buttons" />

### Primary Action

The main action button, prominently displayed:

```csharp
new Notification("New Message", "You have a new message from Alice")
    .PrimaryAction("Read", async ctx => {
        await OpenMessageAsync();
        ctx.Dismiss(); // Optionally dismiss after action
    })
```

### Secondary Actions

Additional actions appear in a dropdown menu:

```csharp
new Notification("File Updated", "config.json was modified")
    .PrimaryAction("View Diff", async ctx => ViewDiff())
    .SecondaryAction("Revert", async ctx => RevertChanges())
    .SecondaryAction("Ignore", async ctx => ctx.Dismiss())
```

### Action Context

Action handlers receive a `NotificationActionContext` with:

| Property | Description |
|----------|-------------|
| `Notification` | The notification this action belongs to |
| `CancellationToken` | Token for cancelling async operations |
| `InputTrigger` | Access to app services (focus, popups, etc.) |
| `Dismiss()` | Remove this notification from the stack |

```csharp
.PrimaryAction("Open", async ctx => {
    await OpenFileAsync(ctx.CancellationToken);
    ctx.Dismiss(); // Remove notification after action
})
```

## Lifecycle Events

Handle notification lifecycle events with `OnTimeout` and `OnDismiss`:

<CodeBlock lang="csharp" :code="lifecycleCode" command="dotnet run" example="notifications-lifecycle" exampleTitle="Notifications - Lifecycle Events" />

### OnTimeout

Called when the notification auto-hides after its timeout:

```csharp
new Notification("Reminder", "Meeting in 5 minutes")
    .Timeout(TimeSpan.FromMinutes(5))
    .OnTimeout(async ctx => {
        // Log that the user saw the reminder
        Analytics.Track("reminder_shown");
    })
```

### OnDismiss

Called when the notification is dismissed (by user or programmatically):

```csharp
new Notification("Undo Available", "Item deleted")
    .Timeout(TimeSpan.FromSeconds(10))
    .OnDismiss(async ctx => {
        // Cleanup - the undo window has closed
        await PermanentlyDeleteAsync();
    })
```

## The Notification Drawer

The drawer shows all notifications (both active and timed-out). Access it by:

- **Clicking the notification icon** (🔔)
- **Pressing Alt+N** from anywhere in the app

When the drawer opens:
- Floating notifications move into the drawer
- All notifications are visible in a scrollable list
- Click outside or press Escape to close

### Drawer Behavior

| Setting | Method | Description |
|---------|--------|-------------|
| Float over content | `.WithDrawerFloats(true)` | Drawer overlays content (default) |
| Push content aside | `.WithDrawerFloats(false)` | Content resizes when drawer opens |

```csharp
v.NotificationPanel(content)
    .WithDrawerFloats(false) // Drawer docks instead of floating
```

## Notification Icon

The `NotificationIcon` displays the bell and notification count:

```csharp
bar.NotificationIcon()
```

### Customization

```csharp
// Custom bell character (for terminals without emoji support)
bar.NotificationIcon("*")

// Hide the count badge
bar.NotificationIcon().WithCount(false)
```

## Panel Configuration

Configure the notification panel behavior:

```csharp
v.NotificationPanel(content)
    .WithMaxFloating(5)        // Show up to 5 floating notifications (default: 3)
    .WithOffset(4, 2)          // Offset from corner (x=4, y=2)
    .WithAnimation(false)      // Disable progress bar animation
    .WithDrawerFloats(true)    // Drawer floats over content
```

### Configuration Options

| Method | Default | Description |
|--------|---------|-------------|
| `WithMaxFloating(n)` | 3 | Maximum floating notifications visible |
| `WithOffset(x, y)` | (2, 1) | Offset from top-right corner |
| `WithAnimation(bool)` | true | Animate timeout progress bars |
| `WithDrawerFloats(bool)` | true | Drawer floats vs pushes content |

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| Alt+N | Toggle notification drawer |
| Escape | Close drawer (when open) |
| Tab | Navigate between notification buttons |
| Enter | Activate focused button |

## Notification Card Layout

Each notification card displays:

```
┌────────────────────────────────────┐
│ Title                        [ × ] │  ← Dismiss button
│ Body text (if present)             │
│ [ Primary ▼ ]                      │  ← Action button (if present)
│ ▓▓▓▓▓▓▓▓▓░░░░░                    │  ← Timeout progress (floating only)
└────────────────────────────────────┘
```

- **Title**: Always displayed prominently
- **Body**: Optional additional text
- **Dismiss (×)**: Removes the notification entirely
- **Action button**: Split button for primary + secondary actions
- **Progress bar**: Shows time remaining (floating cards only)

## Complete Example

Here's a complete example showing all notification features:

```csharp
using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.ZStack(z => [
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
```

## Related Widgets

- [Button](/guide/widgets/button) - Trigger notifications from button clicks
- [SplitButton](/guide/widgets/split-button) - Used for notification action buttons
- [InfoBar](/guide/widgets/infobar) - Persistent status information
