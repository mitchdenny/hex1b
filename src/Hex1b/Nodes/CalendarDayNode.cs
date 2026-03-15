using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for a calendar day cell. Adapts its display format based on
/// the arranged column width and applies theme colors from
/// <see cref="CalendarTheme"/> based on today/selected/focused state.
/// </summary>
internal sealed class CalendarDayNode : Hex1bNode
{
    public int Day { get; set; }
    public bool IsCurrentDay { get; set; }
    public bool IsSelected { get; set; }
    public bool IsCellFocused { get; set; }
    public bool IsCellHovered { get; set; }

    protected override Size MeasureCore(Constraints constraints)
    {
        // 2-char right-aligned number + 1 char padding
        return constraints.Constrain(new Size(3, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var dayStr = Day.ToString().PadLeft(2);

        var theme = context.Theme;

        // Color priority: Selected > Hover > CurrentDay > Default
        Hex1bColor fg, bg;

        if (IsSelected)
        {
            fg = theme.Get(CalendarTheme.SelectedForegroundColor);
            bg = theme.Get(CalendarTheme.SelectedBackgroundColor);
        }
        else if (IsCellHovered)
        {
            fg = theme.Get(CalendarTheme.HoverForegroundColor);
            bg = theme.Get(CalendarTheme.HoverBackgroundColor);
        }
        else if (IsCurrentDay)
        {
            fg = theme.Get(CalendarTheme.CurrentDayForegroundColor);
            bg = theme.Get(CalendarTheme.CurrentDayBackgroundColor);
        }
        else
        {
            fg = theme.Get(CalendarTheme.DayForegroundColor);
            bg = theme.Get(CalendarTheme.DayBackgroundColor);
        }

        var hasCustomColors = !fg.IsDefault || !bg.IsDefault;

        string label;
        if (hasCustomColors)
        {
            var fgCode = fg.IsDefault ? "" : fg.ToForegroundAnsi();
            var bgCode = bg.IsDefault ? "" : bg.ToBackgroundAnsi();
            label = $"{fgCode}{bgCode}{dayStr}{theme.GetResetToGlobalCodes()}";
        }
        else
        {
            label = dayStr;
        }

        var labelX = Bounds.X;

        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(labelX, Bounds.Y, label);
        }
        else
        {
            context.Write(label);
        }
    }
}
