using Hex1b.Nodes;

namespace Hex1b.Input;

/// <summary>
/// Context passed to input binding action handlers and widget event handlers.
/// Provides app-level services for focus navigation, cancellation, and other common operations.
/// </summary>
public sealed class InputBindingActionContext
{
    private readonly FocusRing _focusRing;
    private readonly Action? _requestStop;
    private readonly Action<string>? _copyToClipboard;
    private readonly Action? _invalidate;

    /// <summary>
    /// Cancellation token from the application run loop.
    /// Use this for async operations that should be cancelled when the app stops.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// The absolute X coordinate of the mouse event, if this context was created for a mouse binding.
    /// -1 if not applicable (e.g., for keyboard bindings).
    /// </summary>
    public int MouseX { get; }

    /// <summary>
    /// The absolute Y coordinate of the mouse event, if this context was created for a mouse binding.
    /// -1 if not applicable (e.g., for keyboard bindings).
    /// </summary>
    public int MouseY { get; }

    internal InputBindingActionContext(
        FocusRing focusRing, 
        Action? requestStop = null, 
        CancellationToken cancellationToken = default,
        int mouseX = -1,
        int mouseY = -1,
        Action<string>? copyToClipboard = null,
        Action? invalidate = null)
    {
        _focusRing = focusRing;
        _requestStop = requestStop;
        _copyToClipboard = copyToClipboard;
        _invalidate = invalidate;
        CancellationToken = cancellationToken;
        MouseX = mouseX;
        MouseY = mouseY;
    }

    /// <summary>
    /// Requests the application to stop. The RunAsync call will exit gracefully
    /// after the current frame completes.
    /// </summary>
    public void RequestStop() => _requestStop?.Invoke();

    /// <summary>
    /// Copies the specified text to the system clipboard using the OSC 52 escape sequence.
    /// This works on terminals that support the OSC 52 clipboard protocol (most modern terminals).
    /// </summary>
    /// <param name="text">The text to copy to the clipboard.</param>
    /// <remarks>
    /// OSC 52 is the standard escape sequence for clipboard access:
    /// ESC ] 52 ; c ; &lt;base64-data&gt; ST
    /// 
    /// Not all terminals support OSC 52. Some (like older xterm configurations) may require
    /// explicit enablement. Common terminals with support: iTerm2, Windows Terminal, Alacritty,
    /// kitty, tmux (with set-clipboard on), and others.
    /// </remarks>
    public void CopyToClipboard(string text) => _copyToClipboard?.Invoke(text);

    /// <summary>
    /// Signals that the UI needs to be re-rendered.
    /// </summary>
    /// <remarks>
    /// Call this method after async operations complete that modify widget state.
    /// Without calling Invalidate(), the render loop won't wake up until the next
    /// input event, and your changes won't be visible.
    /// 
    /// This is automatically called by most widget event handlers, but if you're
    /// doing async work in a callback, you should call this when your changes are ready.
    /// </remarks>
    public void Invalidate() => _invalidate?.Invoke();

    /// <summary>
    /// Moves focus to the next focusable widget in the ring.
    /// This is the standard way to implement Tab navigation.
    /// </summary>
    /// <returns>True if focus was moved, false if there are no focusables.</returns>
    public bool FocusNext() => _focusRing.FocusNext();

    /// <summary>
    /// Moves focus to the previous focusable widget in the ring.
    /// This is the standard way to implement Shift+Tab navigation.
    /// </summary>
    /// <returns>True if focus was moved, false if there are no focusables.</returns>
    public bool FocusPrevious() => _focusRing.FocusPrevious();

    /// <summary>
    /// Gets the currently focused node, or null if none.
    /// </summary>
    public Hex1bNode? FocusedNode => _focusRing.FocusedNode;

    /// <summary>
    /// Gets all focusable nodes in render order.
    /// </summary>
    public IReadOnlyList<Hex1bNode> Focusables => _focusRing.Focusables;

    /// <summary>
    /// Focuses a specific node if it's in the ring.
    /// </summary>
    /// <returns>True if the node was found and focused.</returns>
    public bool Focus(Hex1bNode node) => _focusRing.Focus(node);
    
    /// <summary>
    /// Focuses the first node in the ring that matches the predicate.
    /// </summary>
    /// <param name="predicate">A function that returns true for the node to focus.</param>
    /// <returns>True if a matching node was found and focused, false otherwise.</returns>
    public bool FocusWhere(Func<Hex1bNode, bool> predicate) => _focusRing.FocusWhere(predicate);
    
    /// <summary>
    /// Gets the popup stack for the nearest popup host (typically a ZStack) in the focused node's ancestry.
    /// Use this to push popups, menus, and dialogs from event handlers.
    /// The root ZStack automatically provides a PopupStack, so this is never null within a Hex1bApp.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.Button("File")
    ///    .OnClick(ctx => ctx.Popups.Push(() => BuildFileMenu()));
    /// </code>
    /// </example>
    public PopupStack Popups => FindNearestPopupHost()?.Popups 
        ?? throw new InvalidOperationException("No popup host found. Ensure code runs within a Hex1bApp context.");
    
    /// <summary>
    /// Gets the notification stack for the nearest notification host (typically a NotificationPanel) in the focused node's ancestry.
    /// Use this to post notifications from event handlers.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.Button("Save")
    ///    .OnClick(e => {
    ///        SaveFile();
    ///        e.Notifications.Post(new Notification("Saved", "File saved successfully"));
    ///    });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">Thrown if no notification host is found in the tree.</exception>
    public NotificationStack Notifications => FindNearestNotificationHost()?.Notifications
        ?? throw new InvalidOperationException("No notification host found. Add a NotificationPanel to your widget tree.");
    
    /// <summary>
    /// Tries to get the notification stack, returning null if no notification host is found.
    /// Use this when notifications are optional and you don't want an exception.
    /// </summary>
    public NotificationStack? TryGetNotifications() => FindNearestNotificationHost()?.Notifications;
    
    private IPopupHost? FindNearestPopupHost()
    {
        // Walk from the focused node up the parent chain to find the nearest popup host
        Hex1bNode? current = FocusedNode;
        while (current != null)
        {
            if (current is IPopupHost host)
            {
                return host;
            }
            current = current.Parent;
        }
        return null;
    }
    
    private INotificationHost? FindNearestNotificationHost()
    {
        // Walk from the focused node up the parent chain to find the nearest notification host
        Hex1bNode? current = FocusedNode;
        while (current != null)
        {
            if (current is INotificationHost host)
            {
                return host;
            }
            current = current.Parent;
        }
        return null;
    }

    /// <summary>
    /// Gets the window manager for the nearest window host (typically a WindowPanel) in the focused node's ancestry.
    /// Use this to open, close, and manage floating windows from event handlers.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.Button("Settings")
    ///    .OnClick(e => e.Windows.Open(
    ///        id: "settings",
    ///        title: "Settings",
    ///        content: () => BuildSettingsContent()
    ///    ));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">Thrown if no window host is found in the tree.</exception>
    public WindowManager Windows => FindNearestWindowHost()?.Windows
        ?? throw new InvalidOperationException("No window host found. Add a WindowPanel to your widget tree.");

    /// <summary>
    /// Tries to get the window manager, returning null if no window host is found.
    /// Use this when windows are optional and you don't want an exception.
    /// </summary>
    public WindowManager? TryGetWindows() => FindNearestWindowHost()?.Windows;

    private IWindowHost? FindNearestWindowHost()
    {
        // Walk from the focused node up the parent chain to find the nearest window host
        Hex1bNode? current = FocusedNode;
        while (current != null)
        {
            if (current is IWindowHost host)
            {
                return host;
            }
            current = current.Parent;
        }
        return null;
    }
}
