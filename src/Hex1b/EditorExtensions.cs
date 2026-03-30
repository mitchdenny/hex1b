namespace Hex1b;

using Hex1b.Layout;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for building EditorWidget.
/// </summary>
public static class EditorExtensions
{
    /// <summary>
    /// Creates an Editor widget bound to the specified state.
    /// The editor defaults to <see cref="SizeHint.Fill"/> height so it expands to fill
    /// available vertical space in stack layouts rather than requesting unbounded height.
    /// </summary>
    public static EditorWidget Editor<TParent>(
        this WidgetContext<TParent> ctx,
        EditorState state)
        where TParent : Hex1bWidget
        => new(state) { HeightHint = SizeHint.Fill };
}
