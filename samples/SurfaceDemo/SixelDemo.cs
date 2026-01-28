using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace SurfaceDemo;

/// <summary>
/// Sixel demo showing perlin noise backgrounds with mouse-driven rectangular overlay.
/// Demonstrates automatic sixel fragmentation when layers overlap.
/// </summary>
public static class SixelDemo
{
    // Rectangle size around mouse
    private const int RectWidth = 8;  // cells
    private const int RectHeight = 4; // cells
    
    // State
    private static int _width;
    private static int _height;
    private static CellMetrics _metrics;
    private static TrackedObject<SixelData>? _backgroundSixel;
    private static TrackedObject<SixelData>? _foregroundSixel;
    private static int _lastMouseX = -1;
    private static int _lastMouseY = -1;
    private static bool _initialized;
    
    // Debug log file
    private static readonly string _logPath = "/tmp/sixel-debug.log";
    
    // Perlin noise permutation table
    private static readonly int[] _perm = new int[512];
    
    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(_logPath, $"{DateTime.Now:HH:mm:ss.fff} {message}\n");
        }
        catch { }
    }
    
    public static IEnumerable<SurfaceLayer> BuildLayers(SurfaceLayerContext ctx, Random random)
    {
        int width = ctx.Width;
        int height = ctx.Height;
        
        // Reinitialize if size or cell metrics changed or first run
        if (!_initialized || _width != width || _height != height || _metrics != ctx.CellMetrics)
        {
            Log($"Initialize: width={width}, height={height}, metrics={ctx.CellMetrics}");
            Initialize(width, height, random, ctx);
            Log($"After Initialize: _backgroundSixel={(_backgroundSixel != null ? "created" : "NULL")}");
        }
        
        // Update foreground sixel if mouse moved
        if (ctx.MouseX != _lastMouseX || ctx.MouseY != _lastMouseY)
        {
            _lastMouseX = ctx.MouseX;
            _lastMouseY = ctx.MouseY;
            
            // Create foreground sixel for the mouse region
            if (ctx.MouseX >= 0 && ctx.MouseY >= 0)
            {
                _foregroundSixel?.Release();
                _foregroundSixel = CreateForegroundSixel(ctx);
                Log($"Foreground sixel: {(_foregroundSixel != null ? "created" : "NULL")}");
            }
        }
        
        yield return ctx.Layer(RenderBackground);
        yield return ctx.Layer(RenderForeground);
    }
    
    private static void Initialize(int width, int height, Random random, SurfaceLayerContext ctx)
    {
        _width = width;
        _height = height;
        // Use cell metrics from context (queried from terminal)
        _metrics = ctx.CellMetrics;
        
        // Initialize permutation table for perlin noise
        for (int i = 0; i < 256; i++)
            _perm[i] = i;
        
        // Fisher-Yates shuffle
        for (int i = 255; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (_perm[i], _perm[j]) = (_perm[j], _perm[i]);
        }
        
        // Duplicate for overflow
        for (int i = 0; i < 256; i++)
            _perm[256 + i] = _perm[i];
        
        // Create background sixel with grayscale perlin noise
        _backgroundSixel?.Release();
        _backgroundSixel = CreateBackgroundSixel(ctx);
        
        _initialized = true;
    }
    
    private static TrackedObject<SixelData>? CreateBackgroundSixel(SurfaceLayerContext ctx)
    {
        // Use actual (floating-point) cell width for precise sizing
        int pixelWidth = _metrics.GetPixelWidthForCells(_width);
        int pixelHeight = _height * _metrics.PixelHeight;
        
        Log($"CreateBackgroundSixel: pixelWidth={pixelWidth}, pixelHeight={pixelHeight}, metrics={_metrics}");
        
        var buffer = new SixelPixelBuffer(pixelWidth, pixelHeight);
        
        double scale = 0.03; // Noise scale
        
        for (int y = 0; y < pixelHeight; y++)
        {
            for (int x = 0; x < pixelWidth; x++)
            {
                // Multi-octave perlin noise
                double noise = 0;
                double amplitude = 1;
                double frequency = 1;
                double maxValue = 0;
                
                for (int octave = 0; octave < 4; octave++)
                {
                    noise += Perlin(x * scale * frequency, y * scale * frequency) * amplitude;
                    maxValue += amplitude;
                    amplitude *= 0.5;
                    frequency *= 2;
                }
                
                noise = (noise / maxValue + 1) / 2; // Normalize to 0-1
                byte gray = (byte)(noise * 200 + 20); // Range 20-220
                
                buffer[x, y] = Rgba32.FromRgb(gray, gray, gray);
            }
        }
        
        var result = ctx.CreateSixel(buffer);
        Log($"CreateBackgroundSixel result: {(result != null ? $"created, payload length={result.Data.Payload.Length}" : "NULL")}");
        return result;
    }
    
    private static TrackedObject<SixelData>? CreateForegroundSixel(SurfaceLayerContext ctx)
    {
        // Use actual (floating-point) cell width for precise sizing
        int pixelWidth = _metrics.GetPixelWidthForCells(RectWidth);
        int pixelHeight = RectHeight * _metrics.PixelHeight;
        
        var buffer = new SixelPixelBuffer(pixelWidth, pixelHeight);
        
        // Calculate offset in pixels based on mouse position
        int offsetX = _metrics.GetPixelForCellBoundary(ctx.MouseX);
        int offsetY = ctx.MouseY * _metrics.PixelHeight;
        
        double scale = 0.05; // Different scale for variety
        
        for (int y = 0; y < pixelHeight; y++)
        {
            for (int x = 0; x < pixelWidth; x++)
            {
                // Use same perlin but with a different tint (blue/cyan)
                double noise = 0;
                double amplitude = 1;
                double frequency = 1;
                double maxValue = 0;
                
                // Use world coordinates for continuity
                int worldX = offsetX + x;
                int worldY = offsetY + y;
                
                for (int octave = 0; octave < 4; octave++)
                {
                    noise += Perlin(worldX * scale * frequency + 100, worldY * scale * frequency + 100) * amplitude;
                    maxValue += amplitude;
                    amplitude *= 0.5;
                    frequency *= 2;
                }
                
                noise = (noise / maxValue + 1) / 2; // Normalize to 0-1
                
                // Blue/cyan tint
                byte r = (byte)(noise * 80 + 20);
                byte g = (byte)(noise * 180 + 40);
                byte b = (byte)(noise * 220 + 35);
                
                buffer[x, y] = Rgba32.FromRgb(r, g, b);
            }
        }
        
        return ctx.CreateSixel(buffer);
    }
    
    private static void RenderBackground(Surface surface)
    {
        var bg = Hex1bColor.FromRgb(30, 30, 30);
        
        // If we have a sixel, only place it at (0,0) - don't fill other cells
        // The sixel will cover the entire surface
        if (_backgroundSixel != null)
        {
            _backgroundSixel.AddRef(); // Surface will hold a reference
            surface[0, 0] = new SurfaceCell(" ", null, null, Sixel: _backgroundSixel);
            // Don't fill other cells - let the sixel show through
        }
        else
        {
            // Fallback: show perlin noise as text-based gradient
            for (int y = 0; y < surface.Height; y++)
            {
                for (int x = 0; x < surface.Width; x++)
                {
                    double noise = Perlin(x * 0.15, y * 0.15);
                    noise = (noise + 1) / 2;
                    byte gray = (byte)(noise * 200 + 20);
                    var color = Hex1bColor.FromRgb(gray, gray, gray);
                    
                    // Use block character for visual density
                    string ch = noise > 0.6 ? "█" : noise > 0.4 ? "▓" : noise > 0.2 ? "▒" : "░";
                    surface[x, y] = new SurfaceCell(ch, color, bg);
                }
            }
        }
    }
    
    private static void RenderForeground(Surface surface)
    {
        if (_lastMouseX < 0 || _lastMouseY < 0)
            return;
        
        // Calculate rectangle position centered on mouse
        int rectX = _lastMouseX - RectWidth / 2;
        int rectY = _lastMouseY - RectHeight / 2;
        
        // Clamp to surface bounds
        rectX = Math.Max(0, Math.Min(rectX, _width - RectWidth));
        rectY = Math.Max(0, Math.Min(rectY, _height - RectHeight));
        
        // For debugging: skip foreground sixel, just draw text content
        // Fill the rectangle area with a solid color and text
        var bg = Hex1bColor.FromRgb(20, 100, 180);
        var fg = Hex1bColor.FromRgb(255, 255, 255);
        for (int dy = 0; dy < RectHeight && rectY + dy < surface.Height; dy++)
        {
            for (int dx = 0; dx < RectWidth && rectX + dx < surface.Width; dx++)
            {
                int x = rectX + dx;
                int y = rectY + dy;
                surface[x, y] = new SurfaceCell("█", fg, bg);
            }
        }
        
        // Draw border around the rectangle
        var borderColor = Hex1bColor.FromRgb(100, 200, 255);
        
        // Top and bottom borders
        for (int x = rectX; x < rectX + RectWidth && x < surface.Width; x++)
        {
            if (rectY > 0)
                surface[x, rectY - 1] = new SurfaceCell("─", borderColor, null);
            if (rectY + RectHeight < surface.Height)
                surface[x, rectY + RectHeight] = new SurfaceCell("─", borderColor, null);
        }
        
        // Left and right borders
        for (int y = rectY; y < rectY + RectHeight && y < surface.Height; y++)
        {
            if (rectX > 0)
                surface[rectX - 1, y] = new SurfaceCell("│", borderColor, null);
            if (rectX + RectWidth < surface.Width)
                surface[rectX + RectWidth, y] = new SurfaceCell("│", borderColor, null);
        }
        
        // Corners
        if (rectX > 0 && rectY > 0)
            surface[rectX - 1, rectY - 1] = new SurfaceCell("┌", borderColor, null);
        if (rectX + RectWidth < surface.Width && rectY > 0)
            surface[rectX + RectWidth, rectY - 1] = new SurfaceCell("┐", borderColor, null);
        if (rectX > 0 && rectY + RectHeight < surface.Height)
            surface[rectX - 1, rectY + RectHeight] = new SurfaceCell("└", borderColor, null);
        if (rectX + RectWidth < surface.Width && rectY + RectHeight < surface.Height)
            surface[rectX + RectWidth, rectY + RectHeight] = new SurfaceCell("┘", borderColor, null);
    }
    
    #region Perlin Noise
    
    private static double Perlin(double x, double y)
    {
        // Find unit grid cell
        int X = (int)Math.Floor(x) & 255;
        int Y = (int)Math.Floor(y) & 255;
        
        // Relative position in cell
        x -= Math.Floor(x);
        y -= Math.Floor(y);
        
        // Compute fade curves
        double u = Fade(x);
        double v = Fade(y);
        
        // Hash coordinates of corners
        int A = _perm[X] + Y;
        int B = _perm[X + 1] + Y;
        
        // Blend results from corners
        return Lerp(v,
            Lerp(u, Grad(_perm[A], x, y), Grad(_perm[B], x - 1, y)),
            Lerp(u, Grad(_perm[A + 1], x, y - 1), Grad(_perm[B + 1], x - 1, y - 1)));
    }
    
    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    
    private static double Lerp(double t, double a, double b) => a + t * (b - a);
    
    private static double Grad(int hash, double x, double y)
    {
        int h = hash & 3;
        double u = h < 2 ? x : y;
        double v = h < 2 ? y : x;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }
    
    #endregion
}
