using Hex1b.Events;
using Hex1b.Input;

namespace Hex1b.Widgets;

/// <summary>
/// Displays an icon (single character or short string) that can optionally respond to clicks.
/// </summary>
/// <remarks>
/// <para>
/// IconWidget is a simple display widget for icons, emoji, or short labels. When a click handler
/// is attached, it becomes clickable and can trigger actions.
/// </para>
/// <para>
/// Icons support a loading state that shows an animated spinner instead of the icon. This is useful
/// when the icon triggers an async operation. Use <see cref="WithLoading(bool)"/> to toggle
/// between icon and spinner display.
/// </para>
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
/// <para>Icon with loading state:</para>
/// <code>
/// ctx.Icon("▶").WithLoading(isLoading)
/// </code>
/// </example>
/// <param name="Icon">The icon character or string to display.</param>
public sealed record IconWidget(string Icon) : Hex1bWidget
{
    /// <summary>
    /// Gets whether the icon is in loading state (shows spinner instead of icon).
    /// </summary>
    public bool IsLoading { get; init; }
    
    /// <summary>
    /// Gets the spinner style to use when loading. If null, uses theme default.
    /// </summary>
    public SpinnerStyle? LoadingStyle { get; init; }
    
    /// <summary>
    /// Click handler for when the icon is clicked.
    /// </summary>
    internal Func<IconClickedEventArgs, Task>? ClickHandler { get; init; }
    
    /// <summary>
    /// Sets the loading state. When true, displays a spinner instead of the icon.
    /// </summary>
    public IconWidget WithLoading(bool isLoading) => this with { IsLoading = isLoading };
    
    /// <summary>
    /// Sets the spinner style to use during loading.
    /// </summary>
    public IconWidget WithLoadingStyle(SpinnerStyle style) => this with { LoadingStyle = style };
    
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

    /// <inheritdoc/>
    internal override TimeSpan? GetEffectiveRedrawDelay()
    {
        // If explicitly set, use that value
        if (RedrawDelay.HasValue)
        {
            return RedrawDelay;
        }

        // Auto-schedule redraws when loading (for spinner animation)
        if (IsLoading)
        {
            return LoadingStyle?.Interval ?? SpinnerStyle.Dots.Interval;
        }

        return null;
    }

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as IconNode ?? new IconNode();

        // Mark dirty if properties changed
        if (node.Icon != Icon || node.IsLoading != IsLoading || node.LoadingStyle != LoadingStyle)
        {
            node.MarkDirty();
        }

        node.Icon = Icon;
        node.IsLoading = IsLoading;
        node.LoadingStyle = LoadingStyle;
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
