using Hex1b.Charts;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="BarChartWidget{T}"/>. Renders horizontal bars via the Surface API.
/// </summary>
public sealed class BarChartNode<T> : Hex1bNode
{
    private Size _measuredSize;

    // Reconciled properties (same as ColumnChartNode)
    public IReadOnlyList<T>? Data { get; set; }
    public Func<T, string>? LabelSelector { get; set; }
    public Func<T, double>? ValueSelector { get; set; }
    public IReadOnlyList<ChartSeriesDef<T>>? SeriesDefs { get; set; }
    public Func<T, string>? GroupBySelector { get; set; }
    public ChartLayout Mode { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public bool ShowValues { get; set; }
    public bool ShowGridLines { get; set; }
    public string? Title { get; set; }
    public Func<double, string>? ValueFormatter { get; set; }

    /// <inheritdoc />
    public override Size Measure(Constraints constraints)
    {
        var width = constraints.MaxWidth == int.MaxValue ? 40 : constraints.MaxWidth;
        var height = constraints.MaxHeight == int.MaxValue ? 15 : constraints.MaxHeight;
        _measuredSize = constraints.Constrain(new Size(width, height));
        return _measuredSize;
    }

    /// <inheritdoc />
    public override void Render(Hex1bRenderContext context)
    {
        if (Data is null || Data.Count == 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var width = Math.Min(Bounds.Width, 500);
        var height = Math.Min(Bounds.Height, 200);
        if (width <= 2 || height <= 2) return;

        var resolved = ResolveData();
        if (resolved.Categories.Count == 0) return;

        // Calculate layout regions
        var titleHeight = Title is not null ? 1 : 0;
        var labelWidth = resolved.Categories.Max(c => c.Label.Length) + 1;
        labelWidth = Math.Min(labelWidth, width / 3); // Cap at 1/3 of width
        var valueWidth = ShowValues ? 8 : 0;
        var barWidth = width - labelWidth - valueWidth;
        if (barWidth <= 0) return;

        var chartHeight = height - titleHeight;
        if (chartHeight <= 0) return;

        // For Stacked100 mode, normalize values to percentages
        if (Mode == ChartLayout.Stacked100)
            resolved = NormalizeToPercent(resolved);

        // Build the scaler
        ChartScaler scaler;
        if (Mode == ChartLayout.Stacked100)
        {
            scaler = new ChartScaler(0, 100, barWidth);
        }
        else if (Mode == ChartLayout.Stacked)
        {
            var stackedSums = resolved.Categories.Select(c => c.Values.Sum());
            scaler = ChartScaler.FromValues(stackedSums, barWidth, Minimum, Maximum);
        }
        else
        {
            scaler = ChartScaler.FromValues(resolved.Categories.SelectMany(c => c.Values), barWidth, Minimum, Maximum);
        }

        var seriesColors = ResolveSeriesColors(resolved.SeriesNames, context.Theme);

        CellMetrics cellMetrics = CellMetrics.Default;
        if (context is SurfaceRenderContext surfaceCtx)
            cellMetrics = surfaceCtx.CellMetrics;

        var surface = new Surface(width, height, cellMetrics);

        // Grid lines (vertical)
        if (ShowGridLines)
            DrawGridLines(surface, labelWidth, barWidth, titleHeight, chartHeight);

        // Bars
        DrawBars(surface, resolved, scaler, seriesColors, labelWidth, barWidth, titleHeight, chartHeight);

        // Labels and values
        DrawLabels(surface, resolved, scaler, seriesColors, labelWidth, barWidth, valueWidth, titleHeight, chartHeight, width);

        // Composite
        if (context is SurfaceRenderContext surfCtx)
        {
            var destX = Bounds.X - surfCtx.OffsetX;
            var destY = Bounds.Y - surfCtx.OffsetY;
            surfCtx.Surface.Composite(surface, destX, destY);
        }
    }

    /// <inheritdoc />
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        yield break;
    }

    #region Data Resolution (shared logic with ColumnChartNode)

    private ResolvedChartData ResolveData()
    {
        if (Data is null || LabelSelector is null)
            return new([], []);

        if (GroupBySelector is not null && ValueSelector is not null)
            return ResolveGroupByData();

        if (SeriesDefs is not null && SeriesDefs.Count > 0)
            return ResolveFlatMultiSeriesData();

        if (ValueSelector is not null)
            return ResolveSingleSeriesData();

        return new([], []);
    }

    private ResolvedChartData ResolveSingleSeriesData()
    {
        var categories = new List<ResolvedCategory>();
        foreach (var item in Data!)
        {
            var label = LabelSelector!(item);
            var value = ValueSelector!(item);
            categories.Add(new(label, [value]));
        }
        return new([""], categories);
    }

    private ResolvedChartData ResolveFlatMultiSeriesData()
    {
        var seriesNames = SeriesDefs!.Select(s => s.Name).ToList();
        var categories = new List<ResolvedCategory>();
        foreach (var item in Data!)
        {
            var label = LabelSelector!(item);
            var values = SeriesDefs!.Select(s => s.ValueSelector(item)).ToList();
            categories.Add(new(label, values));
        }
        return new(seriesNames, categories);
    }

    private ResolvedChartData ResolveGroupByData()
    {
        var grouped = new Dictionary<string, Dictionary<string, double>>();
        var allGroupKeys = new List<string>();

        foreach (var item in Data!)
        {
            var label = LabelSelector!(item);
            var groupKey = GroupBySelector!(item);
            var value = ValueSelector!(item);

            if (!grouped.TryGetValue(label, out var groups))
            {
                groups = new Dictionary<string, double>();
                grouped[label] = groups;
            }
            groups[groupKey] = value;

            if (!allGroupKeys.Contains(groupKey))
                allGroupKeys.Add(groupKey);
        }

        var categories = new List<ResolvedCategory>();
        foreach (var (label, groups) in grouped)
        {
            var values = allGroupKeys.Select(k => groups.GetValueOrDefault(k, 0.0)).ToList();
            categories.Add(new(label, values));
        }
        return new(allGroupKeys, categories);
    }

    #endregion

    private static ResolvedChartData NormalizeToPercent(ResolvedChartData data)
    {
        var categories = new List<ResolvedCategory>();
        foreach (var cat in data.Categories)
        {
            var sum = cat.Values.Sum();
            var normalized = sum > 0
                ? cat.Values.Select(v => v / sum * 100.0).ToList()
                : cat.Values.Select(_ => 0.0).ToList();
            categories.Add(new(cat.Label, normalized));
        }
        return new(data.SeriesNames, categories);
    }

    #region Drawing

    private void DrawGridLines(Surface surface, int labelWidth, int barWidth, int titleHeight, int chartHeight)
    {
        var gridLineCount = Math.Min(5, barWidth / 4);
        if (gridLineCount <= 0) return;

        var gridColor = Hex1bColor.FromRgb(80, 80, 80);

        for (int i = 0; i <= gridLineCount; i++)
        {
            var fraction = (double)i / gridLineCount;
            var x = labelWidth + (int)(fraction * (barWidth - 1));

            for (int y = titleHeight; y < titleHeight + chartHeight && y < surface.Height; y++)
            {
                surface[x, y] = new SurfaceCell("·", gridColor, null);
            }
        }
    }

    private void DrawBars(
        Surface surface,
        ResolvedChartData data,
        ChartScaler scaler,
        Hex1bColor[] seriesColors,
        int labelWidth,
        int barWidth,
        int titleHeight,
        int chartHeight)
    {
        var categoryCount = data.Categories.Count;
        var seriesCount = data.SeriesNames.Count;
        if (categoryCount == 0) return;

        // Calculate row layout — bars can be multiple rows tall
        int rowsPerSeries;
        int rowsPerCategory;
        switch (Mode)
        {
            case ChartLayout.Grouped:
                // Allocate space for N sub-bars per category
                var totalGroupedSlots = categoryCount * seriesCount;
                rowsPerSeries = Math.Max(1, (chartHeight - (categoryCount - 1)) / totalGroupedSlots);
                rowsPerCategory = rowsPerSeries * seriesCount;
                break;
            default:
                rowsPerSeries = Math.Max(1, (chartHeight - (categoryCount - 1)) / categoryCount);
                rowsPerCategory = rowsPerSeries;
                break;
        }

        var spacing = 1;
        var totalRows = categoryCount * rowsPerCategory + (categoryCount - 1) * spacing;
        var startY = titleHeight;

        if (totalRows > chartHeight)
        {
            spacing = 0;
            totalRows = categoryCount * rowsPerCategory;
        }

        // Use fractional height when bars don't divide evenly
        double exactBarHeight = (double)(chartHeight - Math.Max(0, categoryCount - 1) * spacing) / categoryCount;
        if (Mode == ChartLayout.Grouped)
            exactBarHeight /= seriesCount;

        for (int catIdx = 0; catIdx < categoryCount; catIdx++)
        {
            var category = data.Categories[catIdx];
            var catY = startY + catIdx * (rowsPerCategory + spacing);
            if (catY >= startY + chartHeight) break;

            switch (Mode)
            {
                case ChartLayout.Simple:
                    DrawThickBar(surface, category.Values[0], scaler, seriesColors[0],
                        labelWidth, catY, barWidth, rowsPerSeries, exactBarHeight);
                    break;

                case ChartLayout.Stacked:
                case ChartLayout.Stacked100:
                    DrawThickStackedBar(surface, category.Values, scaler, seriesColors,
                        labelWidth, catY, barWidth, rowsPerSeries, exactBarHeight);
                    break;

                case ChartLayout.Grouped:
                    for (int si = 0; si < category.Values.Count && si < seriesCount; si++)
                    {
                        var rowY = catY + si * rowsPerSeries;
                        if (rowY >= startY + chartHeight) break;
                        DrawThickBar(surface, category.Values[si], scaler, seriesColors[si % seriesColors.Length],
                            labelWidth, rowY, barWidth, rowsPerSeries, exactBarHeight);
                    }
                    break;
            }
        }
    }

    private static void DrawThickBar(
        Surface surface, double value, ChartScaler scaler, Hex1bColor color,
        int startX, int topY, int maxWidth, int barRows, double exactHeight)
    {
        var scaledWidth = scaler.Scale(value);
        var (wholeCells, remainder) = FractionalBlocks.Decompose(scaledWidth);

        // Only draw rows that fall within the exact bar height
        var wholeBarRows = (int)exactHeight;
        var barFrac = exactHeight - wholeBarRows;

        for (int row = 0; row < barRows; row++)
        {
            var y = topY + row;
            if (y >= surface.Height) break;

            // Skip rows beyond the exact height (avoid black stripe artifacts)
            if (row > wholeBarRows) break;

            if (row == wholeBarRows && barFrac > 0.05)
            {
                // Fractional bottom edge — partial height row using upper block
                var blockChar = FractionalBlocks.Vertical(1.0 - barFrac);
                for (int col = 0; col < wholeCells && col < maxWidth; col++)
                {
                    var x = startX + col;
                    if (x < surface.Width)
                        surface[x, y] = new SurfaceCell(blockChar, color, null);
                }
                if (remainder > 0.05 && wholeCells < maxWidth)
                {
                    var x = startX + wholeCells;
                    if (x < surface.Width)
                        surface[x, y] = new SurfaceCell(FractionalBlocks.Horizontal(remainder), color, null);
                }
            }
            else
            {
                // Full row
                for (int col = 0; col < wholeCells && col < maxWidth; col++)
                {
                    var x = startX + col;
                    if (x < surface.Width)
                        surface[x, y] = new SurfaceCell("█", color, null);
                }
                if (remainder > 0.05 && wholeCells < maxWidth)
                {
                    var x = startX + wholeCells;
                    if (x < surface.Width)
                        surface[x, y] = new SurfaceCell(FractionalBlocks.Horizontal(remainder), color, null);
                }
            }
        }
    }

    private static void DrawThickStackedBar(
        Surface surface, IReadOnlyList<double> values, ChartScaler scaler, Hex1bColor[] colors,
        int startX, int topY, int maxWidth, int barRows, double exactHeight)
    {
        // Pre-compute segments
        var segments = new List<(double left, double right, Hex1bColor color)>();
        double cumulativeValue = scaler.Minimum;
        for (int si = 0; si < values.Count; si++)
        {
            var segmentValue = values[si];
            if (segmentValue <= 0) continue;

            var leftScaled = scaler.Scale(cumulativeValue);
            cumulativeValue += segmentValue;
            var rightScaled = scaler.Scale(cumulativeValue);
            segments.Add((leftScaled, rightScaled, colors[si % colors.Length]));
        }

        for (int row = 0; row < barRows; row++)
        {
            var y = topY + row;
            if (y >= surface.Height) break;

            for (int segIdx = 0; segIdx < segments.Count; segIdx++)
            {
                var (leftScaled, rightScaled, color) = segments[segIdx];
                var leftCol = (int)leftScaled;
                var rightCol = (int)rightScaled;

                for (int col = leftCol; col <= rightCol && col < maxWidth; col++)
                {
                    var x = startX + col;
                    if (x >= surface.Width) break;

                    if (col == leftCol && col == rightCol)
                    {
                        var frac = rightScaled - leftScaled;
                        if (frac < 0.05) continue;
                        Hex1bColor? prevColor = segIdx > 0 ? segments[segIdx - 1].color : null;
                        var blockChar = FractionalBlocks.Horizontal(Math.Min(1.0, (leftScaled - leftCol) + frac));
                        surface[x, y] = new SurfaceCell(blockChar, color, prevColor);
                    }
                    else if (col == rightCol)
                    {
                        var rightFrac = rightScaled - rightCol;
                        if (rightFrac < 0.05) continue;
                        Hex1bColor? nextColor = segIdx < segments.Count - 1 ? segments[segIdx + 1].color : null;
                        surface[x, y] = new SurfaceCell(FractionalBlocks.Horizontal(rightFrac), color, nextColor);
                    }
                    else
                    {
                        surface[x, y] = new SurfaceCell("█", color, null);
                    }
                }
            }
        }
    }

    private void DrawLabels(
        Surface surface,
        ResolvedChartData data,
        ChartScaler scaler,
        Hex1bColor[] seriesColors,
        int labelWidth,
        int barWidth,
        int valueWidth,
        int titleHeight,
        int chartHeight,
        int totalWidth)
    {
        var labelColor = Hex1bColor.FromRgb(200, 200, 200);
        var valueColor = Hex1bColor.FromRgb(180, 180, 180);

        // Title
        if (Title is not null && titleHeight > 0)
        {
            var titleX = Math.Max(0, (totalWidth - Title.Length) / 2);
            WriteText(surface, titleX, 0, Title, labelColor);
        }

        // Category labels (left column) and values (right)
        var categoryCount = data.Categories.Count;
        var seriesCount = data.SeriesNames.Count;

        // Match the layout from DrawBars
        int rowsPerSeries;
        int rowsPerCategory;
        if (Mode == ChartLayout.Grouped)
        {
            var totalGroupedSlots = categoryCount * seriesCount;
            rowsPerSeries = Math.Max(1, (chartHeight - (categoryCount - 1)) / totalGroupedSlots);
            rowsPerCategory = rowsPerSeries * seriesCount;
        }
        else
        {
            rowsPerSeries = Math.Max(1, (chartHeight - (categoryCount - 1)) / categoryCount);
            rowsPerCategory = rowsPerSeries;
        }

        var spacing = 1;
        var totalRows = categoryCount * rowsPerCategory + (categoryCount - 1) * spacing;
        if (totalRows > chartHeight) spacing = 0;

        var formatter = ValueFormatter ?? (v => v.ToString("G4"));

        for (int catIdx = 0; catIdx < categoryCount; catIdx++)
        {
            var catY = titleHeight + catIdx * (rowsPerCategory + spacing);
            if (catY >= titleHeight + chartHeight) break;

            // Label (left-aligned, truncated)
            var label = data.Categories[catIdx].Label;
            if (label.Length > labelWidth - 1)
                label = label[..(labelWidth - 1)];
            // Center label vertically within the bar's rows
            var labelY = catY + rowsPerCategory / 2;
            WriteText(surface, 0, labelY, label, labelColor);

            // Value (right of bar)
            if (ShowValues && valueWidth > 0)
            {
                double displayValue;
                if (Mode == ChartLayout.Stacked || Mode == ChartLayout.Stacked100)
                    displayValue = data.Categories[catIdx].Values.Sum();
                else if (Mode == ChartLayout.Simple)
                    displayValue = data.Categories[catIdx].Values[0];
                else
                    continue;

                var text = Mode == ChartLayout.Stacked100
                    ? "100%"
                    : formatter(displayValue);
                var valX = labelWidth + barWidth + 1;
                if (valX + text.Length <= totalWidth)
                    WriteText(surface, valX, labelY, text, valueColor);
            }
        }
    }

    private static void WriteText(Surface surface, int x, int y, string text, Hex1bColor color)
    {
        if (y < 0 || y >= surface.Height) return;
        for (int i = 0; i < text.Length && x + i < surface.Width; i++)
        {
            if (x + i < 0) continue;
            surface[x + i, y] = new SurfaceCell(text[i].ToString(), color, null);
        }
    }

    private static Hex1bColor[] ResolveSeriesColors(IReadOnlyList<string> seriesNames, Hex1bTheme theme)
    {
        Hex1bColor[] palette =
        [
            Hex1bColor.FromRgb(66, 133, 244),
            Hex1bColor.FromRgb(234, 67, 53),
            Hex1bColor.FromRgb(52, 168, 83),
            Hex1bColor.FromRgb(251, 188, 4),
            Hex1bColor.FromRgb(171, 71, 188),
            Hex1bColor.FromRgb(0, 172, 193),
            Hex1bColor.FromRgb(255, 112, 67),
            Hex1bColor.FromRgb(158, 157, 36),
        ];

        var colors = new Hex1bColor[Math.Max(1, seriesNames.Count)];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = palette[i % palette.Length];
        return colors;
    }

    #endregion

    private record ResolvedChartData(
        IReadOnlyList<string> SeriesNames,
        IReadOnlyList<ResolvedCategory> Categories);

    private record ResolvedCategory(
        string Label,
        IReadOnlyList<double> Values);
}
