using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hex1b;
using Hex1b.Theming;

using InputMethod = System.Windows.Input.InputMethod;

namespace WpfTerm;

/// <summary>
/// A WPF control that renders a terminal screen buffer using DrawingVisual
/// with GlyphRun batching and render coalescing for performance.
/// </summary>
public class TerminalControl : FrameworkElement
{
    private readonly DrawingVisual _visual = new();
    private readonly VisualCollection _children;

    private WpfTerminalAdapter? _adapter;
    private double _cellWidth;
    private double _cellHeight;
    private double _baselineY;
    private double _fontSize;
    private double _dpiScale = 1.0;
    private bool _renderPending;

    // Pre-cached typeface variants
    private GlyphTypeface? _regularGlyph;
    private GlyphTypeface? _boldGlyph;
    private GlyphTypeface? _italicGlyph;
    private GlyphTypeface? _boldItalicGlyph;

    // KGP image cache: ImageId → (contentHash, bitmap)
    private readonly Dictionary<uint, (byte[] Hash, BitmapSource Bitmap)> _kgpBitmapCache = new();
    // Fallback for chars not in glyph typeface
    private Typeface _fallbackTypeface = null!;

    // Cached pens
    private static readonly Dictionary<int, Pen> PenCache = new();

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
        
        // Disable WPF's Input Method Editor — prevents the system from showing
        // its own blinking caret at the mouse click position
        InputMethod.SetIsInputMethodEnabled(this, false);

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

        UpdateTerminalSize();
        ScheduleRender();
    }

    private int _wmKeyDownCount;
    private int _wmCharCount;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            _dpiScale = source.CompositionTarget.TransformToDevice.M11;
        }

        // Hook Win32 messages directly to count WM_KEYDOWN and WM_CHAR
        if (source is HwndSource hwndSource)
        {
            hwndSource.AddHook(WndProc);
        }

        Focus();
        UpdateFont();
        UpdateTerminalSize();
    }

    private void UpdateFont()
    {
        var fontFamily = new FontFamily("Cascadia Mono, Consolas");
        _fallbackTypeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Regular, FontStretches.Normal);

        // Pre-cache GlyphTypeface variants
        TryCreateGlyphTypeface(fontFamily, FontStyles.Normal, FontWeights.Regular, out _regularGlyph);
        TryCreateGlyphTypeface(fontFamily, FontStyles.Normal, FontWeights.Bold, out _boldGlyph);
        TryCreateGlyphTypeface(fontFamily, FontStyles.Italic, FontWeights.Regular, out _italicGlyph);
        TryCreateGlyphTypeface(fontFamily, FontStyles.Italic, FontWeights.Bold, out _boldItalicGlyph);

        // Measure cell dimensions from the glyph typeface
        if (_regularGlyph != null)
        {
            double emSize = _fontSize;
            _cellHeight = _regularGlyph.Height * emSize;
            _baselineY = _regularGlyph.Baseline * emSize;
            // Use advance width of 'M' for cell width
            if (_regularGlyph.CharacterToGlyphMap.TryGetValue('M', out ushort mGlyph))
            {
                _cellWidth = _regularGlyph.AdvanceWidths[mGlyph] * emSize;
            }
            else
            {
                _cellWidth = emSize * 0.6; // fallback
            }
        }
        else
        {
            // Fallback measurement using FormattedText
            var ft = new FormattedText("M", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                _fallbackTypeface, _fontSize, Brushes.White, _dpiScale);
            _cellWidth = ft.WidthIncludingTrailingWhitespace;
            _cellHeight = ft.Height;
            _baselineY = ft.Baseline;
        }
    }

    private static bool TryCreateGlyphTypeface(FontFamily family, FontStyle style, FontWeight weight, out GlyphTypeface? result)
    {
        var typeface = new Typeface(family, style, weight, FontStretches.Normal);
        if (typeface.TryGetGlyphTypeface(out var glyph))
        {
            result = glyph;
            return true;
        }
        result = null;
        return false;
    }

    private void OnOutputReceived()
    {
        ScheduleRender();
    }

    /// <summary>
    /// Coalesces multiple output events into a single render on the next frame.
    /// </summary>
    private void ScheduleRender()
    {
        if (_renderPending) return;
        _renderPending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
        {
            _renderPending = false;
            Render();
        }));
    }

    private void Render()
    {
        if (_adapter == null) return;

        // Grab KGP placements outside the buffer lock (KgpPlacements takes its own lock)
        var placements = _adapter.GetKgpPlacements();

        var dc = _visual.RenderOpen();
        try
        {
            // Full background fill
            dc.DrawRectangle(DefaultBackground, null, new Rect(0, 0, ActualWidth, ActualHeight));

            // Read directly from the adapter's buffer under lock — no copy needed
            _adapter.RenderUnderLock((buffer, width, height, cursorX, cursorY, cursorVisible, cursorShape) =>
            {
                // Build a set of cells covered by behind-text KGP images
                // so the text pass doesn't draw opaque backgrounds over them
                HashSet<(int, int)>? kgpCoveredCells = null;
                var behindPlacements = placements.Where(p => p.ZIndex < 0).ToList();
                if (behindPlacements.Count > 0)
                {
                    kgpCoveredCells = new HashSet<(int, int)>();
                    foreach (var p in behindPlacements)
                    {
                        for (int py = p.Row; py < p.Row + (int)p.DisplayRows && py < height; py++)
                            for (int px = p.Column; px < p.Column + (int)p.DisplayColumns && px < width; px++)
                                if (py >= 0 && px >= 0)
                                    kgpCoveredCells.Add((px, py));
                    }
                }

                // Pass 1: KGP images behind text (ZIndex < 0)
                RenderKgpImages(dc, placements, width, height, zBehindText: true);

                // Pass 2: Text and cell backgrounds (skipping bg for KGP-covered cells)
                RenderBuffer(dc, buffer, width, height, cursorX, cursorY, cursorVisible, cursorShape, kgpCoveredCells);

                // Pass 3: KGP images on top of text (ZIndex >= 0)
                RenderKgpImages(dc, placements, width, height, zBehindText: false);
            });
        }
        finally
        {
            dc.Close();
        }
    }

    /// <summary>
    /// Core rendering: scans rows for attribute runs and draws them as batched GlyphRuns.
    /// </summary>
    private void RenderBuffer(DrawingContext dc, TerminalCell[,] buffer, int width, int height,
        int cursorX, int cursorY, bool cursorVisible, CursorShape cursorShape,
        HashSet<(int, int)>? kgpCoveredCells)
    {
        for (int y = 0; y < height; y++)
        {
            double py = y * _cellHeight;
            int x = 0;

            while (x < width)
            {
                var cell = buffer[y, x];

                // Determine effective colors (handle reverse)
                var fg = cell.IsReverse ? GetBrush(cell.Background, DefaultBackground) : GetBrush(cell.Foreground, DefaultForeground);
                var bg = cell.IsReverse ? GetBrush(cell.Foreground, DefaultForeground) : GetBrush(cell.Background, null);
                bool bold = cell.IsBold;
                bool italic = cell.IsItalic;
                bool dim = cell.IsDim;
                bool underline = cell.IsUnderline;
                bool strikethrough = cell.IsStrikethrough;

                // Scan for a run of cells with the same style
                int runStart = x;
                x++;
                while (x < width)
                {
                    var next = buffer[y, x];
                    var nextFg = next.IsReverse ? GetBrush(next.Background, DefaultBackground) : GetBrush(next.Foreground, DefaultForeground);
                    var nextBg = next.IsReverse ? GetBrush(next.Foreground, DefaultForeground) : GetBrush(next.Background, null);

                    if (nextFg != fg || nextBg != bg ||
                        next.IsBold != bold || next.IsItalic != italic || next.IsDim != dim ||
                        next.IsUnderline != underline || next.IsStrikethrough != strikethrough)
                        break;
                    x++;
                }

                int runLen = x - runStart;
                double px = runStart * _cellWidth;
                double runWidth = runLen * _cellWidth;

                // Draw background for the entire run — skip cells covered by behind-text KGP images
                if (bg != null)
                {
                    if (kgpCoveredCells != null)
                    {
                        // Draw background only for uncovered segments within the run
                        int segStart = runStart;
                        for (int i = runStart; i < runStart + runLen; i++)
                        {
                            if (kgpCoveredCells.Contains((i, y)))
                            {
                                // Draw any accumulated uncovered segment
                                if (i > segStart)
                                {
                                    double segPx = segStart * _cellWidth;
                                    double segW = (i - segStart) * _cellWidth;
                                    dc.DrawRectangle(bg, null, new Rect(segPx, py, segW, _cellHeight));
                                }
                                segStart = i + 1;
                            }
                        }
                        // Draw remaining uncovered segment
                        if (runStart + runLen > segStart)
                        {
                            double segPx = segStart * _cellWidth;
                            double segW = (runStart + runLen - segStart) * _cellWidth;
                            dc.DrawRectangle(bg, null, new Rect(segPx, py, segW, _cellHeight));
                        }
                    }
                    else
                    {
                        dc.DrawRectangle(bg, null, new Rect(px, py, runWidth, _cellHeight));
                    }
                }

                // Build GlyphRun for the text
                var glyphTypeface = GetGlyphTypeface(bold, italic);
                if (glyphTypeface != null)
                {
                    DrawGlyphRun(dc, glyphTypeface, buffer, y, runStart, runLen, px, py, fg!, dim);
                }
                else
                {
                    // Fallback: single FormattedText for the whole run
                    DrawFormattedRun(dc, buffer, y, runStart, runLen, px, py, fg!, bold, italic, dim);
                }

                // Draw underline for the run
                if (underline)
                {
                    var pen = GetOrCreatePen(fg!);
                    double underlineY = py + _cellHeight - 2;
                    dc.DrawLine(pen, new Point(px, underlineY), new Point(px + runWidth, underlineY));
                }

                // Draw strikethrough for the run
                if (strikethrough)
                {
                    var pen = GetOrCreatePen(fg!);
                    double strikeY = py + _cellHeight / 2;
                    dc.DrawLine(pen, new Point(px, strikeY), new Point(px + runWidth, strikeY));
                }
            }
        }

        // Draw cursor
        if (cursorVisible &&
            cursorX >= 0 && cursorX < width &&
            cursorY >= 0 && cursorY < height)
        {
            double cx = cursorX * _cellWidth;
            double cy = cursorY * _cellHeight;

            // Determine effective shape (Default → BlinkingBlock)
            var shape = cursorShape == CursorShape.Default ? CursorShape.BlinkingBlock : cursorShape;

            switch (shape)
            {
                case CursorShape.BlinkingBlock:
                case CursorShape.SteadyBlock:
                    // Full block cursor — draw inverted cell
                    dc.DrawRectangle(CursorBrush, null, new Rect(cx, cy, _cellWidth, _cellHeight));
                    // Re-draw the character in inverted color
                    var blockCell = buffer[cursorY, cursorX];
                    var ch = blockCell.Character;
                    if (!string.IsNullOrEmpty(ch) && ch != " " && _regularGlyph != null)
                    {
                        var charMap = _regularGlyph.CharacterToGlyphMap;
                        int codepoint = char.ConvertToUtf32(ch, 0);
                        if (charMap.TryGetValue(codepoint, out ushort glyphIdx))
                        {
#pragma warning disable CS0618
                            var glyphRun = new GlyphRun(
                                _regularGlyph, 0, false, _fontSize, (float)_dpiScale,
                                [glyphIdx], new Point(cx, cy + _baselineY),
                                [_cellWidth], null, null, null, null, null, null);
#pragma warning restore CS0618
                            dc.DrawGlyphRun(DefaultBackground, glyphRun);
                        }
                    }
                    break;

                case CursorShape.BlinkingBar:
                case CursorShape.SteadyBar:
                    // Vertical bar cursor (2px wide)
                    dc.DrawRectangle(CursorBrush, null, new Rect(cx, cy, 2, _cellHeight));
                    break;

                case CursorShape.BlinkingUnderline:
                case CursorShape.SteadyUnderline:
                    // Underline cursor (2px tall at bottom of cell)
                    dc.DrawRectangle(CursorBrush, null, new Rect(cx, cy + _cellHeight - 2, _cellWidth, 2));
                    break;
            }
        }
    }

    /// <summary>
    /// Draws a run of characters using a single GlyphRun — much faster than FormattedText.
    /// </summary>
    private void DrawGlyphRun(DrawingContext dc, GlyphTypeface glyphTypeface,
        TerminalCell[,] buffer, int row, int startCol, int count,
        double px, double py, SolidColorBrush fg, bool dim)
    {
        var glyphIndices = new List<ushort>(count);
        var advanceWidths = new List<double>(count);
        var charMap = glyphTypeface.CharacterToGlyphMap;
        double emSize = _fontSize;
        int skippedLeading = 0;
        bool hasGlyphs = false;

        for (int i = 0; i < count; i++)
        {
            var ch = buffer[row, startCol + i].Character;
            if (string.IsNullOrEmpty(ch) || ch == " ")
            {
                if (!hasGlyphs)
                {
                    skippedLeading++;
                    continue;
                }
                // Use space glyph to maintain positioning
                if (charMap.TryGetValue(' ', out ushort spaceGlyph))
                {
                    glyphIndices.Add(spaceGlyph);
                    advanceWidths.Add(_cellWidth);
                }
                continue;
            }

            // Get glyph for first codepoint of grapheme cluster
            int codepoint = char.ConvertToUtf32(ch, 0);
            if (charMap.TryGetValue(codepoint, out ushort glyphIndex))
            {
                hasGlyphs = true;
                glyphIndices.Add(glyphIndex);
                advanceWidths.Add(_cellWidth);
            }
            else
            {
                // Unknown glyph — use .notdef (index 0) or tofu
                hasGlyphs = true;
                glyphIndices.Add(0);
                advanceWidths.Add(_cellWidth);
            }
        }

        if (glyphIndices.Count == 0) return;

        double originX = px + skippedLeading * _cellWidth;
        var origin = new Point(originX, py + _baselineY);

#pragma warning disable CS0618 // GlyphRun constructor is obsolete but the replacement requires .NET 9+
        var glyphRun = new GlyphRun(
            glyphTypeface,
            bidiLevel: 0,
            isSideways: false,
            renderingEmSize: emSize,
            pixelsPerDip: (float)_dpiScale,
            glyphIndices: glyphIndices,
            baselineOrigin: origin,
            advanceWidths: advanceWidths,
            glyphOffsets: null,
            characters: null,
            deviceFontName: null,
            clusterMap: null,
            caretStops: null,
            language: null);
#pragma warning restore CS0618

        if (dim)
        {
            dc.PushOpacity(0.5);
            dc.DrawGlyphRun(fg, glyphRun);
            dc.Pop();
        }
        else
        {
            dc.DrawGlyphRun(fg, glyphRun);
        }
    }

    /// <summary>
    /// Fallback path: draws a run using a single FormattedText (still much better than per-cell).
    /// </summary>
    private void DrawFormattedRun(DrawingContext dc, TerminalCell[,] buffer, int row,
        int startCol, int count, double px, double py,
        SolidColorBrush fg, bool bold, bool italic, bool dim)
    {
        // Build string for the run
        var chars = new char[count];
        bool hasContent = false;
        for (int i = 0; i < count; i++)
        {
            var ch = buffer[row, startCol + i].Character;
            if (!string.IsNullOrEmpty(ch) && ch != " ")
            {
                chars[i] = ch[0];
                hasContent = true;
            }
            else
            {
                chars[i] = ' ';
            }
        }

        if (!hasContent) return;

        var weight = bold ? FontWeights.Bold : FontWeights.Regular;
        var style = italic ? FontStyles.Italic : FontStyles.Normal;
        var tf = new Typeface(_fallbackTypeface.FontFamily, style, weight, FontStretches.Normal);

        var ft = new FormattedText(
            new string(chars),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            tf,
            _fontSize,
            fg,
            _dpiScale);

        if (dim)
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

    private GlyphTypeface? GetGlyphTypeface(bool bold, bool italic) => (bold, italic) switch
    {
        (false, false) => _regularGlyph,
        (true, false) => _boldGlyph ?? _regularGlyph,
        (false, true) => _italicGlyph ?? _regularGlyph,
        (true, true) => _boldItalicGlyph ?? _boldGlyph ?? _regularGlyph,
    };

    private static Pen GetOrCreatePen(SolidColorBrush brush)
    {
        int key = brush.Color.GetHashCode();
        if (!PenCache.TryGetValue(key, out var pen))
        {
            pen = new Pen(brush, 1);
            pen.Freeze();
            PenCache[key] = pen;
        }
        return pen;
    }

    // === KGP Image Rendering ===

    /// <summary>
    /// Renders KGP image placements at the appropriate z-order layer.
    /// </summary>
    private void RenderKgpImages(DrawingContext dc, IReadOnlyList<KgpPlacement> placements,
        int termWidth, int termHeight, bool zBehindText)
    {
        if (_adapter == null || placements.Count == 0) return;

        foreach (var placement in placements)
        {
            bool isBehind = placement.ZIndex < 0;
            if (isBehind != zBehindText) continue;

            // Skip placements entirely off-screen
            if (placement.Row >= termHeight || placement.Column >= termWidth) continue;
            if (placement.Row + (int)placement.DisplayRows <= 0 || placement.Column + (int)placement.DisplayColumns <= 0) continue;

            var imageData = _adapter.GetKgpImage(placement.ImageId);
            if (imageData == null) continue;

            var bitmap = GetOrCreateKgpBitmap(imageData);
            if (bitmap == null) continue;

            // Calculate display rect in pixels
            double destX = placement.Column * _cellWidth + placement.CellOffsetX;
            double destY = placement.Row * _cellHeight + placement.CellOffsetY;
            double destW = placement.DisplayColumns * _cellWidth;
            double destH = placement.DisplayRows * _cellHeight;

            // Handle source clipping
            if (placement.SourceX > 0 || placement.SourceY > 0 ||
                placement.SourceWidth > 0 || placement.SourceHeight > 0)
            {
                int srcX = (int)placement.SourceX;
                int srcY = (int)placement.SourceY;
                int srcW = placement.SourceWidth > 0 ? (int)placement.SourceWidth : (int)imageData.Width - srcX;
                int srcH = placement.SourceHeight > 0 ? (int)placement.SourceHeight : (int)imageData.Height - srcY;

                // Clamp to image bounds
                srcW = Math.Min(srcW, (int)imageData.Width - srcX);
                srcH = Math.Min(srcH, (int)imageData.Height - srcY);

                if (srcW > 0 && srcH > 0)
                {
                    var cropped = new CroppedBitmap(bitmap, new Int32Rect(srcX, srcY, srcW, srcH));
                    dc.DrawImage(cropped, new Rect(destX, destY, destW, destH));
                }
            }
            else
            {
                dc.DrawImage(bitmap, new Rect(destX, destY, destW, destH));
            }
        }
    }

    /// <summary>
    /// Gets or creates a cached WPF BitmapSource from KGP image data.
    /// </summary>
    private BitmapSource? GetOrCreateKgpBitmap(KgpImageData imageData)
    {
        // Check cache — invalidate if content hash changed
        if (_kgpBitmapCache.TryGetValue(imageData.ImageId, out var cached))
        {
            if (cached.Hash.AsSpan().SequenceEqual(imageData.ContentHash))
                return cached.Bitmap;
        }

        var bitmap = ConvertKgpToBitmap(imageData);
        if (bitmap != null)
        {
            bitmap.Freeze();
            _kgpBitmapCache[imageData.ImageId] = (imageData.ContentHash, bitmap);
        }
        return bitmap;
    }

    /// <summary>
    /// Converts raw KGP pixel data to a WPF BitmapSource.
    /// </summary>
    private static BitmapSource? ConvertKgpToBitmap(KgpImageData imageData)
    {
        int w = (int)imageData.Width;
        int h = (int)imageData.Height;
        if (w <= 0 || h <= 0) return null;

        switch (imageData.Format)
        {
            case KgpFormat.Rgba32:
            {
                // Convert RGBA → BGRA (WPF expects BGRA)
                int expectedSize = w * h * 4;
                if (imageData.Data.Length < expectedSize) return null;

                var bgra = new byte[expectedSize];
                for (int i = 0; i < expectedSize; i += 4)
                {
                    bgra[i + 0] = imageData.Data[i + 2]; // B ← R
                    bgra[i + 1] = imageData.Data[i + 1]; // G ← G
                    bgra[i + 2] = imageData.Data[i + 0]; // R ← B
                    bgra[i + 3] = imageData.Data[i + 3]; // A ← A
                }

                return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, bgra, w * 4);
            }

            case KgpFormat.Rgb24:
            {
                // Convert RGB → BGR (WPF expects BGR)
                int expectedSize = w * h * 3;
                if (imageData.Data.Length < expectedSize) return null;

                var bgr = new byte[expectedSize];
                for (int i = 0; i < expectedSize; i += 3)
                {
                    bgr[i + 0] = imageData.Data[i + 2]; // B ← R
                    bgr[i + 1] = imageData.Data[i + 1]; // G ← G
                    bgr[i + 2] = imageData.Data[i + 0]; // R ← B
                }

                return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr24, null, bgr, w * 3);
            }

            case KgpFormat.Png:
            {
                // PNG: decode directly via WPF's built-in PNG decoder
                using var stream = new System.IO.MemoryStream(imageData.Data);
                var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                return decoder.Frames.Count > 0 ? decoder.Frames[0] : null;
            }

            default:
                return null;
        }
    }

    private System.Windows.Threading.DispatcherTimer? _resizeTimer;

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        
        // Debounce resize — WPF fires this for every pixel during window drag.
        // Coalesce into a single resize after the drag settles.
        if (_resizeTimer == null)
        {
            _resizeTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _resizeTimer.Tick += (_, _) =>
            {
                _resizeTimer.Stop();
                UpdateTerminalSize();
            };
        }
        _resizeTimer.Stop();
        _resizeTimer.Start();
    }

    private void UpdateTerminalSize()
    {
        if (_adapter == null || ActualWidth <= 0 || ActualHeight <= 0) return;

        int cols = Math.Max(1, (int)(ActualWidth / _cellWidth));
        int rows = Math.Max(1, (int)(ActualHeight / _cellHeight));

        _adapter.TriggerResize(cols, rows);
        ScheduleRender();
    }

    // === Keyboard Input ===

    private int _keyDownCount;
    private int _previewKeyDownCount;
    private int _textInputCount;

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        _previewKeyDownCount++;
        
        if (_adapter == null) { base.OnPreviewKeyDown(e); return; }

        // Only handle special keys (arrows, function keys, ctrl combos) here.
        // Printable characters go through OnTextInput for correct keyboard
        // layout/shift/capslock handling without ToUnicode side effects.
        var data = AnsiKeyEncoder.Encode(e);
        if (data != null)
        {
            _adapter.EnqueueInput(data);
            e.Handled = true;
            return;
        }
        
        base.OnPreviewKeyDown(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        _keyDownCount++;
    }

    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        // Printable chars are handled via WM_CHAR hook.
        // This suppresses the WPF TextInput to prevent duplicates.
        _textInputCount++;
        e.Handled = true;
    }

    /// <summary>Diagnostic: WPF event counts.</summary>
    public (int KeyDown, int PreviewKeyDown, int TextInput) WpfEventCounts => (_keyDownCount, _previewKeyDownCount, _textInputCount);

    /// <summary>Diagnostic: Win32 message counts.</summary>
    public (int WmKeyDown, int WmChar) Win32Counts => (_wmKeyDownCount, _wmCharCount);

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_CHAR = 0x0102;
    private const int WM_SYSKEYDOWN = 0x0104;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_KEYDOWN:
            case WM_SYSKEYDOWN:
                _wmKeyDownCount++;
                break;
            case WM_CHAR:
                _wmCharCount++;
                // Handle printable characters directly from WM_CHAR.
                // This bypasses WPF's TextInput which drops ~4% of events
                // during rapid mouse activity.
                if (_adapter != null)
                {
                    char ch = (char)wParam;
                    if (ch >= 0x20) // printable (space and above)
                    {
                        var bytes = System.Text.Encoding.UTF8.GetBytes(new[] { ch });
                        _adapter.EnqueueInput(bytes);
                        handled = true; // Suppress WPF TextInput for this char
                    }
                }
                break;
        }
        return IntPtr.Zero;
    }

    // === Mouse Input ===

    private MouseButton _lastPressedButton;
    private bool _mouseButtonDown;
    private int _lastMouseCellX = -1;
    private int _lastMouseCellY = -1;

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        // Always take keyboard focus on click
        Focus();

        if (_adapter == null) return;

        if (_adapter.MouseTrackingEnabled)
        {
            CaptureMouse();

            var pos = CellPosition(e);
            int button = WpfButtonToSgr(e.ChangedButton);
            if (button < 0) return;

            int modifiers = GetMouseModifiers();
            _adapter.EnqueueInput(AnsiKeyEncoder.EncodeMouse(button, pos.x, pos.y, isRelease: false, modifiers), isMouse: true);
            _lastPressedButton = e.ChangedButton;
            _mouseButtonDown = true;
        }

        e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_adapter == null || !_adapter.MouseTrackingEnabled) return;

        ReleaseMouseCapture();

        var pos = CellPosition(e);
        int button = WpfButtonToSgr(e.ChangedButton);
        if (button < 0) return;

        int modifiers = GetMouseModifiers();
        _adapter.EnqueueInput(AnsiKeyEncoder.EncodeMouse(button, pos.x, pos.y, isRelease: true, modifiers), isMouse: true);
        _mouseButtonDown = false;
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_adapter == null || !_adapter.MouseTrackingEnabled) return;
        
        // Mode 1003: report all motion. Mode 1002: only report drag (button held).
        if (!_mouseButtonDown && !_adapter.MouseMotionEnabled) return;

        var pos = CellPosition(e);
        
        // Only send when the cell position actually changes
        if (pos.x == _lastMouseCellX && pos.y == _lastMouseCellY) return;
        _lastMouseCellX = pos.x;
        _lastMouseCellY = pos.y;

        int button = _mouseButtonDown
            ? WpfButtonToSgr(_lastPressedButton) | 32
            : 35;
        int modifiers = GetMouseModifiers();
        _adapter.EnqueueInput(AnsiKeyEncoder.EncodeMouse(
            button, pos.x, pos.y, isRelease: false, modifiers), isMouse: true);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (_adapter == null || !_adapter.MouseTrackingEnabled) return;

        var pos = CellPosition(e);
        int button = e.Delta > 0 ? 64 : 65;
        int modifiers = GetMouseModifiers();
        _adapter.EnqueueInput(AnsiKeyEncoder.EncodeMouse(button, pos.x, pos.y, isRelease: false, modifiers), isMouse: true);
        e.Handled = true;
    }

    private (int x, int y) CellPosition(MouseEventArgs e)
    {
        var point = e.GetPosition(this);
        int col = Math.Max(1, (int)(point.X / _cellWidth) + 1);
        int row = Math.Max(1, (int)(point.Y / _cellHeight) + 1);
        return (col, row);
    }

    private static int WpfButtonToSgr(MouseButton button) => button switch
    {
        MouseButton.Left => 0,
        MouseButton.Middle => 1,
        MouseButton.Right => 2,
        _ => -1
    };

    private static int GetMouseModifiers()
    {
        int mods = 0;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) mods |= 4;
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) mods |= 8;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) mods |= 16;
        return mods;
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
        if (index < 8) return AnsiStandardBrushes[index];
        if (index < 16) return AnsiBrightBrushes[index - 8];

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

        if (index < 256)
        {
            byte v = (byte)(8 + (index - 232) * 10);
            return Cached(v, v, v);
        }

        return null;
    }
}
