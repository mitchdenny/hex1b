using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace SurfaceDemo;

/// <summary>
/// Radar sweep demo with planes moving across the screen.
/// Features a rotating radar beam that illuminates planes as it passes.
/// </summary>
public static class RadarDemo
{
    // Braille dimensions
    private const int DotsPerCellX = 2;
    private const int DotsPerCellY = 4;
    
    // Radar settings
    private const double SweepSpeed = 0.03;        // Radians per frame
    private const int SweepTrailLength = 12;       // Shorter trail
    private const double RadarRadius = 0.9;        // Fraction of screen
    private const double PlaneIlluminationDecay = 0.005; // Slower fade for planes
    
    // Plane settings
    private const int PlaneCount = 8;
    private const double PlaneSpeed = 0.3;
    
    // Blocky arrow shapes for 8 directions (N, NE, E, SE, S, SW, W, NW)
    private static readonly string[] ArrowChars = ["▲", "◥", "▶", "◢", "▼", "◣", "◀", "◤"];
    
    // State
    private static Plane[]? _planes;
    private static double _sweepAngle;
    private static int _width;
    private static int _height;
    private static int _dotWidth;
    private static int _dotHeight;
    private static double _centerX;
    private static double _centerY;
    private static double _maxRadius;
    private static bool _initialized;
    
    private struct Plane
    {
        public double X;           // Position in dot space
        public double Y;
        public double Vx;          // Velocity
        public double Vy;
        public double Illumination; // 0.0 = dark, 1.0 = fully lit
        public int DirectionIndex;  // 0-7 for arrow character
    }
    
    public static IEnumerable<SurfaceLayer> BuildLayers(SurfaceLayerContext ctx, Random random)
    {
        int width = ctx.Width;
        int height = ctx.Height;
        
        if (!_initialized || _width != width || _height != height)
        {
            Initialize(width, height, random);
        }
        
        // Update simulation
        UpdatePlanes(random);
        UpdateRadar();
        
        yield return ctx.Layer(RenderBackground);
        yield return ctx.Layer(RenderRadarSweep);
        yield return ctx.Layer(RenderPlanes);
    }
    
    private static void Initialize(int width, int height, Random random)
    {
        _width = width;
        _height = height;
        _dotWidth = width * DotsPerCellX;
        _dotHeight = height * DotsPerCellY;
        _centerX = _dotWidth / 2.0;
        _centerY = _dotHeight / 2.0;
        _maxRadius = Math.Min(_dotWidth, _dotHeight) * RadarRadius / 2.0;
        _sweepAngle = 0;
        
        // Create planes
        _planes = new Plane[PlaneCount];
        for (int i = 0; i < PlaneCount; i++)
        {
            SpawnPlane(i, random);
        }
        
        _initialized = true;
    }
    
    private static void SpawnPlane(int index, Random random)
    {
        if (_planes == null) return;
        
        // Spawn from edges
        double x, y, vx, vy;
        int edge = random.Next(4);
        
        switch (edge)
        {
            case 0: // Top
                x = random.NextDouble() * _dotWidth;
                y = 0;
                vx = (random.NextDouble() - 0.5) * PlaneSpeed;
                vy = random.NextDouble() * PlaneSpeed * 0.5 + PlaneSpeed * 0.3;
                break;
            case 1: // Right
                x = _dotWidth;
                y = random.NextDouble() * _dotHeight;
                vx = -random.NextDouble() * PlaneSpeed * 0.5 - PlaneSpeed * 0.3;
                vy = (random.NextDouble() - 0.5) * PlaneSpeed;
                break;
            case 2: // Bottom
                x = random.NextDouble() * _dotWidth;
                y = _dotHeight;
                vx = (random.NextDouble() - 0.5) * PlaneSpeed;
                vy = -random.NextDouble() * PlaneSpeed * 0.5 - PlaneSpeed * 0.3;
                break;
            default: // Left
                x = 0;
                y = random.NextDouble() * _dotHeight;
                vx = random.NextDouble() * PlaneSpeed * 0.5 + PlaneSpeed * 0.3;
                vy = (random.NextDouble() - 0.5) * PlaneSpeed;
                break;
        }
        
        // Calculate direction index (0-7) based on velocity
        double angle = Math.Atan2(vy, vx);
        int dirIndex = (int)Math.Round((angle + Math.PI) / (Math.PI / 4)) % 8;
        // Adjust so 0 = up (negative Y)
        dirIndex = (6 - dirIndex + 8) % 8;
        
        _planes[index] = new Plane
        {
            X = x,
            Y = y,
            Vx = vx,
            Vy = vy,
            Illumination = 0,
            DirectionIndex = dirIndex
        };
    }
    
    private static void UpdatePlanes(Random random)
    {
        if (_planes == null) return;
        
        for (int i = 0; i < _planes.Length; i++)
        {
            var plane = _planes[i];
            
            // Move plane
            plane.X += plane.Vx;
            plane.Y += plane.Vy;
            
            // Fade illumination
            plane.Illumination = Math.Max(0, plane.Illumination - PlaneIlluminationDecay);
            
            // Respawn if off screen
            if (plane.X < -10 || plane.X > _dotWidth + 10 ||
                plane.Y < -10 || plane.Y > _dotHeight + 10)
            {
                SpawnPlane(i, random);
            }
            else
            {
                _planes[i] = plane;
            }
        }
    }
    
    private static void UpdateRadar()
    {
        if (_planes == null) return;
        
        double prevAngle = _sweepAngle;
        _sweepAngle += SweepSpeed;
        if (_sweepAngle > 2 * Math.PI)
            _sweepAngle -= 2 * Math.PI;
        
        // Check if radar beam passed over any planes
        for (int i = 0; i < _planes.Length; i++)
        {
            var plane = _planes[i];
            
            // Calculate plane's angle from center
            double dx = plane.X - _centerX;
            double dy = plane.Y - _centerY;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            
            if (dist > _maxRadius) continue;
            
            double planeAngle = Math.Atan2(dy, dx);
            if (planeAngle < 0) planeAngle += 2 * Math.PI;
            
            // Normalize sweep angle
            double sweepNorm = _sweepAngle;
            if (sweepNorm < 0) sweepNorm += 2 * Math.PI;
            
            // Check if beam passed over plane (with some tolerance)
            double angleDiff = Math.Abs(planeAngle - sweepNorm);
            if (angleDiff > Math.PI) angleDiff = 2 * Math.PI - angleDiff;
            
            if (angleDiff < SweepSpeed * 2)
            {
                plane.Illumination = 1.0;
                _planes[i] = plane;
            }
        }
    }
    
    private static void RenderBackground(Surface surface)
    {
        var bg = Hex1bColor.FromRgb(0, 10, 0); // Dark green tint
        var ringColor = Hex1bColor.FromRgb(0, 40, 0);
        
        int centerCellX = _width / 2;
        int centerCellY = _height / 2;
        
        for (int y = 0; y < surface.Height; y++)
        {
            for (int x = 0; x < surface.Width; x++)
            {
                // Draw range rings
                double dx = x - centerCellX;
                double dy = (y - centerCellY) * 2; // Adjust for cell aspect ratio
                double dist = Math.Sqrt(dx * dx + dy * dy);
                
                string ch = " ";
                Hex1bColor? fg = null;
                
                // Draw concentric rings
                double maxCellRadius = Math.Min(_width, _height) * RadarRadius / 2.0;
                for (int ring = 1; ring <= 4; ring++)
                {
                    double ringDist = maxCellRadius * ring / 4;
                    if (Math.Abs(dist - ringDist) < 0.6)
                    {
                        ch = "·";
                        fg = ringColor;
                        break;
                    }
                }
                
                // Center marker
                if (x == centerCellX && y == centerCellY)
                {
                    ch = "+";
                    fg = Hex1bColor.FromRgb(0, 100, 0);
                }
                
                surface[x, y] = new SurfaceCell(ch, fg, bg);
            }
        }
    }
    
    private static void RenderRadarSweep(Surface surface)
    {
        // Render sweep beam with trail
        for (int trail = 0; trail < SweepTrailLength; trail++)
        {
            double trailAngle = _sweepAngle - trail * SweepSpeed;
            double intensity = 1.0 - (double)trail / SweepTrailLength;
            intensity = intensity * intensity * intensity; // Cubic falloff - sharper
            
            byte green = (byte)(60 + 195 * intensity);
            var beamColor = Hex1bColor.FromRgb(0, green, 0);
            
            // Draw beam as braille dots
            var beamDots = new Dictionary<(int, int), int>();
            
            for (double r = 0; r < _maxRadius; r += 0.5)
            {
                double dotX = _centerX + Math.Cos(trailAngle) * r;
                double dotY = _centerY + Math.Sin(trailAngle) * r;
                
                int dx = (int)dotX;
                int dy = (int)dotY;
                
                if (dx < 0 || dx >= _dotWidth || dy < 0 || dy >= _dotHeight)
                    continue;
                
                int cellX = dx / DotsPerCellX;
                int cellY = dy / DotsPerCellY;
                
                if (cellX < 0 || cellX >= surface.Width || cellY < 0 || cellY >= surface.Height)
                    continue;
                
                int localX = dx % DotsPerCellX;
                int localY = dy % DotsPerCellY;
                
                int bit = localY switch
                {
                    0 => localX == 0 ? 0x01 : 0x08,
                    1 => localX == 0 ? 0x02 : 0x10,
                    2 => localX == 0 ? 0x04 : 0x20,
                    3 => localX == 0 ? 0x40 : 0x80,
                    _ => 0
                };
                
                var key = (cellX, cellY);
                beamDots[key] = beamDots.GetValueOrDefault(key) | bit;
            }
            
            foreach (var (cell, bits) in beamDots)
            {
                var existing = surface[cell.Item1, cell.Item2];
                
                // Merge with existing braille
                int existingBits = 0;
                if (existing.Character.Length == 1 && existing.Character[0] >= 0x2800 && existing.Character[0] <= 0x28FF)
                {
                    existingBits = existing.Character[0] - 0x2800;
                }
                
                int mergedBits = existingBits | bits;
                var ch = (char)(0x2800 + mergedBits);
                
                // Blend colors, preferring brighter
                var fg = existing.Foreground.HasValue && existing.Foreground.Value.G > beamColor.G
                    ? existing.Foreground.Value
                    : beamColor;
                
                surface[cell.Item1, cell.Item2] = new SurfaceCell(
                    ch.ToString(),
                    fg,
                    existing.Background
                );
            }
        }
    }
    
    private static void RenderPlanes(Surface surface)
    {
        if (_planes == null) return;
        
        foreach (var plane in _planes)
        {
            int cellX = (int)plane.X / DotsPerCellX;
            int cellY = (int)plane.Y / DotsPerCellY;
            
            if (cellX < 0 || cellX >= surface.Width || cellY < 0 || cellY >= surface.Height)
                continue;
            
            // Calculate plane color based on illumination
            byte brightness = (byte)(30 + 225 * plane.Illumination);
            var planeColor = Hex1bColor.FromRgb(0, brightness, 0);
            
            var existing = surface[cellX, cellY];
            surface[cellX, cellY] = new SurfaceCell(
                ArrowChars[plane.DirectionIndex],
                planeColor,
                existing.Background
            );
        }
    }
}
