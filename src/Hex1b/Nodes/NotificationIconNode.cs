using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for NotificationIconWidget.
/// Displays a bell icon with notification count and toggles the panel on click.
/// </summary>
public sealed class NotificationIconNode : Hex1bNode
{
    /// <summary>
    /// The bell character to display.
    /// </summary>
    public string BellCharacter { get; set; } = "🔔";

    /// <summary>
    /// Whether to show the count badge.
    /// </summary>
    public bool ShowCount { get; set; } = true;

    private bool _isFocused;
    private bool _isHovered;

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

    public override bool IsFocusable => true;

    /// <summary>
    /// Finds the nearest notification host in the parent chain.
    /// </summary>
    private INotificationHost? FindNotificationHost()
    {
        Hex1bNode? current = Parent;
        while (current != null)
        {
            if (current is INotificationHost host)
            {
                return host;
            }
            current = current.Parent;
        }
        return null;
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        bindings.Key(Hex1bKey.Enter).Action(TogglePanel, "Toggle notifications");
        bindings.Key(Hex1bKey.Spacebar).Action(TogglePanel, "Toggle notifications");
        bindings.Mouse(MouseButton.Left).Action(TogglePanel, "Toggle notifications");
    }

    private Task TogglePanel(InputBindingActionContext ctx)
    {
        var host = FindNotificationHost();
        if (host != null)
        {
            host.Notifications.TogglePanel();
            MarkDirty();
        }
        return Task.CompletedTask;
    }

    public override Size Measure(Constraints constraints)
    {
        var host = FindNotificationHost();
        var count = host?.Notifications.Count ?? 0;
        
        // Bell + optional count: "🔔" or "🔔 3"
        var width = 2; // Bell character (2 cells for emoji)
        if (ShowCount && count > 0)
        {
            width += 1 + count.ToString().Length; // Space + count
        }
        
        return constraints.Constrain(new Size(width, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var host = FindNotificationHost();
        var count = host?.Notifications.Count ?? 0;
        var isPanelVisible = host?.Notifications.IsPanelVisible ?? false;

        var theme = context.Theme;
        
        // Use MenuBar theme colors for unified appearance with menu bar
        Hex1bColor fg, bg;
        if (_isFocused)
        {
            fg = theme.Get(MenuBarTheme.FocusedForegroundColor);
            bg = theme.Get(MenuBarTheme.FocusedBackgroundColor);
        }
        else if (_isHovered)
        {
            fg = theme.Get(MenuBarTheme.HoveredForegroundColor);
            bg = theme.Get(MenuBarTheme.HoveredBackgroundColor);
        }
        else if (isPanelVisible)
        {
            // Indicate panel is open with focused colors
            fg = theme.Get(MenuBarTheme.FocusedForegroundColor);
            bg = theme.Get(MenuBarTheme.FocusedBackgroundColor);
        }
        else
        {
            fg = theme.Get(MenuBarTheme.ForegroundColor);
            bg = theme.Get(MenuBarTheme.BackgroundColor);
        }

        var fgAnsi = fg.ToForegroundAnsi();
        var bgAnsi = bg.ToBackgroundAnsi();
        var reset = theme.GetResetToGlobalCodes();

        context.SetCursorPosition(Bounds.X, Bounds.Y);
        
        var text = BellCharacter;
        if (ShowCount && count > 0)
        {
            text += $" {count}";
        }
        
        context.Write($"{fgAnsi}{bgAnsi}{text}{reset}");
    }
}
