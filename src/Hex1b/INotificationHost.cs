namespace Hex1b;

/// <summary>
/// Interface for nodes that can host notifications.
/// Implemented by NotificationPanelNode to allow notification discovery from anywhere in the tree.
/// </summary>
public interface INotificationHost
{
    /// <summary>
    /// The notification stack for this host.
    /// </summary>
    NotificationStack Notifications { get; }
}
