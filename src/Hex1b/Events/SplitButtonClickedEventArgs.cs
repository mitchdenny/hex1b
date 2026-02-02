using Hex1b.Input;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for split button click events.
/// </summary>
/// <remarks>
/// <para>
/// This event args class is passed to handlers registered with
/// <see cref="Widgets.SplitButtonWidget.PrimaryAction(string, System.Action{SplitButtonClickedEventArgs})"/>
/// and <see cref="Widgets.SplitButtonWidget.SecondaryAction(string, System.Action{SplitButtonClickedEventArgs})"/>.
/// </para>
/// <para>
/// Use <see cref="Context"/> to access app-level services like focus management, popups, notifications,
/// and clipboard.
/// </para>
/// </remarks>
/// <seealso cref="Widgets.SplitButtonWidget"/>
public sealed class SplitButtonClickedEventArgs : EventArgs
{
    internal SplitButtonClickedEventArgs(
        Widgets.SplitButtonWidget widget,
        SplitButtonNode node,
        InputBindingActionContext context)
    {
        Widget = widget;
        Node = node;
        Context = context;
    }

    /// <summary>
    /// The split button widget that was clicked.
    /// </summary>
    public Widgets.SplitButtonWidget Widget { get; }

    /// <summary>
    /// The split button node that rendered the click.
    /// </summary>
    public SplitButtonNode Node { get; }

    /// <summary>
    /// The input context for this event, providing access to app-level services.
    /// </summary>
    /// <remarks>
    /// Use this to access notifications (<c>Context.Notifications</c>), manage focus,
    /// show popups, access the clipboard, or request the app to stop.
    /// </remarks>
    public InputBindingActionContext Context { get; }
}
