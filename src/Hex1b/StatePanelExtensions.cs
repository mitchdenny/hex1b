namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating <see cref="StatePanelWidget"/>.
/// </summary>
public static class StatePanelExtensions
{
    /// <summary>
    /// Creates an identity-anchored state panel. The state object's reference
    /// identity determines which node is reused across reconciliation frames,
    /// enabling state preservation across list reorders.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.StatePanel(myViewModel, sp =>
    ///     sp.Text($"Count: {myViewModel.Count}")
    /// );
    /// </code>
    /// </example>
    public static StatePanelWidget StatePanel<TParent>(
        this WidgetContext<TParent> ctx,
        object stateKey,
        Func<StatePanelContext, Hex1bWidget> builder)
        where TParent : Hex1bWidget
        => new(stateKey, builder);

    /// <summary>
    /// Creates an identity-anchored state panel with an implicit VStack for multiple children.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.StatePanel(myViewModel, sp => [
    ///     sp.Text("Line 1"),
    ///     sp.Text("Line 2"),
    /// ]);
    /// </code>
    /// </example>
    public static StatePanelWidget StatePanel<TParent>(
        this WidgetContext<TParent> ctx,
        object stateKey,
        Func<StatePanelContext, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
        => new(stateKey, sp => new VStackWidget(builder(sp)));
}
