using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for adding input bindings to widgets.
/// </summary>
public static class InputBindingExtensions
{
    /// <summary>
    /// Adds input bindings to a widget. Multiple calls are additive.
    /// </summary>
    /// <typeparam name="TWidget">The widget type.</typeparam>
    /// <param name="widget">The widget to add bindings to.</param>
    /// <param name="bindings">The bindings to add.</param>
    /// <returns>A new widget with the bindings added.</returns>
    public static TWidget WithBindings<TWidget>(this TWidget widget, params InputBinding[] bindings)
        where TWidget : Hex1bWidget
    {
        var existingBindings = widget.InputBindings ?? [];
        var newBindings = new List<InputBinding>(existingBindings.Count + bindings.Length);
        newBindings.AddRange(existingBindings);
        newBindings.AddRange(bindings);
        
        // Use reflection to create a new instance with the updated bindings
        // This works because widgets are records with 'with' expressions
        return (TWidget)(widget with { InputBindings = newBindings });
    }

    /// <summary>
    /// Sets the input bindings for a widget, replacing any existing bindings.
    /// </summary>
    /// <typeparam name="TWidget">The widget type.</typeparam>
    /// <param name="widget">The widget to set bindings on.</param>
    /// <param name="bindings">The bindings to set.</param>
    /// <returns>A new widget with the bindings set.</returns>
    public static TWidget WithBindingsReplaced<TWidget>(this TWidget widget, params InputBinding[] bindings)
        where TWidget : Hex1bWidget
    {
        return (TWidget)(widget with { InputBindings = bindings.ToList() });
    }

    /// <summary>
    /// Adds a single key binding to a widget.
    /// </summary>
    /// <typeparam name="TWidget">The widget type.</typeparam>
    /// <param name="widget">The widget to add the binding to.</param>
    /// <param name="key">The key to bind.</param>
    /// <param name="handler">The action to execute when the key is pressed.</param>
    /// <param name="description">Optional description for the binding.</param>
    /// <returns>A new widget with the binding added.</returns>
    public static TWidget OnKey<TWidget>(this TWidget widget, Hex1bKey key, Action handler, string? description = null)
        where TWidget : Hex1bWidget
    {
        return widget.WithBindings(InputBinding.Plain(key, handler, description));
    }

    /// <summary>
    /// Adds a Ctrl+Key binding to a widget.
    /// </summary>
    public static TWidget OnCtrl<TWidget>(this TWidget widget, Hex1bKey key, Action handler, string? description = null)
        where TWidget : Hex1bWidget
    {
        return widget.WithBindings(InputBinding.Ctrl(key, handler, description));
    }

    /// <summary>
    /// Adds an Alt+Key binding to a widget.
    /// </summary>
    public static TWidget OnAlt<TWidget>(this TWidget widget, Hex1bKey key, Action handler, string? description = null)
        where TWidget : Hex1bWidget
    {
        return widget.WithBindings(InputBinding.Alt(key, handler, description));
    }

    /// <summary>
    /// Adds a Shift+Key binding to a widget.
    /// </summary>
    public static TWidget OnShift<TWidget>(this TWidget widget, Hex1bKey key, Action handler, string? description = null)
        where TWidget : Hex1bWidget
    {
        return widget.WithBindings(InputBinding.Shift(key, handler, description));
    }

    /// <summary>
    /// Adds a Ctrl+Shift+Key binding to a widget.
    /// </summary>
    public static TWidget OnCtrlShift<TWidget>(this TWidget widget, Hex1bKey key, Action handler, string? description = null)
        where TWidget : Hex1bWidget
    {
        return widget.WithBindings(InputBinding.CtrlShift(key, handler, description));
    }

    /// <summary>
    /// Adds an Escape key binding to a widget.
    /// </summary>
    public static TWidget OnEscape<TWidget>(this TWidget widget, Action handler, string? description = null)
        where TWidget : Hex1bWidget
    {
        return widget.OnKey(Hex1bKey.Escape, handler, description);
    }

    /// <summary>
    /// Adds an Enter key binding to a widget.
    /// </summary>
    public static TWidget OnEnter<TWidget>(this TWidget widget, Action handler, string? description = null)
        where TWidget : Hex1bWidget
    {
        return widget.OnKey(Hex1bKey.Enter, handler, description);
    }
}
