using Hex1b.Charts;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="LegendWidget{T}"/>.
/// Renders a standalone legend with colored swatches and labels.
/// </summary>
public sealed class LegendNode<T> : Hex1bNode
{
    private const string SwatchChar = "■"; // U+25A0 Black Square

    private Size _measuredSize;

    public IReadOnlyList<T>? Data { get; set; }
    public Func<T, string>? LabelSelector { get; set; }
    public Func<T, double>? ValueSelector { get; set; }
    public bool ShowValues { get; set; }
    public bool ShowPercentages { get; set; }
    public bool IsHorizontal { get; set; }
    public Func<double, string>? ValueFormatter { get; set; }

    /// <inheritdoc />
    protected override Size MeasureCore(Constraints constraints)
    {
        var items = ResolveItems();
        if (items.Count == 0)
        {
            _measuredSize = constraints.Constrain(new Size(0, 0));
            return _measuredSize;
        }

        if (IsHorizontal)
        {
            var totalWidth = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) totalWidth += 2; // spacing between items
                totalWidth += MeasureItemWidth(items[i]);
            }

            var width = Math.Min(totalWidth, constraints.MaxWidth == int.MaxValue ? totalWidth : constraints.MaxWidth);
            _measuredSize = constraints.Constrain(new Size(width, 1));
        }
        else
        {
            var maxItemWidth = 0;
            foreach (var item in items)
            {
                var itemWidth = MeasureItemWidth(item);
                if (itemWidth > maxItemWidth)
                    maxItemWidth = itemWidth;
            }

            var width = constraints.MaxWidth == int.MaxValue ? maxItemWidth : Math.Min(maxItemWidth, constraints.MaxWidth);
            var height = Math.Min(items.Count, constraints.MaxHeight);
            _measuredSize = constraints.Constrain(new Size(width, height));
        }

        return _measuredSize;
    }

    /// <inheritdoc />
    public override void Render(Hex1bRenderContext context)
    {
        var items = ResolveItems();
        if (items.Count == 0) return;
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

        var width = Math.Min(Bounds.Width, 500);
        var height = Math.Min(Bounds.Height, 200);
        if (width <= 2) return;

        var colors = ResolveColors(items.Count);

        CellMetrics cellMetrics = CellMetrics.Default;
        if (context is SurfaceRenderContext surfaceCtx)
            cellMetrics = surfaceCtx.CellMetrics;

        var surface = new Surface(width, height, cellMetrics);

        if (IsHorizontal)
            RenderHorizontal(surface, items, colors, width);
        else
            RenderVertical(surface, items, colors, width, height);

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

    private void RenderVertical(Surface surface, List<LegendItem> items, Hex1bColor[] colors, int width, int height)
    {
        var labelColor = Hex1bColor.FromRgb(200, 200, 200);
        var dimColor = Hex1bColor.FromRgb(140, 140, 140);

        for (int i = 0; i < items.Count; i++)
        {
            var y = i;
            if (y >= height) break;

            var item = items[i];
            var color = colors[i % colors.Length];

            // Swatch
            if (0 < width)
                surface[0, y] = new SurfaceCell(SwatchChar, color, null);
            if (1 < width)
                surface[1, y] = new SurfaceCell(" ", null, null);

            // Label
            var x = 2;
            WriteText(surface, x, y, item.Label, labelColor);
            x += item.Label.Length;

            // Suffix (value / percentage)
            var suffix = FormatSuffix(item);
            if (suffix.Length > 0)
                WriteText(surface, x, y, suffix, dimColor);
        }
    }

    private void RenderHorizontal(Surface surface, List<LegendItem> items, Hex1bColor[] colors, int width)
    {
        var labelColor = Hex1bColor.FromRgb(200, 200, 200);
        var dimColor = Hex1bColor.FromRgb(140, 140, 140);

        var x = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (x >= width) break;

            if (i > 0)
            {
                x += 2; // spacing
                if (x >= width) break;
            }

            var item = items[i];
            var color = colors[i % colors.Length];

            // Swatch
            if (x < width)
                surface[x, 0] = new SurfaceCell(SwatchChar, color, null);
            x++;
            if (x < width)
                surface[x, 0] = new SurfaceCell(" ", null, null);
            x++;

            // Label
            for (int c = 0; c < item.Label.Length && x < width; c++)
            {
                surface[x, 0] = new SurfaceCell(item.Label[c].ToString(), labelColor, null);
                x++;
            }

            // Suffix
            var suffix = FormatSuffix(item);
            for (int c = 0; c < suffix.Length && x < width; c++)
            {
                surface[x, 0] = new SurfaceCell(suffix[c].ToString(), dimColor, null);
                x++;
            }
        }
    }

    private string FormatSuffix(LegendItem item)
    {
        if (!ShowValues && !ShowPercentages)
            return "";

        var fmt = ValueFormatter ?? ChartFormatters.FormatValue;

        if (ShowValues && ShowPercentages)
        {
            var pct = item.Total > 0 ? item.Value / item.Total * 100 : 0;
            return $" ({fmt(item.Value)}, {pct:F1}%)";
        }

        if (ShowValues)
            return $" ({fmt(item.Value)})";

        if (ShowPercentages)
        {
            var pct = item.Total > 0 ? item.Value / item.Total * 100 : 0;
            return $" ({pct:F1}%)";
        }

        return "";
    }

    private int MeasureItemWidth(LegendItem item)
    {
        // swatch(1) + space(1) + label + suffix
        return 2 + item.Label.Length + FormatSuffix(item).Length;
    }

    private List<LegendItem> ResolveItems()
    {
        if (Data is null || Data.Count == 0 || LabelSelector is null || ValueSelector is null)
            return [];

        var items = new List<LegendItem>();
        double total = 0;
        foreach (var d in Data)
        {
            var value = ValueSelector(d);
            if (value > 0)
                total += value;
        }

        foreach (var d in Data)
        {
            var label = LabelSelector(d);
            var value = ValueSelector(d);
            if (value > 0)
                items.Add(new(label, value, total));
        }

        return items;
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

    private static Hex1bColor[] ResolveColors(int count)
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

        var colors = new Hex1bColor[count];
        for (int i = 0; i < count; i++)
            colors[i] = palette[i % palette.Length];
        return colors;
    }

    private record LegendItem(string Label, double Value, double Total);
}
