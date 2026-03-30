using Hex1b.Events;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating form buttons (submit, cancel).
/// </summary>
public static class FormButtonExtensions
{
    /// <summary>
    /// Creates a submit button for the form. The button can optionally be disabled
    /// until all specified fields pass validation.
    /// </summary>
    /// <param name="ctx">The form context.</param>
    /// <param name="label">The button label text.</param>
    /// <param name="handler">The click handler invoked on submit.</param>
    public static ButtonWidget SubmitButton(
        this FormContext ctx,
        string label,
        Action<ButtonClickedEventArgs> handler)
        => new ButtonWidget(label).OnClick(handler);

    /// <summary>
    /// Creates an async submit button for the form.
    /// </summary>
    public static ButtonWidget SubmitButton(
        this FormContext ctx,
        string label,
        Func<ButtonClickedEventArgs, Task> handler)
        => new ButtonWidget(label).OnClick(handler);

    /// <summary>
    /// Creates a cancel button for the form.
    /// </summary>
    /// <param name="ctx">The form context.</param>
    /// <param name="label">The button label text. Defaults to "Cancel".</param>
    /// <param name="handler">The click handler invoked on cancel.</param>
    public static ButtonWidget CancelButton(
        this FormContext ctx,
        string label,
        Action<ButtonClickedEventArgs> handler)
        => new ButtonWidget(label).OnClick(handler);

    /// <summary>
    /// Creates a cancel button with default "Cancel" label.
    /// </summary>
    public static ButtonWidget CancelButton(
        this FormContext ctx,
        Action<ButtonClickedEventArgs> handler)
        => new ButtonWidget("Cancel").OnClick(handler);
}
