using Hex1b.Surfaces;
using Hex1b.Theming;

namespace WidgetLayerDemo;

/// <summary>
/// Transition effect that reveals UI from black, building up from the bottom.
/// Borders and symbols fade in first, then alphanumeric text appears as
/// rapidly scrambled characters before settling into the actual content.
/// </summary>
internal sealed class HackRevealEffect
{
    private struct CellInfo
    {
        public bool HasContent;
        public bool IsAlphaNumeric;
        public string Character;
        public Hex1bColor? Foreground;
        public Hex1bColor? Background;
        public double BgRevealTime;    // When background starts fading from black
        public double CharRevealTime;  // When character appears
        public double SettleTime;      // When scrambled text locks to actual (alphanum only)
        public int ScrambleSeed;
    }

    private CellInfo[,]? _cells;
    private int _width, _height;
    private bool _contentDetected;
    private readonly Random _rng = new();

    private const string ScrambleChars =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdef@#$%&!?<>{}[]~";

    public void Reset()
    {
        _cells = null;
        _contentDetected = false;
    }

    private void EnsureSize(int width, int height)
    {
        if (_cells is not null && _width == width && _height == height) return;
        _width = width;
        _height = height;
        _cells = new CellInfo[width, height];
        _contentDetected = false;
    }

    public void Update(double progress, int width, int height)
    {
        EnsureSize(width, height);
    }

    public CellCompute GetCompute(double progress)
    {
        return ctx =>
        {
            var below = ctx.GetBelow();

            // First frame: capture content from the widget layer below
            if (!_contentDetected && _cells is not null)
            {
                bool hasVisibleChar = !below.IsContinuation
                    && below.Character != "\uE000"
                    && !string.IsNullOrEmpty(below.Character)
                    && below.Character != " ";

                // Bottom-up: higher Y (bottom rows) reveal first
                double rowFrac = 1.0 - (double)ctx.Y / Math.Max(1, _height - 1);
                double jitter = _rng.NextDouble() * 0.06;

                if (hasVisibleChar)
                {
                    bool isAlpha = !IsStructuralChar(below.Character);

                    double bgReveal = rowFrac * 0.45 + jitter;
                    // Non-alphanum (borders, box-drawing): appear with background
                    // Alphanum: suppressed until all structure is in place, then scramble
                    double charReveal = isAlpha
                        ? 0.55 + _rng.NextDouble() * 0.10
                        : bgReveal;
                    double settle = isAlpha
                        ? charReveal + 0.20 + _rng.NextDouble() * 0.15
                        : charReveal;

                    _cells[ctx.X, ctx.Y] = new CellInfo
                    {
                        HasContent = true,
                        IsAlphaNumeric = isAlpha,
                        Character = below.Character,
                        Foreground = below.Foreground,
                        Background = below.Background,
                        BgRevealTime = bgReveal,
                        CharRevealTime = charReveal,
                        SettleTime = settle,
                        ScrambleSeed = _rng.Next()
                    };
                }
                else
                {
                    _cells[ctx.X, ctx.Y] = new CellInfo
                    {
                        HasContent = false,
                        Background = below.Background,
                        BgRevealTime = rowFrac * 0.45 + jitter,
                    };
                }

                if (ctx.X == _width - 1 && ctx.Y == _height - 1)
                    _contentDetected = true;

                // First frame: everything hidden
                return new SurfaceCell(" ", null, Hex1bColor.FromRgb(0, 0, 0));
            }

            if (_cells is null) return new SurfaceCell(" ", null, null);

            ref var cell = ref _cells[ctx.X, ctx.Y];

            // All cells settled: passthrough
            if (progress >= 1.0)
                return below;

            // Before background reveal: black
            if (progress < cell.BgRevealTime)
                return new SurfaceCell(" ", null, Hex1bColor.FromRgb(0, 0, 0));

            // Background fade from black to target
            double bgFade = Math.Clamp((progress - cell.BgRevealTime) / 0.18, 0, 1);
            var bg = FadeFromBlack(cell.Background, bgFade);

            // No visible character content: background only
            if (!cell.HasContent)
                return new SurfaceCell(" ", null, bg);

            // Character not yet revealed
            if (progress < cell.CharRevealTime)
                return new SurfaceCell(" ", null, bg);

            // Foreground fade from black
            double fgFade = Math.Clamp((progress - cell.CharRevealTime) / 0.08, 0, 1);
            var fg = FadeFromBlack(cell.Foreground, fgFade);

            // Non-structural (borders, symbols): show actual character immediately
            if (!cell.IsAlphaNumeric)
                return new SurfaceCell(cell.Character, fg, bg) with
                {
                    Attributes = below.Attributes,
                    DisplayWidth = below.DisplayWidth
                };

            // Scramble until settle time, then show captured character
            if (progress >= cell.SettleTime)
                return new SurfaceCell(cell.Character, fg, bg) with
                {
                    Attributes = below.Attributes,
                    DisplayWidth = below.DisplayWidth
                };

            // Scrambled text: cycle rapidly through hacker-style characters
            int idx = (int)(progress * 200 + cell.ScrambleSeed) % ScrambleChars.Length;
            return new SurfaceCell(ScrambleChars[idx].ToString(), fg, bg);
        };
    }

    /// <summary>
    /// Returns true for box-drawing, border, and other structural characters
    /// that should reveal during the structure phase. Everything else (letters,
    /// digits, punctuation, symbols used in data) is suppressed until the
    /// scramble phase.
    /// </summary>
    private static bool IsStructuralChar(string ch)
    {
        if (ch.Length == 0) return false;
        char c = ch[0];
        // Unicode box-drawing (U+2500–U+257F) and block elements (U+2580–U+259F)
        if (c is >= '\u2500' and <= '\u259F') return true;
        // Braille patterns (U+2800–U+28FF)
        if (c is >= '\u2800' and <= '\u28FF') return true;
        // Common ASCII border chars
        if (c is '|' or '+' or '-' or '=' or '_') return true;
        return false;
    }

    private static Hex1bColor? FadeFromBlack(Hex1bColor? target, double amount)
    {
        if (target is null || target.Value.IsDefault)
            return amount >= 1.0 ? target : Hex1bColor.FromRgb(0, 0, 0);

        var c = target.Value;
        return Hex1bColor.FromRgb(
            (byte)(c.R * amount),
            (byte)(c.G * amount),
            (byte)(c.B * amount));
    }
}
