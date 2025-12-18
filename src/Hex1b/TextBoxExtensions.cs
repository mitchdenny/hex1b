namespace Hex1b;

using Hex1b.Events;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for building TextBoxWidget.
/// </summary>
public static class TextBoxExtensions
{
    /// <summary>
    /// Creates a TextBox with default empty text.
    /// </summary>
    public static TextBoxWidget TextBox<TParent>(
        this WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
        => new();

    /// <summary>
    /// Creates a TextBox with the specified text.
    /// </summary>
    public static TextBoxWidget TextBox<TParent>(
        this WidgetContext<TParent> ctx,
        string text)
        where TParent : Hex1bWidget
        => new(text);

    /// <summary>
    /// Creates a TextBox with a synchronous text changed handler.
    /// </summary>
    public static TextBoxWidget TextBox<TParent>(
        this WidgetContext<TParent> ctx,
        string? text,
        Action<TextChangedEventArgs> onTextChanged)
        where TParent : Hex1bWidget
        => new(text) { OnTextChanged = args => { onTextChanged(args); return Task.CompletedTask; } };

    /// <summary>
    /// Creates a TextBox with an asynchronous text changed handler.
    /// </summary>
    public static TextBoxWidget TextBox<TParent>(
        this WidgetContext<TParent> ctx,
        string? text,
        Func<TextChangedEventArgs, Task> onTextChanged)
        where TParent : Hex1bWidget
        => new(text) { OnTextChanged = onTextChanged };

    /// <summary>
    /// Creates a TextBox with text changed and submit handlers.
    /// </summary>
    public static TextBoxWidget TextBox<TParent>(
        this WidgetContext<TParent> ctx,
        string? text,
        Action<TextChangedEventArgs>? onTextChanged,
        Action<TextSubmittedEventArgs> onSubmit)
        where TParent : Hex1bWidget
        => new(text) 
        { 
            OnTextChanged = onTextChanged != null 
                ? args => { onTextChanged(args); return Task.CompletedTask; } 
                : null,
            OnSubmit = args => { onSubmit(args); return Task.CompletedTask; } 
        };

    /// <summary>
    /// Creates a TextBox with a synchronous submit handler.
    /// </summary>
    public static TextBoxWidget TextBox<TParent>(
        this WidgetContext<TParent> ctx,
        string? text,
        Action<TextSubmittedEventArgs> onSubmit)
        where TParent : Hex1bWidget
        => new(text) { OnSubmit = args => { onSubmit(args); return Task.CompletedTask; } };

    /// <summary>
    /// Creates a TextBox with an asynchronous submit handler.
    /// </summary>
    public static TextBoxWidget TextBox<TParent>(
        this WidgetContext<TParent> ctx,
        string? text,
        Func<TextSubmittedEventArgs, Task> onSubmit)
        where TParent : Hex1bWidget
        => new(text) { OnSubmit = onSubmit };
}
