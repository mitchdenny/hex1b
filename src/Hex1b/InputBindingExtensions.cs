using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for adding input bindings to widgets.
/// </summary>
public static class InputBindingExtensions
{
    /// <summary>
    /// Configures input bindings for a widget using a fluent builder.
    /// The builder is pre-populated with the widget's default bindings,
    /// which can be inspected, modified, or removed.
    /// </summary>
    /// <typeparam name="TWidget">The widget type.</typeparam>
    /// <param name="widget">The widget to configure bindings for.</param>
    /// <param name="configure">A callback to configure bindings. The builder contains default bindings.</param>
    /// <returns>A new widget with the binding configurator set.</returns>
    /// <example>
    /// <code>
    /// ctx.TextBox(state).WithInputBindings(bindings => 
    /// {
    ///     // At breakpoint here, bindings.Bindings shows all defaults
    ///     
    ///     // Add a chord binding
    ///     bindings.Ctrl().Key(Hex1bKey.K)
    ///         .Then().Key(Hex1bKey.C)
    ///         .Action(() => DoSomething());
    ///     
    ///     // Override a default
    ///     bindings.Ctrl().Key(Hex1bKey.A).Action(() => CustomSelectAll());
    ///     
    ///     // Remove a default
    ///     bindings.Remove(Hex1bKey.Delete);
    /// });
    /// </code>
    /// </example>
    public static TWidget WithInputBindings<TWidget>(this TWidget widget, Action<InputBindingsBuilder> configure)
        where TWidget : Hex1bWidget
    {
        return (TWidget)(widget with { BindingsConfigurator = configure });
    }
}
