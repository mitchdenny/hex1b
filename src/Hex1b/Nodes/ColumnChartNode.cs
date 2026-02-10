using Hex1b.Charts;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="ColumnChartWidget{T}"/>. Renders vertical columns via the Surface API.
/// </summary>
public sealed class ColumnChartNode<T> : Hex1bNode
{
    private Size _measuredSize;

    // Reconciled properties
    public IReadOnlyList<T>? Data { get; set; }
    public Func<T, string>? LabelSelector { get; set; }
    public Func<T, double>? ValueSelector { get; set; }
    public IReadOnlyList<ChartSeriesDef<T>>? SeriesDefs { get; set; }
    public Func<T, string>? GroupBySelector { get; set; }
    public ChartMode Mode { get; set; }
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

        // Resolve chart data into a normalized form
        var resolved = ResolveData();
        if (resolved.Categories.Count == 0) return;

        // Calculate layout regions
        var titleHeight = Title is not null ? 1 : 0;
        var labelHeight = 1; // Bottom row for category labels
        var valueHeight = ShowValues ? 1 : 0;
        var chartHeight = height - titleHeight - labelHeight - valueHeight;
        if (chartHeight <= 0) return;

        // For Stacked100 mode, normalize values to percentages
        if (Mode == ChartMode.Stacked100)
            resolved = NormalizeToPercent(resolved);

        // Build the scaler
        ChartScaler scaler;
        if (Mode == ChartMode.Stacked100)
        {
            scaler = new ChartScaler(0, 100, chartHeight);
        }
        else if (Mode == ChartMode.Stacked)
        {
            var stackedSums = resolved.Categories.Select(c => c.Values.Sum());
            scaler = ChartScaler.FromValues(stackedSums, chartHeight, Minimum, Maximum);
        }
        else
        {
            scaler = ChartScaler.FromValues(resolved.Categories.SelectMany(c => c.Values), chartHeight, Minimum, Maximum);
        }

        // Get theme colors for series
        var seriesColors = ResolveSeriesColors(resolved.SeriesNames, context.Theme);

        // Create surface and draw layers
        CellMetrics cellMetrics = CellMetrics.Default;
        if (context is SurfaceRenderContext surfaceCtx)
            cellMetrics = surfaceCtx.CellMetrics;

        var surface = new Surface(width, height, cellMetrics);

        // Layer 1: Grid lines
        if (ShowGridLines)
            DrawGridLines(surface, titleHeight + valueHeight, chartHeight, width, scaler);

        // Layer 2: Columns
        DrawColumns(surface, resolved, scaler, seriesColors, titleHeight + valueHeight, chartHeight, width);

        // Layer 3: Labels and values
        DrawLabels(surface, resolved, scaler, seriesColors, titleHeight, valueHeight, chartHeight, width, height);

        // Composite to render context
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

    #region Data Resolution

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
        // Group data by label, then by group key
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

    /// <summary>
    /// Normalizes category values to percentages of each category's total.
    /// </summary>
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

    private void DrawGridLines(Surface surface, int topOffset, int chartHeight, int width, ChartScaler scaler)
    {
        // Draw ~5 horizontal grid lines
        var gridLineCount = Math.Min(5, chartHeight);
        if (gridLineCount <= 0) return;

        var gridColor = Hex1bColor.FromRgb(80, 80, 80);

        for (int i = 0; i <= gridLineCount; i++)
        {
            var fraction = (double)i / gridLineCount;
            var y = topOffset + chartHeight - 1 - (int)(fraction * (chartHeight - 1));

            for (int x = 0; x < width; x++)
            {
                surface[x, y] = new SurfaceCell("·", gridColor, null);
            }
        }
    }

    private void DrawColumns(
        Surface surface,
        ResolvedChartData data,
        ChartScaler scaler,
        Hex1bColor[] seriesColors,
        int topOffset,
        int chartHeight,
        int totalWidth)
    {
        var categoryCount = data.Categories.Count;
        var seriesCount = data.SeriesNames.Count;
        if (categoryCount == 0) return;

        // Calculate column layout
        var spacing = 1; // 1-cell gap between categories
        var totalSpacing = (categoryCount - 1) * spacing;
        var availableForColumns = totalWidth - totalSpacing;
        if (availableForColumns <= 0) return;

        var categoryWidth = Math.Max(1, availableForColumns / categoryCount);

        for (int catIdx = 0; catIdx < categoryCount; catIdx++)
        {
            var category = data.Categories[catIdx];
            var catX = catIdx * (categoryWidth + spacing);

            switch (Mode)
            {
                case ChartMode.Simple:
                    DrawSimpleColumn(surface, category.Values[0], scaler, seriesColors[0],
                        catX, categoryWidth, topOffset, chartHeight);
                    break;

                case ChartMode.Stacked:
                case ChartMode.Stacked100:
                    DrawStackedColumns(surface, category.Values, scaler, seriesColors,
                        catX, categoryWidth, topOffset, chartHeight);
                    break;

                case ChartMode.Grouped:
                    DrawGroupedColumns(surface, category.Values, scaler, seriesColors,
                        catX, categoryWidth, topOffset, chartHeight, seriesCount);
                    break;
            }
        }
    }

    private static void DrawSimpleColumn(
        Surface surface, double value, ChartScaler scaler, Hex1bColor color,
        int x, int columnWidth, int topOffset, int chartHeight)
    {
        var scaledHeight = scaler.Scale(value);
        var (wholeCells, remainder) = FractionalBlocks.Decompose(scaledHeight);

        // Draw full cells from bottom up
        for (int row = 0; row < wholeCells && row < chartHeight; row++)
        {
            var y = topOffset + chartHeight - 1 - row;
            for (int col = 0; col < columnWidth && x + col < surface.Width; col++)
            {
                surface[x + col, y] = new SurfaceCell("█", color, null);
            }
        }

        // Draw fractional top cell
        if (remainder > 0.05 && wholeCells < chartHeight)
        {
            var y = topOffset + chartHeight - 1 - wholeCells;
            var blockChar = FractionalBlocks.Vertical(remainder);
            for (int col = 0; col < columnWidth && x + col < surface.Width; col++)
            {
                surface[x + col, y] = new SurfaceCell(blockChar, color, null);
            }
        }
    }

    private static void DrawStackedColumns(
        Surface surface, IReadOnlyList<double> values, ChartScaler scaler, Hex1bColor[] colors,
        int x, int columnWidth, int topOffset, int chartHeight)
    {
        // Stack values: each segment starts where the previous one ended
        double cumulativeValue = scaler.Minimum;
        for (int si = 0; si < values.Count; si++)
        {
            var segmentValue = values[si];
            if (segmentValue <= 0) continue;

            var bottomScaled = scaler.Scale(cumulativeValue);
            cumulativeValue += segmentValue;
            var topScaled = scaler.Scale(cumulativeValue);

            var bottomRow = (int)bottomScaled;
            var topRow = (int)topScaled;
            var color = colors[si % colors.Length];

            for (int row = bottomRow; row <= topRow && row < chartHeight; row++)
            {
                var y = topOffset + chartHeight - 1 - row;
                if (y < topOffset || y >= topOffset + chartHeight) continue;

                var blockChar = "█";
                // Fractional top edge
                if (row == topRow)
                {
                    var frac = topScaled - topRow;
                    if (frac > 0.05 && frac < 0.95)
                        blockChar = FractionalBlocks.Vertical(frac);
                }
                // Fractional bottom edge
                if (row == bottomRow && si > 0)
                {
                    var frac = bottomScaled - bottomRow;
                    if (frac > 0.05)
                        continue; // Skip partial bottom cells, previous segment handles it
                }

                for (int col = 0; col < columnWidth && x + col < surface.Width; col++)
                {
                    surface[x + col, y] = new SurfaceCell(blockChar, color, null);
                }
            }
        }
    }

    private static void DrawGroupedColumns(
        Surface surface, IReadOnlyList<double> values, ChartScaler scaler, Hex1bColor[] colors,
        int catX, int categoryWidth, int topOffset, int chartHeight, int seriesCount)
    {
        if (seriesCount == 0) return;

        var subColumnWidth = Math.Max(1, categoryWidth / seriesCount);
        for (int si = 0; si < values.Count; si++)
        {
            var subX = catX + si * subColumnWidth;
            var color = colors[si % colors.Length];
            DrawSimpleColumn(surface, values[si], scaler, color, subX, subColumnWidth, topOffset, chartHeight);
        }
    }

    private void DrawLabels(
        Surface surface,
        ResolvedChartData data,
        ChartScaler scaler,
        Hex1bColor[] seriesColors,
        int titleHeight,
        int valueHeight,
        int chartHeight,
        int totalWidth,
        int totalHeight)
    {
        var labelColor = Hex1bColor.FromRgb(200, 200, 200);
        var valueColor = Hex1bColor.FromRgb(180, 180, 180);

        // Title
        if (Title is not null && titleHeight > 0)
        {
            var titleX = Math.Max(0, (totalWidth - Title.Length) / 2);
            WriteText(surface, titleX, 0, Title, labelColor);
        }

        // Category labels (bottom row)
        var categoryCount = data.Categories.Count;
        var spacing = 1;
        var totalSpacing = (categoryCount - 1) * spacing;
        var availableForColumns = totalWidth - totalSpacing;
        var categoryWidth = Math.Max(1, availableForColumns / categoryCount);
        var labelY = totalHeight - 1;

        for (int i = 0; i < categoryCount; i++)
        {
            var catX = i * (categoryWidth + spacing);
            var label = data.Categories[i].Label;
            // Truncate label to category width
            if (label.Length > categoryWidth)
                label = label[..categoryWidth];
            // Center label under column
            var labelX = catX + Math.Max(0, (categoryWidth - label.Length) / 2);
            WriteText(surface, labelX, labelY, label, labelColor);
        }

        // Value labels above columns
        if (ShowValues && valueHeight > 0)
        {
            var formatter = ValueFormatter ?? (v => v.ToString("G4"));
            var valuesY = titleHeight;

            for (int i = 0; i < categoryCount; i++)
            {
                var catX = i * (categoryWidth + spacing);
                double displayValue;

                if (Mode == ChartMode.Stacked || Mode == ChartMode.Stacked100)
                    displayValue = data.Categories[i].Values.Sum();
                else if (Mode == ChartMode.Simple)
                    displayValue = data.Categories[i].Values[0];
                else
                    continue; // Skip for grouped — too many values

                var text = Mode == ChartMode.Stacked100
                    ? "100%"
                    : formatter(displayValue);
                if (text.Length > categoryWidth)
                    text = text[..categoryWidth];
                var textX = catX + Math.Max(0, (categoryWidth - text.Length) / 2);
                WriteText(surface, textX, valuesY, text, valueColor);
            }
        }
    }

    private static void WriteText(Surface surface, int x, int y, string text, Hex1bColor color)
    {
        for (int i = 0; i < text.Length && x + i < surface.Width; i++)
        {
            if (x + i < 0) continue;
            surface[x + i, y] = new SurfaceCell(text[i].ToString(), color, null);
        }
    }

    private static Hex1bColor[] ResolveSeriesColors(IReadOnlyList<string> seriesNames, Hex1bTheme theme)
    {
        // Default chart palette
        Hex1bColor[] palette =
        [
            Hex1bColor.FromRgb(66, 133, 244),  // Blue
            Hex1bColor.FromRgb(234, 67, 53),   // Red
            Hex1bColor.FromRgb(52, 168, 83),   // Green
            Hex1bColor.FromRgb(251, 188, 4),   // Yellow
            Hex1bColor.FromRgb(171, 71, 188),  // Purple
            Hex1bColor.FromRgb(0, 172, 193),   // Cyan
            Hex1bColor.FromRgb(255, 112, 67),  // Orange
            Hex1bColor.FromRgb(158, 157, 36),  // Lime
        ];

        var colors = new Hex1bColor[Math.Max(1, seriesNames.Count)];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = palette[i % palette.Length];
        }
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
