using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A notification bell icon widget that displays the notification count and toggles the drawer.
/// </summary>
/// <remarks>
/// <para>
/// The notification icon displays a bell character (🔔 by default) followed by the unread
/// notification count. When clicked or activated with Enter, it toggles the notification drawer.
/// </para>
/// <para>
/// <strong>Requirements:</strong> This widget must be placed inside a <see cref="NotificationPanelWidget"/>
/// subtree to function correctly. It automatically discovers the parent panel's notification stack.
/// </para>
/// <para>
/// <strong>Keyboard shortcuts:</strong>
/// <list type="bullet">
///   <item><description>Alt+N toggles the notification drawer from anywhere in the app.</description></item>
///   <item><description>Enter or Space activates the icon when focused.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <para>A typical menu bar with notification icon:</para>
/// <code>
/// ctx.ZStack(z =&gt; [
///     z.VStack(v =&gt; [
///         v.HStack(bar =&gt; [
///             bar.Button("File"),
///             bar.Button("Edit"),
///             bar.Text("").FillWidth(), // Spacer
///             bar.NotificationIcon()
///         ]),
///         v.NotificationPanel(
///             v.Text("Main content here")
///         ).Fill()
///     ])
/// ])
/// </code>
/// </example>
/// <seealso cref="NotificationPanelWidget"/>
/// <seealso cref="NotificationStack"/>
public sealed record NotificationIconWidget : Hex1bWidget
{
    /// <summary>
    /// The bell character to display. Defaults to "🔔".
    /// </summary>
    public string BellCharacter { get; init; } = "🔔";

    /// <summary>
    /// Whether to show the notification count badge next to the bell. Defaults to true.
    /// </summary>
    public bool ShowCount { get; init; } = true;

    /// <summary>
    /// Sets the bell character to display.
    /// </summary>
    /// <param name="bell">The character or string to use as the bell icon.</param>
    /// <returns>A new widget instance with the bell character configured.</returns>
    /// <remarks>
    /// You can use emoji (🔔), Unicode characters (␇), or plain ASCII (*) depending
    /// on your terminal's capabilities.
    /// </remarks>
    public NotificationIconWidget WithBell(string bell)
        => this with { BellCharacter = bell };

    /// <summary>
    /// Sets whether to show the notification count badge.
    /// </summary>
    /// <param name="show">True to show the count (default), false to hide it.</param>
    /// <returns>A new widget instance with the setting configured.</returns>
    public NotificationIconWidget WithCount(bool show = true)
        => this with { ShowCount = show };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as NotificationIconNode ?? new NotificationIconNode();
        node.BellCharacter = BellCharacter;
        node.ShowCount = ShowCount;
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(NotificationIconNode);
}
