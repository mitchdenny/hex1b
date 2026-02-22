namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating floated widgets within float-aware containers.
/// </summary>
public static class FloatExtensions
{
    /// <summary>
    /// Wraps a widget in a <see cref="FloatWidget"/>, removing it from the container's
    /// normal layout flow. The float can then be positioned with <see cref="FloatWidget.Absolute"/>
    /// or anchor alignment methods like <see cref="FloatWidget.AlignRight"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.VStack(v => [
    ///     v.Text("Normal flow"),
    ///     v.Float(v.Icon("📍")).Absolute(10, 5),
    /// ])
    /// </code>
    /// </example>
    public static FloatWidget Float<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child)
        where TParent : Hex1bWidget, IFloatWidgetContainer
        => new(child);
}
