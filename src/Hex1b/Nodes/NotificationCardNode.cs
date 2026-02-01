using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="NotificationCardWidget"/>.
/// Displays a notification with title, optional body, action buttons, and timeout progress bar.
/// Uses inverted colors (swapped foreground/background) for visual distinction.
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
    /// Child nodes for the card content.
    /// </summary>
    public List<Hex1bNode> ChildNodes { get; set; } = [];

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

    public override bool IsFocusable => true;

    // Card dimensions
    private const int CardWidth = 40;
    private const int MinCardHeight = 2;

    /// <summary>
    /// Lower one-eighth block character for progress bar (thinner appearance).
    /// </summary>
    private const char ProgressBlock = '▁'; // U+2581

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Escape dismisses the notification
        bindings.Key(Hex1bKey.Escape).Action(DismissNotification, "Dismiss");
        
        // X key also dismisses
        bindings.Key(Hex1bKey.X).Action(DismissNotification, "Dismiss");

        // Enter triggers primary action if available
        if (PrimaryAction != null)
        {
            bindings.Key(Hex1bKey.Enter).Action(TriggerPrimaryAction, "Activate");
        }
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

    private async Task TriggerPrimaryAction(InputBindingActionContext ctx)
    {
        if (PrimaryAction != null && Notification != null && Stack != null)
        {
            var actionCtx = new NotificationActionContext(Notification, Stack, ctx.CancellationToken, ctx);
            await PrimaryAction.Handler(actionCtx);
        }
    }

    public override Size Measure(Constraints constraints)
    {
        // Calculate height based on content
        var height = 1; // Title row
        if (!string.IsNullOrEmpty(Body))
        {
            // Wrap body text to card width
            var bodyLines = WrapText(Body, CardWidth - 2); // -2 for padding
            height += bodyLines.Count;
        }
        if (PrimaryAction != null || SecondaryActions.Count > 0)
        {
            height += 1; // Action row
        }
        if (Notification?.Timeout != null)
        {
            height += 1; // Progress bar row (half-height, but still needs a row)
        }

        var size = new Size(CardWidth, Math.Max(MinCardHeight, height));
        return constraints.Constrain(size);
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        
        // Get colors from theme
        var bg = IsFocused 
            ? theme.Get(NotificationCardTheme.FocusedBackgroundColor)
            : theme.Get(NotificationCardTheme.BackgroundColor);
        var titleColor = theme.Get(NotificationCardTheme.TitleColor);
        var bodyColor = theme.Get(NotificationCardTheme.BodyColor);
        var actionColor = theme.Get(NotificationCardTheme.ActionColor);
        var dismissColor = theme.Get(NotificationCardTheme.DismissButtonColor);

        var bgAnsi = bg.ToBackgroundAnsi();
        var titleFgAnsi = titleColor.ToForegroundAnsi();
        var bodyFgAnsi = bodyColor.ToForegroundAnsi();
        var actionFgAnsi = actionColor.ToForegroundAnsi();
        var dismissFgAnsi = dismissColor.ToForegroundAnsi();
        var resetCodes = theme.GetResetToGlobalCodes();

        var x = Bounds.X;
        var y = Bounds.Y;
        var width = Bounds.Width;
        var height = Bounds.Height;

        var currentY = y;

        // Draw title row with dismiss button
        var dismissBtn = "[×]";
        var titleMaxWidth = width - 2 - dismissBtn.Length; // padding, dismiss
        var displayTitle = Title.Length > titleMaxWidth 
            ? Title[..(titleMaxWidth - 1)] + "…" 
            : Title;
        var titlePadding = width - 1 - displayTitle.Length - dismissBtn.Length;

        context.SetCursorPosition(x, currentY);
        context.Write($"{titleFgAnsi}{bgAnsi} {displayTitle}{new string(' ', Math.Max(0, titlePadding))}{dismissFgAnsi}{dismissBtn}{resetCodes}");
        currentY++;

        // Draw body if present
        if (!string.IsNullOrEmpty(Body))
        {
            var bodyLines = WrapText(Body, width - 2);
            foreach (var line in bodyLines)
            {
                if (currentY >= y + height - (Notification?.Timeout != null ? 1 : 0)) break;

                var paddedLine = line.PadRight(width - 2);
                context.SetCursorPosition(x, currentY);
                context.Write($"{bodyFgAnsi}{bgAnsi} {paddedLine} {resetCodes}");
                currentY++;
            }
        }

        // Draw action row if there are actions
        if (PrimaryAction != null || SecondaryActions.Count > 0)
        {
            if (currentY < y + height - (Notification?.Timeout != null ? 1 : 0))
            {
                var actionText = BuildActionText(width - 2);
                context.SetCursorPosition(x, currentY);
                context.Write($"{actionFgAnsi}{bgAnsi} {actionText} {resetCodes}");
                currentY++;
            }
        }

        // Fill remaining rows (except progress bar)
        var progressBarRow = Notification?.Timeout != null ? 1 : 0;
        while (currentY < y + height - progressBarRow)
        {
            context.SetCursorPosition(x, currentY);
            context.Write($"{bgAnsi}{new string(' ', width)}{resetCodes}");
            currentY++;
        }

        // Draw timeout progress bar if notification has a timeout
        if (Notification?.Timeout != null && currentY < y + height)
        {
            var progress = CalculateTimeoutProgress();
            var filledWidth = (int)(width * progress);
            var emptyWidth = width - filledWidth;

            // Use thin block for progress bar
            var filledBar = new string(ProgressBlock, filledWidth);
            var emptyBar = new string(' ', emptyWidth);

            context.SetCursorPosition(x, currentY);
            // Progress bar uses bright theme color for filled portion
            var progressColor = theme.Get(NotificationCardTheme.ProgressBarColor);
            var progressFgAnsi = progressColor.ToForegroundAnsi();
            context.Write($"{progressFgAnsi}{bgAnsi}{filledBar}{emptyBar}{resetCodes}");
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

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        yield return this;
    }

    public override IEnumerable<Hex1bNode> GetChildren() => ChildNodes;
}
