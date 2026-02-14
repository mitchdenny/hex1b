namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building EditorWidget.
/// </summary>
public static class EditorExtensions
{
    /// <summary>
    /// Creates an Editor widget bound to the specified state.
    /// </summary>
    public static EditorWidget Editor<TParent>(
        this WidgetContext<TParent> ctx,
        EditorState state)
        where TParent : Hex1bWidget
        => new(state);
}
