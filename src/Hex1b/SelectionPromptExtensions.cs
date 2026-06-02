namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building <see cref="SelectionPromptWidget{T}"/>.
/// </summary>
public static class SelectionPromptExtensions
{
    /// <summary>
    /// Creates a Spectre-style filtered selection prompt over <paramref name="items"/>.
    /// </summary>
    public static SelectionPromptWidget<T> SelectionPrompt<TParent, T>(
        this WidgetContext<TParent> context,
        IReadOnlyList<T> items)
        where TParent : Hex1bWidget
        => new(items);

    /// <summary>
    /// Sets the string projection used both for filtering (case-insensitive
    /// substring match) and for default row rendering. Defaults to
    /// <see cref="object.ToString"/>.
    /// </summary>
    public static SelectionPromptWidget<T> ItemText<T>(
        this SelectionPromptWidget<T> widget,
        Func<T, string> selector)
        => widget with { ItemTextSelector = selector };

    /// <summary>
    /// Overrides the string projection used for filter matching and
    /// Right-Arrow prediction. When unset, the <see cref="ItemText{T}"/>
    /// selector is used for both display and filtering; setting this lets
    /// you display rich rows (e.g. <c>name + description</c>) while
    /// filtering only on a salient subset (e.g. <c>name</c>).
    /// </summary>
    public static SelectionPromptWidget<T> FilterText<T>(
        this SelectionPromptWidget<T> widget,
        Func<T, string> selector)
        => widget with { FilterTextSelector = selector };

    /// <summary>
    /// Sets the maximum number of list rows shown at once. The list scrolls
    /// within this window. Defaults to 8.
    /// </summary>
    public static SelectionPromptWidget<T> MaxVisibleItems<T>(
        this SelectionPromptWidget<T> widget,
        int rows)
        => widget with { MaxVisibleItemsValue = Math.Max(1, rows) };

    /// <summary>
    /// Sets the prompt text rendered above the filter textbox. Defaults to
    /// <c>"Filter:"</c>.
    /// </summary>
    public static SelectionPromptWidget<T> Prompt<T>(
        this SelectionPromptWidget<T> widget,
        string text)
        => widget with { PromptText = text };

    /// <summary>
    /// Sets the message shown when the filter excludes every item. Defaults
    /// to <c>"No matches."</c>.
    /// </summary>
    public static SelectionPromptWidget<T> EmptyMessage<T>(
        this SelectionPromptWidget<T> widget,
        string text)
        => widget with { EmptyMessage = text };

    /// <summary>
    /// Sets a synchronous handler invoked when the user presses
    /// <c>Enter</c> on a filtered item.
    /// </summary>
    public static SelectionPromptWidget<T> OnSelected<T>(
        this SelectionPromptWidget<T> widget,
        Action<T> handler)
        => widget with { SelectedHandler = item => { handler(item); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler invoked when the user presses
    /// <c>Enter</c> on a filtered item.
    /// </summary>
    public static SelectionPromptWidget<T> OnSelected<T>(
        this SelectionPromptWidget<T> widget,
        Func<T, Task> handler)
        => widget with { SelectedHandler = handler };
}
