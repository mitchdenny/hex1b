namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// A context for building widgets within a parent container.
/// The TParentWidget type constrains which child widgets can be created.
/// Extension methods return widgets directly; covariance allows collection expressions.
/// </summary>
/// <typeparam name="TParentWidget">The parent widget type - constrains valid children.</typeparam>
public class WidgetContext<TParentWidget>
    where TParentWidget : Hex1bWidget
{
    internal WidgetContext() { }
}
