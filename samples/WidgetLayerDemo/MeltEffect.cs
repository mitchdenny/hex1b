using Hex1b.Surfaces;
using Hex1b.Theming;

namespace WidgetLayerDemo;

/// <summary>
/// Stateful transition effect that converts content to braille characters
/// which then melt (fall with gravity) to the bottom of the screen.
/// </summary>
internal sealed class MeltEffect
{
    private struct CellInfo
    {
        public bool HasContent;
        public byte BrailleDots;       // Random 8-dot braille pattern
        public double DissolveTime;    // When this cell starts dissolving
        public double FallStartTime;   // When gravity kicks in
        public double FallSpeed;       // Per-cell gravity variation
        public Hex1bColor? Color;      // Original foreground color
    }

    private struct FallingParticle
    {
        public bool Active;
        public byte BrailleDots;
        public Hex1bColor? Color;
        public double DistanceFallen;
    }

    private CellInfo[,]? _cells;
    private FallingParticle[,]? _frameMap;
    private int _width, _height;
    private bool _contentDetected;
    private readonly Random _rng = new();

    public void Reset()
    {
        _cells = null;
        _frameMap = null;
        _contentDetected = false;
    }

    private void EnsureSize(int width, int height)
    {
        if (_cells is not null && _width == width && _height == height) return;
        _width = width;
        _height = height;
        _cells = new CellInfo[width, height];
        _frameMap = new FallingParticle[width, height];
        _contentDetected = false;
    }

    /// <summary>
    /// Pre-compute particle positions for this frame. Call before GetCompute().
    /// </summary>
    public void Update(double progress, int width, int height)
    {
        EnsureSize(width, height);
        if (!_contentDetected || _cells is null || _frameMap is null) return;

        Array.Clear(_frameMap);

        for (int y = 0; y < _height; y++)
        for (int x = 0; x < _width; x++)
        {
            ref var c = ref _cells[x, y];
            if (!c.HasContent || progress < c.DissolveTime) continue;

            double fallTime = Math.Max(0, progress - c.FallStartTime);
            double fallOffset = 0.5 * c.FallSpeed * fallTime * fallTime;
            int currentY = y + (int)fallOffset;

            if (currentY >= 0 && currentY < _height)
            {
                _frameMap[x, currentY] = new FallingParticle
                {
                    Active = true,
                    BrailleDots = c.BrailleDots,
                    Color = c.Color,
                    DistanceFallen = fallOffset
                };
            }
        }
    }

    /// <summary>
    /// Returns a CellCompute delegate for this frame.
    /// On the first frame, detects content from the layer below.
    /// On subsequent frames, renders dissolve + falling braille.
    /// </summary>
    public CellCompute GetCompute(double progress)
    {
        return ctx =>
        {
            var below = ctx.GetBelow();

            // First frame: detect which cells have content
            if (!_contentDetected && _cells is not null)
            {
                bool hasContent = !below.IsContinuation
                    && below.Character != "\uE000" // Unwritten marker
                    && !string.IsNullOrEmpty(below.Character);

                if (hasContent)
                {
                    // Stagger dissolve: top rows go first, with random jitter
                    double rowFrac = (double)ctx.Y / Math.Max(1, _height - 1);
                    double dissolve = rowFrac * 0.25 + _rng.NextDouble() * 0.1;

                    _cells[ctx.X, ctx.Y] = new CellInfo
                    {
                        HasContent = true,
                        BrailleDots = (byte)_rng.Next(1, 256),
                        DissolveTime = dissolve,
                        FallStartTime = dissolve + 0.08 + _rng.NextDouble() * 0.08,
                        FallSpeed = 12.0 + _rng.NextDouble() * 24.0,
                        Color = below.Foreground ?? below.Background
                    };
                }

                // Mark detection complete after visiting the last cell
                if (ctx.X == _width - 1 && ctx.Y == _height - 1)
                    _contentDetected = true;

                // First frame: always passthrough (positions not computed yet)
                return below;
            }

            if (_cells is null || _frameMap is null) return below;

            // Check if a falling particle is at this position
            ref var particle = ref _frameMap[ctx.X, ctx.Y];
            if (particle.Active)
            {
                char braille = (char)(0x2800 + particle.BrailleDots);
                var color = DimColor(particle.Color, particle.DistanceFallen / (_height * 0.5));
                return new SurfaceCell(braille.ToString(), color, null);
            }

            // Check if original content here has been dissolved away
            ref var orig = ref _cells[ctx.X, ctx.Y];
            if (orig.HasContent && progress >= orig.DissolveTime)
            {
                // Particle has moved on — show empty
                return new SurfaceCell(" ", null, null);
            }

            // Not yet dissolved, passthrough
            return below;
        };
    }

    private static Hex1bColor? DimColor(Hex1bColor? color, double amount)
    {
        if (color is null) return null;
        var c = color.Value;
        var factor = 1.0 - Math.Clamp(amount, 0, 0.85);
        return Hex1bColor.FromRgb(
            (byte)(c.R * factor),
            (byte)(c.G * factor),
            (byte)(c.B * factor));
    }
}
