using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="NotificationCardWidget"/>.
/// Displays a notification with title, optional body, and action buttons.
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
    private const int MinCardHeight = 3;

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
        var height = 2; // Top and bottom borders
        height += 1; // Title row
        if (!string.IsNullOrEmpty(Body))
        {
            // Wrap body text to card width
            var bodyLines = WrapText(Body, CardWidth - 4); // -4 for borders and padding
            height += bodyLines.Count;
        }
        if (PrimaryAction != null || SecondaryActions.Count > 0)
        {
            height += 1; // Action row
        }

        var size = new Size(CardWidth, Math.Max(MinCardHeight, height));
        return constraints.Constrain(size);
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var fg = theme.GetGlobalForeground();
        var bg = theme.GetGlobalBackground();
        var resetCodes = theme.GetResetToGlobalCodes();

        // Use focused colors if focused
        if (IsFocused)
        {
            fg = theme.Get(ButtonTheme.FocusedForegroundColor);
            bg = theme.Get(ButtonTheme.FocusedBackgroundColor);
        }

        var fgAnsi = fg.ToForegroundAnsi();
        var bgAnsi = bg.ToBackgroundAnsi();

        var x = Bounds.X;
        var y = Bounds.Y;
        var width = Bounds.Width;
        var height = Bounds.Height;

        // Draw top border
        context.SetCursorPosition(x, y);
        context.Write($"{fgAnsi}{bgAnsi}┌{new string('─', width - 2)}┐{resetCodes}");

        // Draw title row with dismiss button
        var dismissBtn = "[×]";
        var titleMaxWidth = width - 4 - dismissBtn.Length; // borders, padding, dismiss
        var displayTitle = Title.Length > titleMaxWidth 
            ? Title[..(titleMaxWidth - 1)] + "…" 
            : Title;
        var titlePadding = width - 4 - displayTitle.Length - dismissBtn.Length;

        context.SetCursorPosition(x, y + 1);
        context.Write($"{fgAnsi}{bgAnsi}│ {displayTitle}{new string(' ', Math.Max(0, titlePadding))}{dismissBtn} │{resetCodes}");

        var currentY = y + 2;

        // Draw body if present
        if (!string.IsNullOrEmpty(Body))
        {
            var bodyLines = WrapText(Body, width - 4);
            foreach (var line in bodyLines)
            {
                if (currentY >= y + height - 1) break; // Leave room for bottom border

                var paddedLine = line.PadRight(width - 4);
                context.SetCursorPosition(x, currentY);
                context.Write($"{fgAnsi}{bgAnsi}│ {paddedLine} │{resetCodes}");
                currentY++;
            }
        }

        // Draw action row if there are actions
        if (PrimaryAction != null || SecondaryActions.Count > 0)
        {
            if (currentY < y + height - 1)
            {
                var actionText = BuildActionText(width - 4);
                context.SetCursorPosition(x, currentY);
                context.Write($"{fgAnsi}{bgAnsi}│ {actionText} │{resetCodes}");
                currentY++;
            }
        }

        // Fill remaining rows
        while (currentY < y + height - 1)
        {
            context.SetCursorPosition(x, currentY);
            context.Write($"{fgAnsi}{bgAnsi}│{new string(' ', width - 2)}│{resetCodes}");
            currentY++;
        }

        // Draw bottom border
        context.SetCursorPosition(x, y + height - 1);
        context.Write($"{fgAnsi}{bgAnsi}└{new string('─', width - 2)}┘{resetCodes}");
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
