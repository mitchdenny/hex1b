using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A notification bell icon widget that displays the notification count and toggles the panel.
/// Must be placed inside a NotificationPanel to function.
/// </summary>
public sealed record NotificationIconWidget : Hex1bWidget
{
    /// <summary>
    /// The bell character to display.
    /// </summary>
    public string BellCharacter { get; init; } = "🔔";

    /// <summary>
    /// Whether to show the count badge.
    /// </summary>
    public bool ShowCount { get; init; } = true;

    /// <summary>
    /// Sets the bell character.
    /// </summary>
    public NotificationIconWidget WithBell(string bell)
        => this with { BellCharacter = bell };

    /// <summary>
    /// Sets whether to show the count badge.
    /// </summary>
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
