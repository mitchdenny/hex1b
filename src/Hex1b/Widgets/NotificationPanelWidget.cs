using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A notification panel widget that hosts notifications and displays them as floating overlays
/// or in a docked drawer.
/// </summary>
/// <remarks>
/// <para>
/// This is a placeholder implementation for Phase 2. The full implementation will include:
/// <list type="bullet">
///   <item><description>Floating notification cards as overlays</description></item>
///   <item><description>Expandable drawer showing all notifications</description></item>
///   <item><description>Configurable max visible floating notifications</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed record NotificationPanelWidget : Hex1bWidget
{
    /// <summary>
    /// Animation interval for progress bar updates (50ms = 20fps).
    /// </summary>
    private static readonly TimeSpan AnimationInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// The content to display in the main area (notifications overlay on top of this).
    /// </summary>
    internal Hex1bWidget? Content { get; init; }

    /// <summary>
    /// Maximum number of notifications to show floating at once.
    /// </summary>
    public int MaxFloating { get; init; } = 3;

    /// <summary>
    /// Horizontal offset from the right edge for floating notifications.
    /// </summary>
    public int OffsetX { get; init; } = 2;

    /// <summary>
    /// Vertical offset from the top edge for floating notifications.
    /// </summary>
    public int OffsetY { get; init; } = 1;

    /// <summary>
    /// Whether to enable animation for progress bars.
    /// </summary>
    public bool EnableAnimation { get; init; } = true;

    /// <summary>
    /// Sets the content that notifications will overlay.
    /// </summary>
    /// <param name="content">The main content widget.</param>
    public NotificationPanelWidget WithContent(Hex1bWidget content)
        => this with { Content = content };

    /// <summary>
    /// Sets the maximum number of floating notifications.
    /// </summary>
    /// <param name="max">Maximum floating notifications (default: 3).</param>
    public NotificationPanelWidget WithMaxFloating(int max)
        => this with { MaxFloating = max };

    /// <summary>
    /// Sets the offset from the corner for floating notifications.
    /// </summary>
    /// <param name="x">Horizontal offset from right edge.</param>
    /// <param name="y">Vertical offset from top edge.</param>
    public NotificationPanelWidget WithOffset(int x, int y)
        => this with { OffsetX = x, OffsetY = y };

    /// <summary>
    /// Enables or disables animation for timeout progress bars.
    /// </summary>
    /// <param name="enable">True to enable animation (default), false to disable.</param>
    public NotificationPanelWidget WithAnimation(bool enable = true)
        => this with { EnableAnimation = enable };

    /// <summary>
    /// Returns animation interval to keep progress bars updating.
    /// </summary>
    internal override TimeSpan? GetEffectiveRedrawDelay()
    {
        // If explicitly set via RedrawDelay, use that
        if (RedrawDelay.HasValue)
        {
            return RedrawDelay;
        }

        // Enable animation for progress bars on notification cards
        return EnableAnimation ? AnimationInterval : null;
    }

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as NotificationPanelNode ?? new NotificationPanelNode();
        node.MaxFloating = MaxFloating;
        node.OffsetX = OffsetX;
        node.OffsetY = OffsetY;

        // Reconcile content
        if (Content != null)
        {
            node.Content = await context.ReconcileChildAsync(node.Content, Content, node);
        }
        else
        {
            node.Content = null;
        }

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(NotificationPanelNode);
}
