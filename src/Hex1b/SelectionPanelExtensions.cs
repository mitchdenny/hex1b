namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building <see cref="SelectionPanelWidget"/>.
/// </summary>
public static class SelectionPanelExtensions
{
    /// <summary>
    /// Wraps the supplied child in a <see cref="SelectionPanelWidget"/>.
    /// At this stage the panel is a pure pass-through; future iterations will
    /// add a copy/select mode similar to <c>TerminalWidget</c>'s.
    /// </summary>
    public static SelectionPanelWidget SelectionPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(child);
}
