using System.Globalization;
using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Nodes;

/// <summary>
/// Tracks the minimum column width across all calendar headers so they
/// can consistently select the same format level.
/// </summary>
internal sealed class HeaderColumnTracker
{
    public int MinWidth { get; private set; } = int.MaxValue;

    public void Report(int width)
    {
        if (width < MinWidth)
            MinWidth = width;
    }
}

/// <summary>
/// Render node for a calendar day-of-week header. Responsively selects the
/// label format (M → Mo → Mon → Monday) based on the narrowest column width
/// reported by all sibling headers via a shared <see cref="HeaderColumnTracker"/>.
/// </summary>
internal sealed class CalendarHeaderNode : Hex1bNode
{
    public DayOfWeek DayOfWeek { get; set; }
    public HeaderColumnTracker? ColumnTracker { get; set; }

    /// <summary>
    /// Cached format levels: each entry contains the 7 day labels at that level
    /// and the max display width across all 7. Ordered shortest to longest.
    /// </summary>
    private static (string[] Labels, int MaxWidth)[]? _cachedLevels;

    internal static (string[] Labels, int MaxWidth)[] GetFormatLevels()
    {
        if (_cachedLevels != null)
            return _cachedLevels;

        var dtf = CultureInfo.CurrentCulture.DateTimeFormat;

        var levels = new (string[] Labels, int MaxWidth)[4];

        // Level 0: First character of abbreviated name
        var singleChars = new string[7];
        for (int i = 0; i < 7; i++)
            singleChars[i] = dtf.AbbreviatedDayNames[i][..1];
        levels[0] = (singleChars, MaxDisplayWidth(singleChars));

        // Level 1: Shortest day names (e.g. "Mo", "Tu")
        var shortest = (string[])dtf.ShortestDayNames.Clone();
        levels[1] = (shortest, MaxDisplayWidth(shortest));

        // Level 2: Abbreviated (e.g. "Mon", "Tue")
        var abbreviated = (string[])dtf.AbbreviatedDayNames.Clone();
        levels[2] = (abbreviated, MaxDisplayWidth(abbreviated));

        // Level 3: Full names (e.g. "Monday", "Tuesday")
        var full = (string[])dtf.DayNames.Clone();
        levels[3] = (full, MaxDisplayWidth(full));

        _cachedLevels = levels;
        return levels;
    }

    private static int MaxDisplayWidth(string[] names)
    {
        var max = 0;
        foreach (var name in names)
            max = Math.Max(max, DisplayWidth.GetStringWidth(name));
        return max;
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        return constraints.Constrain(new Size(2, 1));
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);
        ColumnTracker?.Report(bounds.Width);
    }

    public override void Render(Hex1bRenderContext context)
    {
        var levels = GetFormatLevels();
        var width = ColumnTracker?.MinWidth ?? Bounds.Width;

        // Find highest level where the longest label fits in the narrowest column
        var bestLevel = 0;
        for (int i = levels.Length - 1; i >= 0; i--)
        {
            if (levels[i].MaxWidth + 1 <= width)
            {
                bestLevel = i;
                break;
            }
        }

        var label = levels[bestLevel].Labels[(int)DayOfWeek];
        var labelWidth = DisplayWidth.GetStringWidth(label);
        // Use MinWidth (not Bounds.Width) so all headers use the same offset,
        // even when Fill remainder makes some columns 1 char wider.
        var effectiveWidth = ColumnTracker?.MinWidth ?? Bounds.Width;
        var labelX = Bounds.X + Math.Max(0, (effectiveWidth - labelWidth) / 2);

        var theme = context.Theme;
        var fg = theme.Get(CalendarTheme.HeaderForegroundColor);

        string output;
        if (!fg.IsDefault)
        {
            output = $"{fg.ToForegroundAnsi()}{label}{theme.GetResetToGlobalCodes()}";
        }
        else
        {
            output = label;
        }

        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(labelX, Bounds.Y, output);
        }
        else
        {
            context.Write(output);
        }
    }
}
