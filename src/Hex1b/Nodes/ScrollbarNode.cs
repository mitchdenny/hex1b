using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A standalone scrollbar node that handles thumb dragging and track clicking.
/// </summary>
public sealed class ScrollbarNode : Hex1bNode
{
    /// <summary>
    /// The scrollbar orientation.
    /// </summary>
    public ScrollOrientation Orientation { get; set; } = ScrollOrientation.Vertical;

    /// <summary>
    /// The total size of the content being scrolled.
    /// </summary>
    public int ContentSize { get; set; } = 100;

    /// <summary>
    /// The visible viewport size.
    /// </summary>
    public int ViewportSize { get; set; } = 50;

    /// <summary>
    /// The current scroll offset.
    /// </summary>
    public int Offset { get; set; }

    /// <summary>
    /// Handler called when scroll offset changes.
    /// </summary>
    public Func<int, Task>? ScrollHandler { get; set; }

    /// <summary>
    /// Whether the scrollbar is needed (content exceeds viewport).
    /// </summary>
    public bool IsScrollable => ContentSize > ViewportSize;

    /// <summary>
    /// The maximum scroll offset.
    /// </summary>
    public int MaxOffset => Math.Max(0, ContentSize - ViewportSize);

    /// <inheritdoc />
    public override bool IsFocusable => IsScrollable;

    private bool _isFocused;
    /// <inheritdoc />
    public override bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
            }
        }
    }

    /// <inheritdoc />
    protected override Size MeasureCore(Constraints constraints)
    {
        // Scrollbar is 1 cell wide/tall
        if (Orientation == ScrollOrientation.Vertical)
        {
            var height = constraints.MaxHeight < 10000 ? constraints.MaxHeight : 10;
            return constraints.Constrain(new Size(1, height));
        }
        else
        {
            var width = constraints.MaxWidth < 10000 ? constraints.MaxWidth : 10;
            return constraints.Constrain(new Size(width, 1));
        }
    }

    /// <inheritdoc />
    protected override void ArrangeCore(Rect bounds)
    {
        base.Arrange(bounds);
    }

    /// <inheritdoc />
    public override void Render(Hex1bRenderContext context)
    {
        if (!IsScrollable) return;

        var theme = context.Theme;
        var trackColor = theme.Get(ScrollTheme.TrackColor);
        var thumbColor = IsFocused
            ? theme.Get(ScrollTheme.FocusedThumbColor)
            : theme.Get(ScrollTheme.ThumbColor);

        if (Orientation == ScrollOrientation.Vertical)
        {
            RenderVertical(context, theme, trackColor, thumbColor);
        }
        else
        {
            RenderHorizontal(context, theme, trackColor, thumbColor);
        }
    }

    private void RenderVertical(Hex1bRenderContext context, Hex1bTheme theme, Hex1bColor trackColor, Hex1bColor thumbColor)
    {
        var trackChar = theme.Get(ScrollTheme.VerticalTrackCharacter);
        var thumbChar = theme.Get(ScrollTheme.VerticalThumbCharacter);

        var scrollbarHeight = Bounds.Height;
        var (thumbSize, thumbPosition) = CalculateThumb(scrollbarHeight);

        for (int row = 0; row < scrollbarHeight; row++)
        {
            context.SetCursorPosition(Bounds.X, Bounds.Y + row);

            string charToRender;
            Hex1bColor color;

            if (row >= thumbPosition && row < thumbPosition + thumbSize)
            {
                charToRender = thumbChar;
                color = thumbColor;
            }
            else
            {
                charToRender = trackChar;
                color = trackColor;
            }

            context.Write($"{color.ToForegroundAnsi()}{charToRender}\x1b[0m");
        }
    }

    private void RenderHorizontal(Hex1bRenderContext context, Hex1bTheme theme, Hex1bColor trackColor, Hex1bColor thumbColor)
    {
        var trackChar = theme.Get(ScrollTheme.HorizontalTrackCharacter);
        var thumbChar = theme.Get(ScrollTheme.HorizontalThumbCharacter);

        var scrollbarWidth = Bounds.Width;
        var (thumbSize, thumbPosition) = CalculateThumb(scrollbarWidth);

        for (int col = 0; col < scrollbarWidth; col++)
        {
            context.SetCursorPosition(Bounds.X + col, Bounds.Y);

            string charToRender;
            Hex1bColor color;

            if (col >= thumbPosition && col < thumbPosition + thumbSize)
            {
                charToRender = thumbChar;
                color = thumbColor;
            }
            else
            {
                charToRender = trackChar;
                color = trackColor;
            }

            context.Write($"{color.ToForegroundAnsi()}{charToRender}\x1b[0m");
        }
    }

    private (int thumbSize, int thumbPosition) CalculateThumb(int trackLength)
    {
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)ViewportSize / ContentSize * trackLength));
        var scrollRange = trackLength - thumbSize;
        var thumbPosition = MaxOffset > 0
            ? (int)Math.Round((double)Offset / MaxOffset * scrollRange)
            : 0;

        return (thumbSize, thumbPosition);
    }

    /// <inheritdoc />
    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Keyboard scrolling when focused
        if (Orientation == ScrollOrientation.Vertical)
        {
            bindings.Key(Hex1bKey.UpArrow).Action(_ => ScrollByAmount(-1), "Scroll up");
            bindings.Key(Hex1bKey.DownArrow).Action(_ => ScrollByAmount(1), "Scroll down");
            bindings.Key(Hex1bKey.PageUp).Action(_ => ScrollByPage(-1), "Page up");
            bindings.Key(Hex1bKey.PageDown).Action(_ => ScrollByPage(1), "Page down");
            bindings.Key(Hex1bKey.Home).Action(_ => SetOffset(0), "Scroll to top");
            bindings.Key(Hex1bKey.End).Action(_ => SetOffset(MaxOffset), "Scroll to bottom");
        }
        else
        {
            bindings.Key(Hex1bKey.LeftArrow).Action(_ => ScrollByAmount(-1), "Scroll left");
            bindings.Key(Hex1bKey.RightArrow).Action(_ => ScrollByAmount(1), "Scroll right");
            bindings.Key(Hex1bKey.Home).Action(_ => SetOffset(0), "Scroll to start");
            bindings.Key(Hex1bKey.End).Action(_ => SetOffset(MaxOffset), "Scroll to end");
        }

        // Mouse drag on scrollbar
        bindings.Drag(MouseButton.Left).Action(HandleDrag, "Drag scrollbar");
    }

    private DragHandler HandleDrag(int localX, int localY)
    {
        var trackLength = Orientation == ScrollOrientation.Vertical ? Bounds.Height : Bounds.Width;
        var localPos = Orientation == ScrollOrientation.Vertical ? localY : localX;

        var (thumbSize, thumbPosition) = CalculateThumb(trackLength);

        if (localPos >= thumbPosition && localPos < thumbPosition + thumbSize)
        {
            // Clicked on thumb - start drag
            var startOffset = Offset;
            var scrollRange = trackLength - thumbSize;
            var contentPerPixel = MaxOffset > 0 && scrollRange > 0
                ? (double)MaxOffset / scrollRange
                : 0;

            return DragHandler.Simple(
                onMove: (deltaX, deltaY) =>
                {
                    if (contentPerPixel > 0)
                    {
                        var delta = Orientation == ScrollOrientation.Vertical ? deltaY : deltaX;
                        var newOffset = (int)Math.Round(startOffset + delta * contentPerPixel);
                        SetOffset(Math.Clamp(newOffset, 0, MaxOffset));
                    }
                }
            );
        }
        else if (localPos < thumbPosition)
        {
            // Clicked before thumb - page backward
            ScrollByPage(-1);
        }
        else
        {
            // Clicked after thumb - page forward
            ScrollByPage(1);
        }

        return new DragHandler();
    }

    private void ScrollByAmount(int amount)
    {
        SetOffset(Math.Clamp(Offset + amount, 0, MaxOffset));
    }

    private void ScrollByPage(int direction)
    {
        var pageSize = Math.Max(1, ViewportSize - 1);
        SetOffset(Math.Clamp(Offset + direction * pageSize, 0, MaxOffset));
    }

    private void SetOffset(int newOffset)
    {
        if (newOffset != Offset)
        {
            Offset = newOffset;
            MarkDirty();
            _ = ScrollHandler?.Invoke(newOffset);
        }
    }

    /// <inheritdoc />
    public override InputResult HandleMouseClick(int localX, int localY, Hex1bMouseEvent mouseEvent)
    {
        // The drag binding handles clicks, but we also capture here to prevent propagation
        if (mouseEvent.Button == MouseButton.Left && IsScrollable)
        {
            return InputResult.Handled;
        }
        return InputResult.NotHandled;
    }
}
