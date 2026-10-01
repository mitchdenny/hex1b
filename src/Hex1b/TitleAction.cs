namespace Hex1b;

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
