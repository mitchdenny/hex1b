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
/// // Window with close button (default)
/// e.Windows.Open("my-window", "My Window", () => content);
/// 
/// // Custom actions
/// e.Windows.Open("editor", "Editor", () => content, new WindowOptions
/// {
///     RightTitleBarActions = [
///         new WindowAction("?", ctx => ShowHelp()),
///         WindowAction.Close()
///     ]
/// });
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
