using Hex1b.Charts;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Internal node that renders a single span's timeline bar with sub-cell precision.
/// </summary>
internal sealed class TraceTimelineSpanNode : Hex1bNode
{
    public double StartFraction { get; set; }
    public double DurationFraction { get; set; }
    public double? InnerDurationFraction { get; set; }
    public TraceSpanStatus Status { get; set; }
    public string? DurationLabel { get; set; }
    public int DurationLabelWidth { get; set; }

    protected override Size MeasureCore(Constraints constraints)
    {
        // Height is always 1 row; width fills available space
        return constraints.Constrain(new Size(constraints.MaxWidth, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var blocks = theme.Get(TraceTimelineTheme.BarBlockStyle);
        var laneColor = theme.Get(TraceTimelineTheme.LaneColor);
        var durationLabelColor = theme.Get(TraceTimelineTheme.DurationLabelColor);
        var resetCodes = theme.GetResetToGlobalCodes();

        var barColor = Status switch
        {
            TraceSpanStatus.Error => theme.Get(TraceTimelineTheme.ErrorBarColor),
            TraceSpanStatus.Unset => theme.Get(TraceTimelineTheme.UnsetBarColor),
            _ => theme.Get(TraceTimelineTheme.OkBarColor),
        };

        // Reserve space for duration label at the end (fixed width for alignment)
        var labelText = DurationLabel ?? "";
        var fixedLabelWidth = DurationLabelWidth > 0 ? DurationLabelWidth : labelText.Length;
        var labelColumnWidth = fixedLabelWidth > 0 ? fixedLabelWidth + 1 : 0; // +1 for space before label
        var barWidth = Math.Max(1, Bounds.Width - labelColumnWidth);

        if (barWidth < 1)
        {
            return;
        }

        var line = new System.Text.StringBuilder();
        var x = Bounds.X;
        var y = Bounds.Y;

        // Compute positions in sub-cell units (8 units per cell)
        var totalUnits = barWidth * 8;
        var startUnits = Math.Max(0, Math.Min(totalUnits - 1, (int)Math.Floor(StartFraction * totalUnits)));
        var endUnits = Math.Max(startUnits + 1, Math.Min(totalUnits, (int)Math.Ceiling((StartFraction + DurationFraction) * totalUnits)));

        var startCell = startUnits / 8;
        var endCell = (endUnits - 1) / 8;
        var startOffset = startUnits % 8;
        var endOffset = endUnits % 8;

        // Leading gap (before bar starts) — lane color
        if (startCell > 0)
        {
            line.Append(laneColor.ToForegroundAnsi());
            line.Append(laneColor.ToBackgroundAnsi());
            line.Append(new string(' ', startCell));
            line.Append(resetCodes);
        }

        // Render the bar
        var barFg = barColor.ToForegroundAnsi();
        var barBg = barColor.ToBackgroundAnsi();
        var laneFg = laneColor.ToForegroundAnsi();
        var laneBg = laneColor.ToBackgroundAnsi();

        if (startCell == endCell)
        {
            // Bar fits in a single cell
            var singleCellUnits = Math.Max(1, endUnits - startUnits);
            if (startOffset == 0)
            {
                // Bar starts at cell edge — use bar fg on default bg
                line.Append(barFg);
                line.Append(GetBlock(blocks, singleCellUnits));
                line.Append(resetCodes);
            }
            else
            {
                // Bar starts mid-cell — lane fg on bar bg
                line.Append(laneFg);
                line.Append(barBg);
                line.Append(GetBlock(blocks, startOffset));
                line.Append(resetCodes);
            }
        }
        else
        {
            // Leading partial cell
            if (startOffset > 0)
            {
                line.Append(laneFg);
                line.Append(barBg);
                line.Append(GetBlock(blocks, startOffset));
                line.Append(resetCodes);
            }

            // Full cells
            var fullStartCell = startCell + (startOffset > 0 ? 1 : 0);
            var fullEndCell = endCell - (endOffset > 0 ? 1 : 0);
            var fullCells = Math.Max(0, fullEndCell - fullStartCell + 1);
            if (fullCells > 0)
            {
                line.Append(barFg);
                line.Append(new string(GetBlock(blocks, 8)[0], fullCells));
                line.Append(resetCodes);
            }

            // Trailing partial cell
            if (endOffset > 0)
            {
                line.Append(barFg);
                line.Append(GetBlock(blocks, endOffset));
                line.Append(resetCodes);
            }
        }

        // Trailing gap (after bar ends) — lane color background
        var trailingCells = Math.Max(0, barWidth - endCell - 1);
        if (trailingCells > 0)
        {
            line.Append(laneColor.ToForegroundAnsi());
            line.Append(laneColor.ToBackgroundAnsi());
            line.Append(new string(' ', trailingCells));
            line.Append(resetCodes);
        }

        // Duration label (right-aligned within fixed column)
        if (labelColumnWidth > 0)
        {
            line.Append(' ');
            line.Append(durationLabelColor.ToForegroundAnsi());
            if (labelText.Length < fixedLabelWidth)
            {
                line.Append(new string(' ', fixedLabelWidth - labelText.Length));
            }
            line.Append(labelText);
            line.Append(resetCodes);
        }

        // Write the line
        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(x, y, line.ToString());
        }
        else
        {
            context.SetCursorPosition(x, y);
            context.Write(line.ToString());
        }
    }

    /// <summary>
    /// Gets the block character for the given number of filled eighths (0-8).
    /// </summary>
    private static string GetBlock(IReadOnlyList<string> blocks, int eighths)
    {
        var index = Math.Clamp(eighths, 0, blocks.Count - 1);
        return blocks[index];
    }
}
