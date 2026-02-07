namespace Hex1b;

/// <summary>
/// Represents an action button that can be displayed in a window's title bar.
/// </summary>
/// <remarks>
/// <para>
/// Window actions receive a <see cref="WindowActionContext"/> that provides access
/// to the window being acted upon. This enables reusable actions like the standard
/// close button.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Window with default close button
/// var window = e.Windows.Window(w => w.Text("Content"))
///     .Title("My Window");
/// e.Windows.Open(window);
/// 
/// // Custom title bar actions
/// var editor = e.Windows.Window(w => w.Text("Editor content"))
///     .Title("Editor")
///     .RightTitleActions(a => [a.Action("?", ShowHelp), a.Close()]);
/// e.Windows.Open(editor);
/// </code>
/// </example>
public sealed record WindowAction
{
    /// <summary>
    /// Creates a new window action with the specified icon and handler.
    /// </summary>
    /// <param name="icon">The icon to display (emoji or single character).</param>
    /// <param name="handler">The action handler called when clicked.</param>
    public WindowAction(string icon, Action<WindowActionContext> handler)
    {
        Icon = icon ?? throw new ArgumentNullException(nameof(icon));
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
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
    /// Optional tooltip text for this action.
    /// </summary>
    public string? Tooltip { get; init; }

    /// <summary>
    /// Creates a close action that closes the window when clicked.
    /// </summary>
    /// <param name="icon">Optional custom icon. Defaults to "×".</param>
    /// <returns>A window action that closes the window.</returns>
    public static WindowAction Close(string icon = "×")
        => new(icon, ctx => ctx.Close()) { Tooltip = "Close" };
}
