using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="BackdropWidget"/>.
/// Fills available space, intercepts all input, and optionally renders a background.
/// </summary>
public sealed class BackdropNode : Hex1bNode
{
    /// <summary>
    /// The visual style for the backdrop.
    /// </summary>
    public BackdropStyle Style { get; set; } = BackdropStyle.Transparent;
    
    /// <summary>
    /// The optional background color for the backdrop (used when Style is Opaque).
    /// </summary>
    public Hex1bColor? BackgroundColor { get; set; }
    
    /// <summary>
    /// Optional layer identifier for popup stack management.
    /// </summary>
    public string? LayerId { get; set; }
    
    /// <summary>
    /// The source widget for creating event args.
    /// </summary>
    public BackdropWidget? SourceWidget { get; set; }

    /// <summary>
    /// The optional child node to render on top of the backdrop.
    /// </summary>
    public Hex1bNode? Child { get; set; }

    /// <summary>
    /// Simple callback for click-away behavior.
    /// </summary>
    public Func<Task>? ClickAwayHandler { get; set; }
    
    /// <summary>
    /// Rich callback with event args for click-away behavior.
    /// </summary>
    public Func<BackdropClickedEventArgs, Task>? ClickAwayEventHandler { get; set; }

    /// <summary>
    /// Backdrop is always focusable so it can intercept clicks.
    /// </summary>
    public override bool IsFocusable => true;

    private bool _isFocused;
    public override bool IsFocused
    {
        get => _isFocused;
        set
        {
            // If setting focus to true and we have focusable children,
            // delegate focus to the first child instead
            if (value && !_isFocused && Child != null)
            {
                var firstChildFocusable = Child.GetFocusableNodes().FirstOrDefault();
                if (firstChildFocusable != null)
                {
                    firstChildFocusable.IsFocused = true;
                    return; // Don't focus backdrop
                }
            }
            
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
            }
        }
    }

    public override Size Measure(Constraints constraints)
    {
        // Backdrop fills all available space
        var width = constraints.MaxWidth;
        var height = constraints.MaxHeight;

        // Measure child if present (it will be arranged within our bounds)
        Child?.Measure(constraints);

        return new Size(width, height);
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        // Arrange child centered within our bounds (or at its natural size)
        if (Child != null)
        {
            var childSize = Child.Measure(new Constraints(0, bounds.Width, 0, bounds.Height));
            
            // Center the child within the backdrop
            var childX = bounds.X + (bounds.Width - childSize.Width) / 2;
            var childY = bounds.Y + (bounds.Height - childSize.Height) / 2;
            
            Child.Arrange(new Rect(childX, childY, childSize.Width, childSize.Height));
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        // Render background if opaque mode with a color
        if (Style == BackdropStyle.Opaque && BackgroundColor.HasValue)
        {
            var bg = BackgroundColor.Value;
            var bgCode = bg.ToBackgroundAnsi();
            var resetCode = context.Theme.GetResetToGlobalCodes();

            // Fill the entire bounds with the background color
            var spaces = new string(' ', Bounds.Width);
            for (int y = Bounds.Y; y < Bounds.Y + Bounds.Height; y++)
            {
                context.SetCursorPosition(Bounds.X, y);
                context.Write($"{bgCode}{spaces}{resetCode}");
            }
        }
        // Transparent: don't render any background, let base layer show through

        // Render child on top
        if (Child != null)
        {
            context.SetCursorPosition(Child.Bounds.X, Child.Bounds.Y);
            Child.Render(context);
        }
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        var hasHandler = ClickAwayHandler != null || ClickAwayEventHandler != null;
        
        // Backdrop captures Escape to trigger click-away (if handler is set)
        if (hasHandler)
        {
            bindings.Key(Hex1bKey.Escape).Action(async _ => await InvokeClickAway(-1, -1), "Dismiss");
        }

        // Mouse click on backdrop (not on child) triggers click-away
        if (hasHandler)
        {
            bindings.Mouse(MouseButton.Left).Action(async ctx =>
            {
                // Check if click is on child content bounds - if so, don't trigger click-away
                // Use ContentBounds instead of Bounds for nodes like AnchoredNode that
                // have layout bounds larger than their actual content
                if (Child != null && Child.ContentBounds.Contains(ctx.MouseX, ctx.MouseY))
                {
                    return; // Click is on child, not backdrop
                }
                await InvokeClickAway(ctx.MouseX, ctx.MouseY);
            }, "Click away to dismiss");
        }
    }
    
    /// <summary>
    /// Invokes the appropriate click-away handler.
    /// </summary>
    private async Task InvokeClickAway(int x, int y)
    {
        // Prefer the rich event handler if available
        if (ClickAwayEventHandler != null && SourceWidget != null)
        {
            var args = new BackdropClickedEventArgs(SourceWidget, x, y, LayerId);
            await ClickAwayEventHandler(args);
        }
        else if (ClickAwayHandler != null)
        {
            await ClickAwayHandler();
        }
    }

    public override InputResult HandleMouseClick(int localX, int localY, Hex1bMouseEvent mouseEvent)
    {
        // Always consume the click to prevent it from reaching lower layers
        // The binding above handles the click-away logic
        return InputResult.Handled;
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // Return ourselves first so HitTest can find us for click-away detection.
        // When iterating in reverse (as HitTest does), children are checked first,
        // and backdrop acts as the catch-all for clicks not on child content.
        // Note: Our IsFocused setter delegates to children, so keyboard focus
        // will go to children rather than the backdrop itself.
        yield return this;
        
        // Then return child focusables for Tab navigation
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null)
        {
            yield return Child;
        }
    }
}
