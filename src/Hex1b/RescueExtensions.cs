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
    public static RescueWidget Rescue<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child,
        RescueState? state = null,
        Func<RescueState, Hex1bWidget>? fallbackBuilder = null,
        bool? showDetails = null)
        where TParent : Hex1bWidget
        => new(child, state, fallbackBuilder, showDetails);

    /// <summary>
    /// Wraps a VStack in a rescue boundary that catches exceptions and displays a fallback.
    /// </summary>
    public static RescueWidget Rescue<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> childBuilder,
        RescueState? state = null,
        Func<RescueState, Hex1bWidget>? fallbackBuilder = null,
        bool? showDetails = null)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget>();
        return new RescueWidget(
            new VStackWidget(childBuilder(childCtx)),
            state,
            fallbackBuilder,
            showDetails);
    }

    /// <summary>
    /// Wraps a widget in a rescue boundary with a custom fallback message.
    /// </summary>
    public static RescueWidget Rescue<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child,
        string fallbackMessage,
        RescueState? state = null)
        where TParent : Hex1bWidget
        => new(child, state, _ => new TextBlockWidget(fallbackMessage));
}
