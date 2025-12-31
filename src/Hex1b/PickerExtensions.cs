using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating Picker widgets.
/// </summary>
public static class PickerExtensions
{
    /// <summary>
    /// Creates a picker widget with the specified items.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="items">The list of items to choose from.</param>
    /// <returns>A new PickerWidget.</returns>
    public static PickerWidget Picker<TParent>(
        this WidgetContext<TParent> ctx,
        IReadOnlyList<string> items)
        where TParent : Hex1bWidget
        => new(items);
    
    /// <summary>
    /// Creates a picker widget with the specified items.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="items">The items to choose from.</param>
    /// <returns>A new PickerWidget.</returns>
    public static PickerWidget Picker<TParent>(
        this WidgetContext<TParent> ctx,
        params string[] items)
        where TParent : Hex1bWidget
        => new(items);
    
    /// <summary>
    /// Creates a picker widget with the specified items and initial selection.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="items">The list of items to choose from.</param>
    /// <param name="initialSelectedIndex">The initial selected index.</param>
    /// <returns>A new PickerWidget.</returns>
    public static PickerWidget Picker<TParent>(
        this WidgetContext<TParent> ctx,
        IReadOnlyList<string> items,
        int initialSelectedIndex)
        where TParent : Hex1bWidget
        => new(items) { InitialSelectedIndex = initialSelectedIndex };
}
