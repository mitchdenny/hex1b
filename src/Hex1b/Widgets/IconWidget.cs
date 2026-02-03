using Hex1b.Events;

namespace Hex1b.Widgets;

/// <summary>
/// Displays an icon (single character or short string) that can optionally respond to clicks.
/// </summary>
/// <remarks>
/// IconWidget is a simple display widget for icons, emoji, or short labels. When a click handler
/// is attached, it becomes clickable and can trigger actions.
/// </remarks>
/// <example>
/// <para>Simple icon:</para>
/// <code>
/// ctx.Icon("▶")
/// </code>
/// <para>Clickable icon:</para>
/// <code>
/// ctx.Icon("▶").OnClick(e => { /* handle click */ })
/// </code>
/// </example>
/// <param name="Icon">The icon character or string to display.</param>
public sealed record IconWidget(string Icon) : Hex1bWidget
{
    /// <summary>
    /// Click handler for when the icon is clicked.
    /// </summary>
    internal Func<IconClickedEventArgs, Task>? ClickHandler { get; init; }
    
    /// <summary>
    /// Attaches a synchronous click handler.
    /// </summary>
    public IconWidget OnClick(Action<IconClickedEventArgs> handler)
        => this with { ClickHandler = args => { handler(args); return Task.CompletedTask; } };
    
    /// <summary>
    /// Attaches an asynchronous click handler.
    /// </summary>
    public IconWidget OnClick(Func<IconClickedEventArgs, Task> handler)
        => this with { ClickHandler = handler };

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as IconNode ?? new IconNode();

        // Mark dirty if properties changed
        if (node.Icon != Icon)
        {
            node.MarkDirty();
        }

        node.Icon = Icon;
        node.SourceWidget = this;
        
        // Set click callback
        if (ClickHandler != null)
        {
            node.ClickCallback = async ctx =>
            {
                var args = new IconClickedEventArgs(this, node, ctx);
                await ClickHandler(args);
            };
        }
        else
        {
            node.ClickCallback = null;
        }

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(IconNode);
}
