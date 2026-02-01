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

    public override Size Measure(Constraints constraints)
    {
        // Calculate height based on content
        var height = 1; // Title row (includes dismiss button)
        if (!string.IsNullOrEmpty(Body))
        {
            // Wrap body text to card width
            var bodyLines = WrapText(Body, CardWidth - 2); // -2 for padding
            height += bodyLines.Count;
        }
        if (ActionButton != null)
        {
            height += 1; // Action row
        }
        if (ShowProgressBar && Notification?.Timeout != null)
        {
            height += 1; // Progress bar row
        }

        // Measure child buttons and cache their sizes
        if (DismissButton != null)
        {
            _dismissButtonSize = DismissButton.Measure(new Constraints(0, 5, 0, 1)); // "[ × ]" style
        }
        if (ActionButton != null)
        {
            _actionButtonSize = ActionButton.Measure(new Constraints(0, CardWidth - 2, 0, 1));
        }

        var size = new Size(CardWidth, Math.Max(MinCardHeight, height));
        return constraints.Constrain(size);
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        var x = bounds.X;
        var y = bounds.Y;
        var width = bounds.Width;

        // Position dismiss button at top-right
        if (DismissButton != null)
        {
            var dismissWidth = _dismissButtonSize.Width;
            _dismissButtonX = x + width - dismissWidth;
            DismissButton.Arrange(new Rect(_dismissButtonX, y, dismissWidth, 1));
        }

        // Calculate action button row position
        var actionY = y + 1; // After title
        if (!string.IsNullOrEmpty(Body))
        {
            var bodyLines = WrapText(Body, width - 2);
            actionY += bodyLines.Count;
        }

        // Position action button
        if (ActionButton != null)
        {
            _actionButtonY = actionY;
            var actionWidth = _actionButtonSize.Width;
            ActionButton.Arrange(new Rect(x + 1, actionY, actionWidth, 1));
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        
        // Get colors from theme
        var bg = theme.Get(NotificationCardTheme.BackgroundColor);
        var titleColor = theme.Get(NotificationCardTheme.TitleColor);
        var bodyColor = theme.Get(NotificationCardTheme.BodyColor);

        var bgAnsi = bg.ToBackgroundAnsi();
        var titleFgAnsi = titleColor.ToForegroundAnsi();
        var bodyFgAnsi = bodyColor.ToForegroundAnsi();
        var resetCodes = theme.GetResetToGlobalCodes();

        var x = Bounds.X;
        var y = Bounds.Y;
        var width = Bounds.Width;
        var height = Bounds.Height;

        var currentY = y;

        // Draw title row background (dismiss button renders itself)
        var dismissWidth = _dismissButtonSize.Width > 0 ? _dismissButtonSize.Width : 5;
        var titleMaxWidth = width - 1 - dismissWidth; // padding + space for dismiss
        var displayTitle = Title.Length > titleMaxWidth 
            ? Title[..(titleMaxWidth - 1)] + "…" 
            : Title;
        var titlePadding = titleMaxWidth - displayTitle.Length;

        context.SetCursorPosition(x, currentY);
        context.Write($"{titleFgAnsi}{bgAnsi} {displayTitle}{new string(' ', Math.Max(0, titlePadding))}{resetCodes}");
        
        // Render dismiss button child
        if (DismissButton != null)
        {
            context.RenderChild(DismissButton);
        }
        currentY++;

        // Draw body if present
        if (!string.IsNullOrEmpty(Body))
        {
            var bodyLines = WrapText(Body, width - 2);
            foreach (var line in bodyLines)
            {
                if (currentY >= y + height - (ShowProgressBar && Notification?.Timeout != null ? 1 : 0) - (ActionButton != null ? 1 : 0)) break;

                var paddedLine = line.PadRight(width - 2);
                context.SetCursorPosition(x, currentY);
                context.Write($"{bodyFgAnsi}{bgAnsi} {paddedLine} {resetCodes}");
                currentY++;
            }
        }

        // Draw action row background and render action button child
        if (ActionButton != null)
        {
            if (currentY < y + height - (ShowProgressBar && Notification?.Timeout != null ? 1 : 0))
            {
                // Fill the row with background, then let button render on top
                var actionWidth = _actionButtonSize.Width;
                var actionPadding = width - 2 - actionWidth;
                context.SetCursorPosition(x, currentY);
                context.Write($"{bgAnsi} {resetCodes}"); // Left padding
                
                context.RenderChild(ActionButton);
                
                // Fill rest of row
                if (actionPadding > 0)
                {
                    context.SetCursorPosition(x + 1 + actionWidth, currentY);
                    context.Write($"{bgAnsi}{new string(' ', actionPadding + 1)}{resetCodes}");
                }
                currentY++;
            }
        }

        // Fill remaining rows (except progress bar)
        var progressBarRow = ShowProgressBar && Notification?.Timeout != null ? 1 : 0;
        while (currentY < y + height - progressBarRow)
        {
            context.SetCursorPosition(x, currentY);
            context.Write($"{bgAnsi}{new string(' ', width)}{resetCodes}");
            currentY++;
        }

        // Draw timeout progress bar if notification has a timeout and we should show it
        if (ShowProgressBar && Notification?.Timeout != null && currentY < y + height)
        {
            var progress = CalculateTimeoutProgress();
            
            // Get braille characters from theme
            var filledChar = theme.Get(NotificationCardTheme.ProgressFilledCharacter);
            var leftHalfChar = theme.Get(NotificationCardTheme.ProgressLeftHalfCharacter);
            
            // Half-cell precision: multiply by 2 to get half-cell units
            var halfCellUnits = progress * width * 2;
            var filledWidth = (int)(halfCellUnits / 2);
            var hasHalfCell = ((int)halfCellUnits % 2) == 1;
            var emptyWidth = width - filledWidth - (hasHalfCell ? 1 : 0);

            // Build progress bar with half-cell precision
            // For countdown, trailing edge uses left-half character
            var filledBar = new string(filledChar, filledWidth);
            var halfPart = hasHalfCell ? leftHalfChar.ToString() : "";
            var emptyBar = new string(' ', emptyWidth);

            context.SetCursorPosition(x, currentY);
            var progressColor = theme.Get(NotificationCardTheme.ProgressBarColor);
            var progressFgAnsi = progressColor.ToForegroundAnsi();
            context.Write($"{progressFgAnsi}{bgAnsi}{filledBar}{halfPart}{emptyBar}{resetCodes}");
        }
    }

    private double CalculateTimeoutProgress()
    {
        if (Notification?.Timeout == null) return 0;

        var elapsed = DateTimeOffset.Now - Notification.CreatedAt;
        var remaining = Notification.Timeout.Value - elapsed;
        
        if (remaining <= TimeSpan.Zero) return 0;
        
        return remaining.TotalMilliseconds / Notification.Timeout.Value.TotalMilliseconds;
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
