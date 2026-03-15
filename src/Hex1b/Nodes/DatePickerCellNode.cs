using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for a date picker grid cell (year or month label).
/// Applies theme colors from <see cref="DatePickerTheme"/> based on
/// selected/hovered state.
/// </summary>
internal sealed class DatePickerCellNode : Hex1bNode
{
    public string Label { get; set; } = "";
    public bool IsSelected { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsCellFocused { get; set; }
    public bool IsCellHovered { get; set; }

    protected override Size MeasureCore(Constraints constraints)
    {
        var width = DisplayWidth.GetStringWidth(Label) + 2; // 1 char padding each side
        return constraints.Constrain(new Size(width, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;

        Hex1bColor fg, bg;

        if (IsSelected)
        {
            fg = theme.Get(DatePickerTheme.SelectedCellForegroundColor);
            bg = theme.Get(DatePickerTheme.SelectedCellBackgroundColor);
        }
        else if (IsCellHovered || IsCellFocused)
        {
            fg = theme.Get(DatePickerTheme.CellForegroundColor);
            bg = theme.Get(DatePickerTheme.HoverBackgroundColor);
        }
        else if (IsCurrent)
        {
            fg = theme.Get(DatePickerTheme.CurrentCellForegroundColor);
            bg = theme.Get(DatePickerTheme.CurrentCellBackgroundColor);
        }
        else
        {
            fg = theme.Get(DatePickerTheme.CellForegroundColor);
            bg = theme.Get(DatePickerTheme.CellBackgroundColor);
        }

        var hasCustomColors = !fg.IsDefault || !bg.IsDefault;

        // Center label with 1-char padding
        var padded = $" {Label} ";

        string output;
        if (hasCustomColors)
        {
            var fgCode = fg.IsDefault ? "" : fg.ToForegroundAnsi();
            var bgCode = bg.IsDefault ? "" : bg.ToBackgroundAnsi();
            output = $"{fgCode}{bgCode}{padded}{theme.GetResetToGlobalCodes()}";
        }
        else
        {
            output = padded;
        }

        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output);
        }
        else
        {
            context.Write(output);
        }
    }
}
