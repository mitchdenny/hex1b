namespace Hex1b;

using Hex1b.Events;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating ToggleSwitchWidget.
/// </summary>
public static class ToggleSwitchExtensions
{
    /// <summary>
    /// Creates a ToggleSwitchWidget with the provided options.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="options">The available options for the toggle switch.</param>
    /// <param name="selectedIndex">The initial selected option index (default is 0).</param>
    /// <returns>A new ToggleSwitchWidget.</returns>
    public static ToggleSwitchWidget ToggleSwitch<TParent>(
        this WidgetContext<TParent> ctx,
        IReadOnlyList<string> options,
        int selectedIndex = 0)
        where TParent : Hex1bWidget
        => new(options, selectedIndex);
}
