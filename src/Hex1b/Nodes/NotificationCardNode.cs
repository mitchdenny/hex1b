using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="NotificationCardWidget"/>.
/// Displays a notification with title, optional body, action buttons, and timeout progress bar.
/// Composes child ButtonNode for dismiss and SplitButtonNode for actions.
/// </summary>
public sealed class NotificationCardNode : Hex1bNode
{
    /// <summary>
    /// The notification title.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// The notification body text (optional).
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// The notification this card displays.
    /// </summary>
    public Notification? Notification { get; set; }

    /// <summary>
    /// The notification stack for dismiss operations.
    /// </summary>
    public NotificationStack? Stack { get; set; }

    /// <summary>
    /// The primary action.
    /// </summary>
    public NotificationAction? PrimaryAction { get; set; }

    /// <summary>
    /// The secondary actions.
    /// </summary>
    public IReadOnlyList<NotificationAction> SecondaryActions { get; set; } = [];

    /// <summary>
    /// The dismiss button child node.
    /// </summary>
    public ButtonNode? DismissButton { get; set; }

    /// <summary>
    /// The action button child node.
    /// </summary>
    public SplitButtonNode? ActionButton { get; set; }

    /// <summary>
    /// Whether to show the timeout progress bar.
    /// When false, the progress bar is hidden (e.g., in the drawer view).
    /// </summary>
    public bool ShowProgressBar { get; set; } = true;

    private bool _isFocused;
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

    private bool _isHovered;
    public override bool IsHovered
    {
        get => _isHovered;
        set
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => false; // Children are focusable, not the card itself
    public override bool ManagesChildFocus => true;

    // Card dimensions
    private const int CardWidth = 40;
    private const int MinCardHeight = 2;

    // Cached positions for child layout
    private int _dismissButtonX;
    private int _actionButtonY;

    /// <summary>
    /// Returns focusable children (dismiss button and action button).
    /// </summary>
    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (DismissButton != null) yield return DismissButton;
        if (ActionButton != null) yield return ActionButton;
    }

    /// <summary>
    /// Returns all children for rendering.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (DismissButton != null) yield return DismissButton;
        if (ActionButton != null) yield return ActionButton;
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Escape dismisses the notification (global shortcut)
        bindings.Key(Hex1bKey.Escape).Action(DismissNotification, "Dismiss");
    }

    private async Task DismissNotification(InputBindingActionContext ctx)
    {
        if (Notification != null && Stack != null)
        {
            // Invoke dismiss handler if set
            if (Notification.DismissHandler != null)
            {
                var eventCtx = new NotificationEventContext(Notification, Stack, ctx.CancellationToken);
                await Notification.DismissHandler(eventCtx);
            }

            Stack.Dismiss(Notification);
        }
    }

    // Cached sizes from measuring children
    private Size _dismissButtonSize;
    private Size _actionButtonSize;

    protected override Size MeasureCore(Constraints constraints)
    {
        // Calculate content height
        var contentHeight = 1; // Title row (includes dismiss button)
        if (!string.IsNullOrEmpty(Body))
        {
            // Wrap body text to card width (minus border + padding)
            var bodyLines = WrapText(Body, CardWidth - 4); // -2 for half-block borders, -2 for padding
            contentHeight += bodyLines.Count;
        }
        if (ActionButton != null)
        {
            contentHeight += 1; // Action row
        }
        if (ShowProgressBar && Notification?.TimeoutDuration != null)
        {
            contentHeight += 1; // Progress bar row
        }

        // Add 2 rows for top and bottom half-block borders
        var height = contentHeight + 2;

        // Measure child buttons and cache their sizes
        if (DismissButton != null)
        {
            _dismissButtonSize = DismissButton.Measure(new Constraints(0, 5, 0, 1)); // "[ × ]" style
        }
        if (ActionButton != null)
        {
            _actionButtonSize = ActionButton.Measure(new Constraints(0, CardWidth - 4, 0, 1));
        }

        var size = new Size(CardWidth, Math.Max(MinCardHeight + 2, height));
        return constraints.Constrain(size);
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);

        var x = bounds.X;
        var y = bounds.Y;
        var width = bounds.Width;

        // Content starts after top border row
        var contentY = y + 1;
        var contentWidth = width - 2; // Subtract left and right half-block borders

        // Position dismiss button at top-right (inside content area)
        if (DismissButton != null)
        {
            var dismissWidth = _dismissButtonSize.Width;
            _dismissButtonX = x + width - 1 - dismissWidth; // -1 for right border
            DismissButton.Arrange(new Rect(_dismissButtonX, contentY, dismissWidth, 1));
        }

        // Calculate action button row position
        var actionY = contentY + 1; // After title
        if (!string.IsNullOrEmpty(Body))
        {
            var bodyLines = WrapText(Body, contentWidth - 2); // -2 for padding
            actionY += bodyLines.Count;
        }

        // Position action button
        if (ActionButton != null)
        {
            _actionButtonY = actionY;
            var actionWidth = _actionButtonSize.Width;
            ActionButton.Arrange(new Rect(x + 2, actionY, actionWidth, 1)); // +2 for left border + padding
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var originalTheme = context.Theme;
        
        // Get colors from theme
        var cardBg = originalTheme.Get(NotificationCardTheme.BackgroundColor);
        var globalBg = originalTheme.Get(GlobalTheme.BackgroundColor);
        var titleColor = originalTheme.Get(NotificationCardTheme.TitleColor);
        var bodyColor = originalTheme.Get(NotificationCardTheme.BodyColor);

        // Create a scoped theme for children that sets button background to card background
        // This ensures buttons rendered inside the card use the card's background color
        var childTheme = originalTheme.Clone();
        childTheme.Set(ButtonTheme.BackgroundColor, cardBg);
        context.Theme = childTheme;

        // Half-block border: card bg as foreground, global bg as background
        // This creates the illusion of a soft-edged card floating over the content
        var cardBgFgAnsi = cardBg.ToForegroundAnsi();  // For half-block characters
        var globalBgAnsi = globalBg.ToBackgroundAnsi(); // Background behind half-blocks
        var cardBgAnsi = cardBg.ToBackgroundAnsi();
        var titleFgAnsi = titleColor.ToForegroundAnsi();
        var bodyFgAnsi = bodyColor.ToForegroundAnsi();
        var resetCodes = childTheme.GetResetToGlobalCodes();

        var x = Bounds.X;
        var y = Bounds.Y;
        var width = Bounds.Width;
        var height = Bounds.Height;

        // Half-block characters for soft border effect
        // Top border uses ▄ (lower half filled) - connects to content below
        // Bottom border uses ▀ (upper half filled) - connects to content above
        const char topBorder = '▄';    // Lower half is fg color
        const char bottomBorder = '▀'; // Upper half is fg color  
        const char leftEdge = '▐';     // Right half is fg color (on left edge of card)
        const char rightEdge = '▌';    // Left half is fg color (on right edge of card)
        
        // Corner quadrant blocks for rounded effect
        const char topLeftCorner = '▗';     // Lower-right quadrant (top-left of card)
        const char topRightCorner = '▖';    // Lower-left quadrant (top-right of card)
        const char bottomLeftCorner = '▝';  // Upper-right quadrant (bottom-left of card)
        const char bottomRightCorner = '▘'; // Upper-left quadrant (bottom-right of card)

        var contentWidth = width - 2; // Minus left and right borders
        var currentY = y;

        // ═══ TOP BORDER ROW (with corner quadrants) ═══
        context.WriteClipped(x, currentY, $"{cardBgFgAnsi}{globalBgAnsi}{topLeftCorner}{new string(topBorder, contentWidth)}{topRightCorner}{resetCodes}");
        currentY++;

        // ═══ TITLE ROW ═══
        var dismissWidth = _dismissButtonSize.Width > 0 ? _dismissButtonSize.Width : 5;
        var titleMaxWidth = contentWidth - 1 - dismissWidth; // padding + space for dismiss
        
        // Use display width for proper emoji/wide character handling
        var titleDisplayWidth = DisplayWidth.GetStringWidth(Title);
        string displayTitle;
        int displayTitleWidth;
        
        if (titleDisplayWidth > titleMaxWidth - 1)
        {
            var (sliced, columns, _, _) = DisplayWidth.SliceByDisplayWidth(Title, 0, titleMaxWidth - 1);
            displayTitle = sliced + "…";
            displayTitleWidth = columns + 1; // +1 for ellipsis
        }
        else
        {
            displayTitle = Title;
            displayTitleWidth = titleDisplayWidth;
        }
        var titlePadding = titleMaxWidth - displayTitleWidth;

        // Build the title row content string up to the dismiss button
        var titleContent = $"{cardBgFgAnsi}{globalBgAnsi}{leftEdge}{titleFgAnsi}{cardBgAnsi}{displayTitle}{new string(' ', Math.Max(0, titlePadding))}";
        
        // Fill gap between title and dismiss button (if any)
        if (DismissButton != null)
        {
            var titleEndX = x + 1 + titleMaxWidth;
            var gapWidth = _dismissButtonX - titleEndX;
            if (gapWidth > 0)
            {
                titleContent += new string(' ', gapWidth);
            }
            titleContent += resetCodes;
            context.WriteClipped(x, currentY, titleContent);
            context.RenderChild(DismissButton);
        }
        else
        {
            titleContent += resetCodes;
            context.WriteClipped(x, currentY, titleContent);
        }
        
        // Right border for title row
        context.WriteClipped(x + width - 1, currentY, $"{cardBgFgAnsi}{globalBgAnsi}{rightEdge}{resetCodes}");
        currentY++;

        // ═══ BODY ROWS ═══
        if (!string.IsNullOrEmpty(Body))
        {
            var bodyLines = WrapText(Body, contentWidth - 2); // -2 for internal padding
            foreach (var line in bodyLines)
            {
                if (currentY >= y + height - 1 - (ShowProgressBar && Notification?.TimeoutDuration != null ? 1 : 0) - (ActionButton != null ? 1 : 0)) break;

                var paddedLine = line.PadRight(contentWidth - 2);
                context.WriteClipped(x, currentY, $"{cardBgFgAnsi}{globalBgAnsi}{leftEdge}{bodyFgAnsi}{cardBgAnsi} {paddedLine} {cardBgFgAnsi}{globalBgAnsi}{rightEdge}{resetCodes}");
                currentY++;
            }
        }

        // ═══ ACTION ROW ═══
        if (ActionButton != null)
        {
            if (currentY < y + height - 1 - (ShowProgressBar && Notification?.TimeoutDuration != null ? 1 : 0))
            {
                // Left border + padding
                context.WriteClipped(x, currentY, $"{cardBgFgAnsi}{globalBgAnsi}{leftEdge}{cardBgAnsi} ");
                
                context.RenderChild(ActionButton);
                
                // Fill rest of row up to right border
                var actionWidth = _actionButtonSize.Width;
                var actionEndX = x + 2 + actionWidth;
                var rightBorderX = x + width - 1;
                var actionPadding = rightBorderX - actionEndX;
                if (actionPadding > 0)
                {
                    context.WriteClipped(actionEndX, currentY, $"{cardBgAnsi}{new string(' ', actionPadding)}{resetCodes}");
                }
                context.WriteClipped(rightBorderX, currentY, $"{cardBgFgAnsi}{globalBgAnsi}{rightEdge}{resetCodes}");
                currentY++;
            }
        }

        // ═══ FILL REMAINING CONTENT ROWS ═══
        var progressBarRow = ShowProgressBar && Notification?.TimeoutDuration != null ? 1 : 0;
        while (currentY < y + height - 1 - progressBarRow)
        {
            context.WriteClipped(x, currentY, $"{cardBgFgAnsi}{globalBgAnsi}{leftEdge}{cardBgAnsi}{new string(' ', contentWidth)}{cardBgFgAnsi}{globalBgAnsi}{rightEdge}{resetCodes}");
            currentY++;
        }

        // ═══ PROGRESS BAR ROW (inside border) ═══
        if (ShowProgressBar && Notification?.TimeoutDuration != null && currentY < y + height - 1)
        {
            var progress = CalculateTimeoutProgress();
            
            // Get braille characters from theme
            var filledChar = childTheme.Get(NotificationCardTheme.ProgressFilledCharacter);
            var rightEdgeChar = childTheme.Get(NotificationCardTheme.ProgressLeftHalfCharacter);
            
            // Half-cell precision for progress bar (inside borders)
            var halfCellUnits = progress * contentWidth * 2;
            var filledWidth = (int)(halfCellUnits / 2);
            var hasHalfCell = ((int)halfCellUnits % 2) == 1;
            var emptyWidth = contentWidth - filledWidth - (hasHalfCell ? 1 : 0);

            var filledBar = new string(filledChar, filledWidth);
            var halfPart = hasHalfCell ? rightEdgeChar.ToString() : "";
            var emptyBar = new string(' ', emptyWidth);

            var progressColor = childTheme.Get(NotificationCardTheme.ProgressBarColor);
            var progressFgAnsi = progressColor.ToForegroundAnsi();
            context.WriteClipped(x, currentY, $"{cardBgFgAnsi}{globalBgAnsi}{leftEdge}{progressFgAnsi}{cardBgAnsi}{filledBar}{halfPart}{emptyBar}{cardBgFgAnsi}{globalBgAnsi}{rightEdge}{resetCodes}");
            currentY++;
        }

        // ═══ BOTTOM BORDER ROW (with corner quadrants) ═══
        context.WriteClipped(x, currentY, $"{cardBgFgAnsi}{globalBgAnsi}{bottomLeftCorner}{new string(bottomBorder, contentWidth)}{bottomRightCorner}{resetCodes}");
        
        // Restore original theme
        context.Theme = originalTheme;
    }

    private double CalculateTimeoutProgress()
    {
        if (Notification?.TimeoutDuration == null) return 0;

        var elapsed = DateTimeOffset.Now - Notification.CreatedAt;
        var remaining = Notification.TimeoutDuration.Value - elapsed;
        
        if (remaining <= TimeSpan.Zero) return 0;
        
        return remaining.TotalMilliseconds / Notification.TimeoutDuration.Value.TotalMilliseconds;
    }

    private string BuildActionText(int maxWidth)
    {
        var parts = new List<string>();

        if (PrimaryAction != null)
        {
            var arrow = SecondaryActions.Count > 0 ? " ▼" : "";
            parts.Add($"[{PrimaryAction.Label}{arrow}]");
        }

        var result = string.Join(" ", parts);
        if (result.Length > maxWidth)
        {
            result = result[..(maxWidth - 1)] + "…";
        }

        return result.PadRight(maxWidth);
    }

    private static List<string> WrapText(string text, int maxWidth)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text) || maxWidth <= 0)
        {
            return lines;
        }

        var words = text.Split(' ');
        var currentLine = "";

        foreach (var word in words)
        {
            if (currentLine.Length == 0)
            {
                currentLine = word;
            }
            else if (currentLine.Length + 1 + word.Length <= maxWidth)
            {
                currentLine += " " + word;
            }
            else
            {
                lines.Add(currentLine);
                currentLine = word;
            }
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine);
        }

        return lines;
    }
}
