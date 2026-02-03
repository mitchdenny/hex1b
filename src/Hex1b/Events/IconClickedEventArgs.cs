using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for IconWidget click events.
/// </summary>
public sealed class IconClickedEventArgs
{
    /// <summary>
    /// The widget that raised the event.
    /// </summary>
    public IconWidget Widget { get; }
    
    /// <summary>
    /// The node that raised the event.
    /// </summary>
    public IconNode Node { get; }
    
    /// <summary>
    /// The input binding context providing access to app services.
    /// </summary>
    public InputBindingActionContext Context { get; }

    internal IconClickedEventArgs(IconWidget widget, IconNode node, InputBindingActionContext context)
    {
        Widget = widget;
        Node = node;
        Context = context;
    }
}
