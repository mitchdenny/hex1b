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

/// <summary>
/// Represents a title bar action descriptor created by <see cref="TitleActionBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// TitleAction is an immutable descriptor that captures the icon and handler for a
/// title bar button. It is converted to a <see cref="WindowAction"/> internally when
/// the window is opened.
/// </para>
/// </remarks>
public sealed class TitleAction
{
    internal TitleAction(string icon, Action<WindowActionContext> handler, bool isCloseAction = false)
    {
        Icon = icon;
        Handler = handler;
        IsCloseAction = isCloseAction;
    }

    /// <summary>
    /// The icon displayed for this action.
    /// </summary>
    public string Icon { get; }

    /// <summary>
    /// The action handler called when this button is clicked.
    /// </summary>
    public Action<WindowActionContext> Handler { get; }

    /// <summary>
    /// Whether this is a standard close action.
    /// </summary>
    internal bool IsCloseAction { get; }

    /// <summary>
    /// Converts this descriptor to a WindowAction.
    /// </summary>
    internal WindowAction ToWindowAction()
    {
        return IsCloseAction
            ? WindowAction.Close(Icon)
            : new WindowAction(Icon, Handler);
    }
}
