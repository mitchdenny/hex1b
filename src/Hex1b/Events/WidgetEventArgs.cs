using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Base class for all widget event arguments.
/// Provides access to the InputBindingActionContext for focus navigation, app control, and cancellation.
/// </summary>
public abstract class WidgetEventArgs
{
    /// <summary>
    /// The context providing access to focus navigation, RequestStop, and CancellationToken.
    /// </summary>
    public InputBindingActionContext Context { get; }

    /// <summary>
    /// Convenience accessor for the cancellation token from the application run loop.
    /// </summary>
    public CancellationToken CancellationToken => Context.CancellationToken;
    
    /// <summary>
    /// Convenience accessor for the popup stack of the nearest popup host.
    /// Use this to push popups, menus, and dialogs from event handlers.
    /// The root ZStack automatically provides a PopupStack, so this is never null within a Hex1bApp.
    /// </summary>
    public PopupStack Popups => Context.Popups;

    /// <summary>
    /// Convenience accessor for the notification stack of the nearest notification host.
    /// Use this to post notifications from event handlers.
    /// </summary>
    public NotificationStack Notifications => Context.Notifications;

    /// <summary>
    /// Convenience accessor for the window manager of the nearest window host.
    /// Use this to open, close, and manage floating windows from event handlers.
    /// </summary>
    public WindowManager Windows => Context.Windows;

    protected WidgetEventArgs(InputBindingActionContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }
}
