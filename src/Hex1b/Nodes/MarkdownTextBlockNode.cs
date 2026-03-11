using Hex1b.Layout;
using Hex1b.Markdown;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="MarkdownTextBlockWidget"/>. Measures and renders
/// inline markdown content as ANSI-styled, word-wrapped text.
/// </summary>
internal sealed class MarkdownTextBlockNode : Hex1bNode
{
    private List<string>? _wrappedLines;
    private int _lastWrapWidth = -1;
    private IReadOnlyList<MarkdownInline>? _lastInlines;
    private Hex1bColor? _lastBaseForeground;
    private CellAttributes _lastBaseAttributes;

    /// <summary>
    /// The inline AST elements to render.
    /// </summary>
    public IReadOnlyList<MarkdownInline> Inlines { get; set; } = [];

    /// <summary>
    /// Optional base foreground color.
    /// </summary>
    public Hex1bColor? BaseForeground { get; set; }

    /// <summary>
    /// Optional base attributes.
    /// </summary>
    public CellAttributes BaseAttributes { get; set; }

    public override bool IsFocusable => false;

    protected override Size MeasureCore(Constraints constraints)
    {
        var maxWidth = constraints.MaxWidth;
        if (maxWidth <= 0)
            return constraints.Constrain(new Size(0, 0));

        var lines = GetWrappedLines(maxWidth);
        var height = lines.Count;

        // Width is the max display width of any line
        var width = 0;
        foreach (var line in lines)
        {
            var lineWidth = DisplayWidth.GetStringWidth(line);
            if (lineWidth > width)
                width = lineWidth;
        }

        return constraints.Constrain(new Size(width, height));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var lines = GetWrappedLines(Bounds.Width);

        for (var i = 0; i < lines.Count && i < Bounds.Height; i++)
        {
            context.WriteClipped(Bounds.X, Bounds.Y + i, lines[i]);
        }
    }

    private List<string> GetWrappedLines(int maxWidth)
    {
        // Return cached lines if nothing changed
        if (_wrappedLines != null
            && _lastWrapWidth == maxWidth
            && ReferenceEquals(_lastInlines, Inlines)
            && ColorsEqual(_lastBaseForeground, BaseForeground)
            && _lastBaseAttributes == BaseAttributes)
        {
            return _wrappedLines;
        }

        _wrappedLines = MarkdownInlineRenderer.RenderLines(
            Inlines, maxWidth, BaseForeground, BaseAttributes);
        _lastWrapWidth = maxWidth;
        _lastInlines = Inlines;
        _lastBaseForeground = BaseForeground;
        _lastBaseAttributes = BaseAttributes;

        return _wrappedLines;
    }

    private static bool ColorsEqual(Hex1bColor? a, Hex1bColor? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.Value.IsDefault == b.Value.IsDefault
            && a.Value.R == b.Value.R
            && a.Value.G == b.Value.G
            && a.Value.B == b.Value.B;
    }
}
