using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Hex1b;
using Hex1b.Theming;

namespace WpfTerm;

/// <summary>
/// A WPF control that renders a terminal screen buffer using DrawingVisual.
/// </summary>
public class TerminalControl : FrameworkElement
{
    private readonly DrawingVisual _visual = new();
    private readonly VisualCollection _children;

    private WpfTerminalAdapter? _adapter;
    private double _cellWidth;
    private double _cellHeight;
    private Typeface _typeface = null!;
    private double _fontSize;
    private double _dpiScale = 1.0;

    // Cached brushes for ANSI colors
    private static readonly SolidColorBrush[] AnsiStandardBrushes =
    [
        B(0x00, 0x00, 0x00), // 0 Black
        B(0xCD, 0x31, 0x31), // 1 Red
        B(0x0D, 0xBC, 0x79), // 2 Green
        B(0xE5, 0xE5, 0x10), // 3 Yellow
        B(0x24, 0x72, 0xC8), // 4 Blue
        B(0xBC, 0x3F, 0xBC), // 5 Magenta
        B(0x11, 0xA8, 0xCD), // 6 Cyan
        B(0xE5, 0xE5, 0xE5), // 7 White
    ];

    private static readonly SolidColorBrush[] AnsiBrightBrushes =
    [
        B(0x66, 0x66, 0x66), // 8  Bright Black
        B(0xF1, 0x4C, 0x4C), // 9  Bright Red
        B(0x23, 0xD1, 0x8B), // 10 Bright Green
        B(0xF5, 0xF5, 0x43), // 11 Bright Yellow
        B(0x3B, 0x8E, 0xEA), // 12 Bright Blue
        B(0xD6, 0x70, 0xD6), // 13 Bright Magenta
        B(0x29, 0xB8, 0xDB), // 14 Bright Cyan
        B(0xFF, 0xFF, 0xFF), // 15 Bright White
    ];

    private static readonly SolidColorBrush DefaultForeground = B(0xCC, 0xCC, 0xCC);
    private static readonly SolidColorBrush DefaultBackground = B(0x1E, 0x1E, 0x1E);
    private static readonly SolidColorBrush CursorBrush = B(0xFF, 0xFF, 0xFF);

    private static SolidColorBrush B(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public TerminalControl()
    {
        _children = new VisualCollection(this) { _visual };

        Focusable = true;
        FocusVisualStyle = null;

        _fontSize = 14;
        UpdateFont();

        Loaded += OnLoaded;
    }

    /// <summary>
    /// Attaches this control to a terminal adapter and begins rendering.
    /// </summary>
    public void Attach(WpfTerminalAdapter adapter)
    {
        if (_adapter != null)
        {
            _adapter.OutputReceived -= OnOutputReceived;
        }

        _adapter = adapter;
        _adapter.OutputReceived += OnOutputReceived;

        // Trigger initial resize based on current control size
        UpdateTerminalSize();
        InvalidateVisual();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            _dpiScale = source.CompositionTarget.TransformToDevice.M11;
        }

        Focus();
        UpdateTerminalSize();
    }

    private void UpdateFont()
    {
        // Try Cascadia Mono first, fall back to Consolas
        _typeface = new Typeface(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.Regular, FontStretches.Normal);

        // Measure a character to get cell dimensions
        var ft = new FormattedText(
            "M",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            _typeface,
            _fontSize,
            Brushes.White,
            _dpiScale);

        _cellWidth = ft.WidthIncludingTrailingWhitespace;
        _cellHeight = ft.Height;
    }

    private void OnOutputReceived()
    {
        // Marshal to UI thread
        Dispatcher.BeginInvoke(new Action(Render));
    }

    private void Render()
    {
        if (_adapter == null) return;

        var snapshot = _adapter.GetSnapshot();
        var dc = _visual.RenderOpen();

        try
        {
            // Draw background fill
            dc.DrawRectangle(DefaultBackground, null, new Rect(0, 0, ActualWidth, ActualHeight));

            int width = snapshot.Width;
            int height = snapshot.Height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cell = snapshot.Buffer[y, x];
                    double px = x * _cellWidth;
                    double py = y * _cellHeight;

                    var fg = cell.IsReverse ? GetBrush(cell.Background, DefaultBackground) : GetBrush(cell.Foreground, DefaultForeground);
                    var bg = cell.IsReverse ? GetBrush(cell.Foreground, DefaultForeground) : GetBrush(cell.Background, null);

                    // Draw cell background if not default
                    if (bg != null)
                    {
                        dc.DrawRectangle(bg, null, new Rect(px, py, _cellWidth, _cellHeight));
                    }

                    // Draw character
                    var ch = cell.Character;
                    if (!string.IsNullOrEmpty(ch) && ch != " ")
                    {
                        var weight = cell.IsBold ? FontWeights.Bold : FontWeights.Regular;
                        var style = cell.IsItalic ? FontStyles.Italic : FontStyles.Normal;
                        var tf = new Typeface(_typeface.FontFamily, style, weight, FontStretches.Normal);

                        var ft = new FormattedText(
                            ch,
                            CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,
                            tf,
                            _fontSize,
                            fg,
                            _dpiScale);

                        if (cell.IsDim)
                        {
                            dc.PushOpacity(0.5);
                            dc.DrawText(ft, new Point(px, py));
                            dc.Pop();
                        }
                        else
                        {
                            dc.DrawText(ft, new Point(px, py));
                        }
                    }

                    // Draw underline
                    if (cell.IsUnderline)
                    {
                        var pen = new Pen(fg, 1);
                        pen.Freeze();
                        double underlineY = py + _cellHeight - 2;
                        dc.DrawLine(pen, new Point(px, underlineY), new Point(px + _cellWidth, underlineY));
                    }

                    // Draw strikethrough
                    if (cell.IsStrikethrough)
                    {
                        var pen = new Pen(fg, 1);
                        pen.Freeze();
                        double strikeY = py + _cellHeight / 2;
                        dc.DrawLine(pen, new Point(px, strikeY), new Point(px + _cellWidth, strikeY));
                    }
                }
            }

            // Draw cursor
            if (snapshot.CursorVisible &&
                snapshot.CursorX >= 0 && snapshot.CursorX < width &&
                snapshot.CursorY >= 0 && snapshot.CursorY < height)
            {
                double cx = snapshot.CursorX * _cellWidth;
                double cy = snapshot.CursorY * _cellHeight;

                // Draw a bar cursor
                dc.DrawRectangle(CursorBrush, null, new Rect(cx, cy, 2, _cellHeight));
            }
        }
        finally
        {
            dc.Close();
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateTerminalSize();
    }

    private void UpdateTerminalSize()
    {
        if (_adapter == null || ActualWidth <= 0 || ActualHeight <= 0) return;

        int cols = Math.Max(1, (int)(ActualWidth / _cellWidth));
        int rows = Math.Max(1, (int)(ActualHeight / _cellHeight));

        _adapter.TriggerResize(cols, rows);
        Render();
    }

    // === Keyboard Input ===

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_adapter == null) return;

        var data = AnsiKeyEncoder.Encode(e);
        if (data != null)
        {
            _adapter.EnqueueInput(data);
            e.Handled = true;
        }
    }

    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        if (_adapter == null) return;

        var data = AnsiKeyEncoder.EncodeText(e.Text);
        if (data != null)
        {
            _adapter.EnqueueInput(data);
            e.Handled = true;
        }
    }

    // === Visual tree ===

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    private static SolidColorBrush? GetBrush(Hex1bColor? color, SolidColorBrush? defaultBrush)
    {
        if (color == null || color.Value.IsDefault)
            return defaultBrush;

        var c = color.Value;

        return c.Kind switch
        {
            Hex1bColorKind.Rgb => Cached(c.R, c.G, c.B),
            Hex1bColorKind.Standard => c.AnsiIndex >= 0 && c.AnsiIndex < 8 ? AnsiStandardBrushes[c.AnsiIndex] : defaultBrush,
            Hex1bColorKind.Bright => c.AnsiIndex >= 0 && c.AnsiIndex < 8 ? AnsiBrightBrushes[c.AnsiIndex] : defaultBrush,
            Hex1bColorKind.Indexed => GetIndexedBrush(c.AnsiIndex) ?? defaultBrush,
            _ => defaultBrush
        };
    }

    // Simple brush cache for RGB colors
    private static readonly Dictionary<int, SolidColorBrush> BrushCache = new();

    private static SolidColorBrush Cached(byte r, byte g, byte b)
    {
        int key = (r << 16) | (g << 8) | b;
        if (!BrushCache.TryGetValue(key, out var brush))
        {
            brush = B(r, g, b);
            BrushCache[key] = brush;
        }
        return brush;
    }

    private static SolidColorBrush? GetIndexedBrush(int index)
    {
        if (index < 0) return null;

        // Standard colors 0-7
        if (index < 8) return AnsiStandardBrushes[index];

        // Bright colors 8-15
        if (index < 16) return AnsiBrightBrushes[index - 8];

        // 216-color cube (indices 16-231)
        if (index < 232)
        {
            int i = index - 16;
            int r = i / 36;
            int g = (i % 36) / 6;
            int b = i % 6;
            return Cached(
                (byte)(r == 0 ? 0 : 55 + r * 40),
                (byte)(g == 0 ? 0 : 55 + g * 40),
                (byte)(b == 0 ? 0 : 55 + b * 40));
        }

        // Grayscale ramp (indices 232-255)
        if (index < 256)
        {
            byte v = (byte)(8 + (index - 232) * 10);
            return Cached(v, v, v);
        }

        return null;
    }
}
