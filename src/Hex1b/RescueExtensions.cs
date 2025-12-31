namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building RescueWidget.
/// </summary>
public static class RescueExtensions
{
    /// <summary>
    /// Wraps a widget in a rescue boundary that catches exceptions and displays a fallback.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="child">The child widget to protect.</param>
    /// <returns>A new RescueWidget.</returns>
    /// <example>
    /// <code>
    /// ctx.Rescue(ctx.SomeWidget())
    ///    .OnRescue(e => logger.LogError(e.Exception, "Error in {Phase}", e.Phase))
    /// </code>
    /// </example>
    public static RescueWidget Rescue<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(child);

    /// <summary>
    /// Wraps a VStack in a rescue boundary that catches exceptions and displays a fallback.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="childBuilder">A builder function that creates VStack children.</param>
    /// <returns>A new RescueWidget containing a VStack.</returns>
    /// <example>
    /// <code>
    /// ctx.Rescue(v => [
    ///     v.Text("Some content"),
    ///     v.Button("Click me")
    /// ])
    /// </code>
    /// </example>
    public static RescueWidget Rescue<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> childBuilder)
        where TParent : Hex1bWidget
    {
        try
        {
            var childCtx = new WidgetContext<VStackWidget>();
            return new RescueWidget(new VStackWidget(childBuilder(childCtx)));
        }
        catch (Exception ex)
        {
            // Capture Build phase exception and pass it to RescueWidget
            return new RescueWidget(null) { BuildException = ex };
        }
    }

    /// <summary>
    /// Wraps a widget in a rescue boundary with hidden exception details.
    /// Useful for production environments where you want a friendly error message.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="child">The child widget to protect.</param>
    /// <returns>A new RescueWidget with ShowDetails set to false.</returns>
    public static RescueWidget RescueFriendly<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new RescueWidget(child) { ShowDetails = false };
}
