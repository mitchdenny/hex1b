namespace Hex1b;

/// <summary>
/// Represents an action button on a notification.
/// </summary>
/// <param name="Label">The button label text.</param>
/// <param name="Handler">The async handler invoked when the action is triggered.</param>
public sealed record NotificationAction(
    string Label,
    Func<NotificationActionContext, Task> Handler);
