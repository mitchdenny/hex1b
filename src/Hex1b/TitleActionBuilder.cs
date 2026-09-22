namespace Hex1b;

/// <summary>
/// Builder for creating title bar actions in the fluent window API.
/// </summary>
/// <remarks>
/// <para>
/// TitleActionBuilder is passed to the <see cref="WindowHandle.LeftTitleActions"/> and
/// <see cref="WindowHandle.RightTitleActions"/> builder functions, providing methods
/// to create standard and custom title bar actions.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// window.RightTitleActions(t =&gt; [
///     t.Action("?", ctx =&gt; ShowHelp()),
///     t.Close()
/// ]);
/// </code>
/// </para>
/// </remarks>
public sealed class TitleActionBuilder
{
    /// <summary>
    /// Creates a custom action with the specified icon and handler.
    /// </summary>
    /// <param name="icon">The icon to display (emoji or single character).</param>
    /// <param name="handler">The action handler called when clicked.</param>
    /// <returns>A title action descriptor.</returns>
    /// <example>
    /// <code>
    /// t.Action("📌", ctx =&gt; ctx.Notifications.Post(new Notification("Pinned!")))
    /// </code>
    /// </example>
    public TitleAction Action(string icon, Action<WindowActionContext> handler)
    {
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(handler);
        return new TitleAction(icon, handler);
    }

    /// <summary>
    /// Creates a standard close action that closes the window when clicked.
    /// </summary>
    /// <param name="icon">Optional custom icon. Defaults to "×".</param>
    /// <returns>A title action descriptor for the close button.</returns>
    public TitleAction Close(string icon = "×")
    {
        return new TitleAction(icon, ctx => ctx.Close(), isCloseAction: true);
    }
}
