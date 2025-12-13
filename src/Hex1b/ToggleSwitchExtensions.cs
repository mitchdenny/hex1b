namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating ToggleSwitchWidget.
/// </summary>
public static class ToggleSwitchExtensions
{
    /// <summary>
    /// Creates a ToggleSwitchWidget with the provided state.
    /// </summary>
    public static ToggleSwitchWidget ToggleSwitch<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        ToggleSwitchState state)
        where TParent : Hex1bWidget
        => new(state);

    /// <summary>
    /// Creates a ToggleSwitchWidget with state derived from the parent state.
    /// </summary>
    public static ToggleSwitchWidget ToggleSwitch<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<TState, ToggleSwitchState> stateSelector)
        where TParent : Hex1bWidget
        => new(stateSelector(ctx.State));

    /// <summary>
    /// Creates a ToggleSwitchWidget with inline options and a callback.
    /// </summary>
    public static ToggleSwitchWidget ToggleSwitch<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        IReadOnlyList<string> options,
        int selectedIndex = 0,
        Action<int, string>? onSelectionChanged = null)
        where TParent : Hex1bWidget
        => new(new ToggleSwitchState
        {
            Options = options,
            SelectedIndex = selectedIndex,
            OnSelectionChanged = onSelectionChanged
        });
}
