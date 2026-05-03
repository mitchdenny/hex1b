using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="FigletTextWidget"/>. Caches the rendered FIGlet block keyed by the
/// inputs (text, font, layout, wrap width) and emits the result line-by-line during render.
/// </summary>
/// <remarks>
/// <para>
/// The node deliberately does not emit any ANSI color codes so that a wrapping
/// <c>EffectPanel</c> can colorize the surface cells produced by this node.
/// </para>
/// </remarks>
public sealed class FigletTextNode : Hex1bNode
{
    private string _text = string.Empty;
    private FigletFont _font = FigletFonts.Standard;
    private FigletLayoutMode _horizontalLayout = FigletLayoutMode.Default;
    private FigletLayoutMode _verticalLayout = FigletLayoutMode.Default;
    private FigletHorizontalOverflow _horizontalOverflow = FigletHorizontalOverflow.Clip;
    private FigletVerticalOverflow _verticalOverflow = FigletVerticalOverflow.Clip;

    private IReadOnlyList<string>? _cachedLines;
    private int _cachedMaxWidth;
    private int _cacheWrapWidth = -2;

    /// <summary>Gets or sets the text to render.</summary>
    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value ?? string.Empty;
                Invalidate();
            }
        }
    }

    /// <summary>Gets or sets the font used to render the text.</summary>
    public FigletFont Font
    {
        get => _font;
        set
        {
            if (!ReferenceEquals(_font, value))
            {
                _font = value ?? FigletFonts.Standard;
                Invalidate();
            }
        }
    }

    /// <summary>Gets or sets the horizontal layout mode.</summary>
    public FigletLayoutMode HorizontalLayout
    {
        get => _horizontalLayout;
        set
        {
            if (_horizontalLayout != value)
            {
                _horizontalLayout = value;
                Invalidate();
            }
        }
    }

    /// <summary>Gets or sets the vertical layout mode.</summary>
    public FigletLayoutMode VerticalLayout
    {
        get => _verticalLayout;
        set
        {
            if (_verticalLayout != value)
            {
                _verticalLayout = value;
                Invalidate();
            }
        }
    }

    /// <summary>Gets or sets the horizontal overflow behavior.</summary>
    public FigletHorizontalOverflow HorizontalOverflow
    {
        get => _horizontalOverflow;
        set
        {
            if (_horizontalOverflow != value)
            {
                _horizontalOverflow = value;
                Invalidate();
            }
        }
    }

    /// <summary>Gets or sets the vertical overflow behavior.</summary>
    public FigletVerticalOverflow VerticalOverflow
    {
        get => _verticalOverflow;
        set
        {
            if (_verticalOverflow != value)
            {
                _verticalOverflow = value;
                Invalidate();
            }
        }
    }

    private void Invalidate()
    {
        _cachedLines = null;
        _cacheWrapWidth = -2;
        _cachedMaxWidth = 0;
        MarkDirty();
    }

    /// <summary>
    /// Measures the FIGlet text within the supplied constraints. Width is the maximum display
    /// width of any rendered line; height is the total line count.
    /// </summary>
    protected override Size MeasureCore(Constraints constraints)
    {
        var lines = EnsureRendered(constraints.MaxWidth);
        var height = lines.Count;
        if (_verticalOverflow == FigletVerticalOverflow.Truncate
            && constraints.MaxHeight != int.MaxValue
            && constraints.MaxHeight > 0)
        {
            // Truncation is a render-time concern; measurement still reports natural size so the
            // parent can decide how much room to grant.
        }
        return constraints.Constrain(new Size(_cachedMaxWidth, height));
    }

    private IReadOnlyList<string> EnsureRendered(int maxWidth)
    {
        var wrapWidth = _horizontalOverflow == FigletHorizontalOverflow.Wrap
            ? (maxWidth == int.MaxValue ? int.MaxValue : Math.Max(1, maxWidth))
            : int.MaxValue;

        if (_cachedLines is not null && _cacheWrapWidth == wrapWidth)
        {
            return _cachedLines;
        }

        var lines = FigletRenderer.Render(
            _text,
            _font,
            _horizontalLayout,
            _verticalLayout,
            _horizontalOverflow,
            wrapWidth);

        var max = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var w = DisplayWidth.GetStringWidth(lines[i]);
            if (w > max) max = w;
        }

        _cachedLines = lines;
        _cachedMaxWidth = max;
        _cacheWrapWidth = wrapWidth;
        return lines;
    }

    /// <summary>Renders the cached FIGlet lines into <paramref name="context"/>.</summary>
    public override void Render(Hex1bRenderContext context)
    {
        var lines = _cachedLines;
        if (lines is null || lines.Count == 0)
        {
            return;
        }

        var availableHeight = Bounds.Height > 0 ? Bounds.Height : lines.Count;
        var maxLines = lines.Count;

        if (_verticalOverflow == FigletVerticalOverflow.Truncate)
        {
            var fontHeight = _font.Height;
            // Drop entire FIGlet rows that don't fully fit. A FIGlet row consists of `fontHeight`
            // sub-character rows (vertical fitting/smushing may have collapsed adjacent rows; we
            // treat the rendered output as a single contiguous block and round down to the
            // nearest fontHeight multiple ≤ availableHeight).
            if (fontHeight > 0)
            {
                var fittableRows = (availableHeight / fontHeight) * fontHeight;
                maxLines = Math.Min(lines.Count, fittableRows);
            }
            else
            {
                maxLines = Math.Min(lines.Count, availableHeight);
            }
        }
        else
        {
            maxLines = Math.Min(lines.Count, availableHeight);
        }

        for (var i = 0; i < maxLines; i++)
        {
            context.WriteClipped(Bounds.X, Bounds.Y + i, lines[i]);
        }
    }
}
