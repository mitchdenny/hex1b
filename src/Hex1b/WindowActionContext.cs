using Hex1b.Events;
using Hex1b.Input;

namespace Hex1b;

/// <summary>
/// Provides context for window title bar action callbacks.
/// </summary>
/// <remarks>
/// <para>
/// This context is passed to action handlers (like close, custom actions) when
/// a title bar button is clicked, allowing the handler to interact with the window
/// and access app-level services.
/// </para>
/// <para>
/// WindowActionContext extends <see cref="WidgetEventArgs"/>, providing access to:
/// <list type="bullet">
///   <item><see cref="WidgetEventArgs.Windows"/> - The window manager for opening other windows</item>
///   <item><see cref="WidgetEventArgs.Popups"/> - The popup stack for menus and dialogs</item>
///   <item><see cref="WidgetEventArgs.Notifications"/> - The notification stack for user feedback</item>
///   <item><see cref="WidgetEventArgs.CancellationToken"/> - For async operations</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// new WindowAction("📌", ctx => {
///     ctx.Notifications.Post(new Notification("Pinned", "Window has been pinned"));
/// })
/// </code>
/// </example>
public sealed class WindowActionContext : WidgetEventArgs
{
    internal WindowActionContext(WindowEntry window, InputBindingActionContext context)
        : base(context)
    {
        Window = window;
    }

    /// <summary>
    /// The window that owns this action.
    /// </summary>
    public WindowEntry Window { get; }

    /// <summary>
    /// Closes this window.
    /// For dialogs with OnResult, this signals cancellation (IsCancelled = true).
    /// </summary>
    public void Close() => Window.Close();

    /// <summary>
    /// Closes this window without a result, signaling cancellation.
    /// Alias for <see cref="Close"/> - for dialogs with OnResult, the callback receives IsCancelled = true.
    /// </summary>
    public void Cancel() => Window.Close();

    /// <summary>
    /// Closes this window with a result value (for modal dialogs).
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="result">The result value.</param>
    public void CloseWithResult<TResult>(TResult result) => Window.CloseWithResult(result);
}
