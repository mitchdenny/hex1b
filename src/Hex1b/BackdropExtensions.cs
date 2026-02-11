namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating <see cref="BackdropWidget"/>.
/// </summary>
public static class BackdropExtensions
{
    /// <summary>
    /// Creates a Backdrop that fills available space and intercepts all input.
    /// Use this to create modal overlays that prevent interaction with layers below.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <returns>A new BackdropWidget.</returns>
    /// <example>
    /// <code>
    /// // Simple backdrop that blocks input
    /// ctx.ZStack(z => [
    ///     z.VStack(...),  // Base content
    ///     showModal ? z.Backdrop() : null
    /// ])
    /// </code>
    /// </example>
    public static BackdropWidget Backdrop<TParent>(this WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
        => new();

    /// <summary>
    /// Creates a Backdrop with child content displayed on top.
    /// The child is centered within the backdrop.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="child">The child widget to display on top of the backdrop.</param>
    /// <returns>A new BackdropWidget with the child.</returns>
    /// <example>
    /// <code>
    /// // Modal dialog with click-away to dismiss
    /// ctx.ZStack(z => [
    ///     z.VStack(...),  // Base content
    ///     showModal 
    ///         ? z.Backdrop(z.Border(z.VStack(v => [
    ///               v.Text("Modal content"),
    ///               v.Button("Close").OnClick(_ => showModal = false)
    ///           ])).Title("Dialog"))
    ///           .WithBackground(Hex1bColor.FromRgb(0, 0, 0)) // Dim background
    ///           .OnClickAway(() => showModal = false)
    ///         : null
    /// ])
    /// </code>
    /// </example>
    public static BackdropWidget Backdrop<TParent>(this WidgetContext<TParent> ctx, Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(child);
}
