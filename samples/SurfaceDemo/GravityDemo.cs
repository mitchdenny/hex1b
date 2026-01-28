using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace SurfaceDemo;

/// <summary>
/// Orbital gravity simulation with a central body and orbiting satellites.
/// Uses quadrant block characters for smooth sub-cell movement.
/// </summary>
public static class GravityDemo
{
    // Quadrant block dimensions (2x2 per cell)
    private const int QuadrantsPerCellX = 2;
    private const int QuadrantsPerCellY = 2;
    
    // Braille for orbit paths (higher resolution)
    private const int DotsPerCellX = 2;
    private const int DotsPerCellY = 4;
    
    // Physics constants
    private const double G = 80.0;               // Gravitational constant
    private const double TimeStep = 0.008;       // Very slow simulation
    private const double CentralMass = 800.0;    // Mass of central body
    private const int OrbitPredictionSteps = 800; // Steps to predict ahead
    
    // Quadrant block characters for 2x2 sub-cell positioning
    private static readonly string[] QuadrantChars =
    [
        " ",  // 0000 - empty
        "▘",  // 0001 - top-left
        "▝",  // 0010 - top-right
        "▀",  // 0011 - top half
        "▖",  // 0100 - bottom-left
        "▌",  // 0101 - left half
        "▞",  // 0110 - diagonal
        "▛",  // 0111 - missing bottom-right
        "▗",  // 1000 - bottom-right
        "▚",  // 1001 - other diagonal
        "▐",  // 1010 - right half
        "▜",  // 1011 - missing bottom-left
        "▄",  // 1100 - bottom half
        "▙",  // 1101 - missing top-right
        "▟",  // 1110 - missing top-left
        "█",  // 1111 - full block
    ];
    
    // Orbit colors
    private static readonly Hex1bColor[] OrbitColors =
    [
        Hex1bColor.FromRgb(100, 180, 255),  // Blue
        Hex1bColor.FromRgb(255, 180, 100),  // Orange
        Hex1bColor.FromRgb(180, 255, 150),  // Green
    ];
    
    // State
    private static Orbiter[]? _orbiters;
    private static int _width;
    private static int _height;
    private static int _quadWidth;
    private static int _quadHeight;
    private static int _dotWidth;
    private static int _dotHeight;
    private static double _centerX;
    private static double _centerY;
    private static bool _initialized;
    private static int _frameCount;
    
    private struct Orbiter
    {
        public double X;      // Position in quadrant space
        public double Y;
        public double Vx;
        public double Vy;
        public Hex1bColor Color;
        public bool MovingAway; // True if moving away from center (toward apoapsis)
    }
    
    public static IEnumerable<SurfaceLayer> BuildLayers(SurfaceLayerContext ctx, Random random)
    {
        int width = ctx.Width;
        int height = ctx.Height;
        
        if (!_initialized || _width != width || _height != height)
        {
            Initialize(width, height, random);
        }
        
        _frameCount++;
        
        // Update physics
        UpdatePhysics();
        
        yield return ctx.Layer(RenderBackground);
        yield return ctx.Layer(RenderOrbits);
        yield return ctx.Layer(RenderBodies);
    }
    
    private static void Initialize(int width, int height, Random random)
    {
        _width = width;
        _height = height;
        _quadWidth = width * QuadrantsPerCellX;
        _quadHeight = height * QuadrantsPerCellY;
        _dotWidth = width * DotsPerCellX;
        _dotHeight = height * DotsPerCellY;
        _centerX = _quadWidth / 2.0;
        _centerY = _quadHeight / 2.0;
        _frameCount = 0;
        
        // Create 3 orbiters at close-in orbits with good eccentricity
        _orbiters = new Orbiter[3];
        
        double baseRadius = Math.Min(_quadWidth, _quadHeight) / 5.0;
        
        for (int i = 0; i < 3; i++)
        {
            double radius = baseRadius * (0.8 + i * 0.3);
            // Spread starting angles evenly
            double angle = (i * 2 * Math.PI / 3) + 0.2;
            
            // Calculate orbital velocity for circular orbit
            double orbitalSpeed = Math.Sqrt(G * CentralMass / radius);
            
            // Apply eccentricity - vary speed to create elliptical orbits
            double speedMod = 0.7 + i * 0.2; // 0.7, 0.9, 1.1
            orbitalSpeed *= speedMod;
            
            _orbiters[i] = new Orbiter
            {
                X = _centerX + Math.Cos(angle) * radius,
                Y = _centerY + Math.Sin(angle) * radius,
                Vx = -Math.Sin(angle) * orbitalSpeed,
                Vy = Math.Cos(angle) * orbitalSpeed,
                Color = OrbitColors[i],
                MovingAway = true
            };
        }
        
        _initialized = true;
    }
    
    private static void RespawnOrbiter(int index)
    {
        if (_orbiters == null) return;
        
        var random = Random.Shared;
        double baseRadius = Math.Min(_quadWidth, _quadHeight) / 5.0;
        double radius = baseRadius * (0.8 + index * 0.3);
        double angle = random.NextDouble() * 2 * Math.PI;
        
        double orbitalSpeed = Math.Sqrt(G * CentralMass / radius);
        double speedMod = 0.7 + index * 0.2;
        orbitalSpeed *= speedMod;
        
        _orbiters[index] = new Orbiter
        {
            X = _centerX + Math.Cos(angle) * radius,
            Y = _centerY + Math.Sin(angle) * radius,
            Vx = -Math.Sin(angle) * orbitalSpeed,
            Vy = Math.Cos(angle) * orbitalSpeed,
            Color = OrbitColors[index],
            MovingAway = true
        };
    }
    
    private struct OrbitData
    {
        public List<(double x, double y)> PathToExtreme;
        public (double x, double y) ExtremePoint;
        public bool IsApoapsis; // True if heading to apoapsis, false if to periapsis
    }
    
    private static OrbitData PredictToExtreme(int orbiterIndex)
    {
        var result = new OrbitData { PathToExtreme = [], ExtremePoint = (0, 0), IsApoapsis = false };
        if (_orbiters == null) return result;
        
        var orbiter = _orbiters[orbiterIndex];
        
        double simX = orbiter.X;
        double simY = orbiter.Y;
        double simVx = orbiter.Vx;
        double simVy = orbiter.Vy;
        
        // Determine if we're moving toward apoapsis or periapsis
        double currentDist = Math.Sqrt((simX - _centerX) * (simX - _centerX) + 
                                       (simY - _centerY) * (simY - _centerY));
        
        // Check radial velocity (positive = moving away)
        double radialVel = ((simX - _centerX) * simVx + (simY - _centerY) * simVy) / currentDist;
        result.IsApoapsis = radialVel > 0;
        
        double prevDist = currentDist;
        
        for (int step = 0; step < OrbitPredictionSteps; step++)
        {
            // Scale to dot space for path rendering
            double dotX = simX * DotsPerCellX / QuadrantsPerCellX;
            double dotY = simY * DotsPerCellY / QuadrantsPerCellY;
            result.PathToExtreme.Add((dotX, dotY));
            
            // Calculate force from central body
            double dx = _centerX - simX;
            double dy = _centerY - simY;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 1) dist = 1;
            
            double force = G * CentralMass / (dist * dist);
            double ax = force * dx / dist;
            double ay = force * dy / dist;
            
            simVx += ax * TimeStep;
            simVy += ay * TimeStep;
            simX += simVx * TimeStep;
            simY += simVy * TimeStep;
            
            double newDist = Math.Sqrt((simX - _centerX) * (simX - _centerX) + 
                                       (simY - _centerY) * (simY - _centerY));
            
            // Detect extreme point (direction change)
            if (result.IsApoapsis && newDist < prevDist && step > 5)
            {
                // Found apoapsis (was moving out, now moving in)
                result.ExtremePoint = (simX * DotsPerCellX / QuadrantsPerCellX,
                                       simY * DotsPerCellY / QuadrantsPerCellY);
                break;
            }
            else if (!result.IsApoapsis && newDist > prevDist && step > 5)
            {
                // Found periapsis (was moving in, now moving out)
                result.ExtremePoint = (simX * DotsPerCellX / QuadrantsPerCellX,
                                       simY * DotsPerCellY / QuadrantsPerCellY);
                break;
            }
            
            prevDist = newDist;
        }
        
        return result;
    }
    
    private static void UpdatePhysics()
    {
        if (_orbiters == null) return;
        
        for (int i = 0; i < _orbiters.Length; i++)
        {
            var orbiter = _orbiters[i];
            
            // Track if moving away before update
            double distBefore = Math.Sqrt((orbiter.X - _centerX) * (orbiter.X - _centerX) + 
                                          (orbiter.Y - _centerY) * (orbiter.Y - _centerY));
            
            // Calculate force from central body only
            double dx = _centerX - orbiter.X;
            double dy = _centerY - orbiter.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 1) dist = 1;
            
            double force = G * CentralMass / (dist * dist);
            double ax = force * dx / dist;
            double ay = force * dy / dist;
            
            orbiter.Vx += ax * TimeStep;
            orbiter.Vy += ay * TimeStep;
            orbiter.X += orbiter.Vx * TimeStep;
            orbiter.Y += orbiter.Vy * TimeStep;
            
            // Update moving direction
            double distAfter = Math.Sqrt((orbiter.X - _centerX) * (orbiter.X - _centerX) + 
                                         (orbiter.Y - _centerY) * (orbiter.Y - _centerY));
            orbiter.MovingAway = distAfter > distBefore;
            
            _orbiters[i] = orbiter;
        }
    }
    
    private static void RenderBackground(Surface surface)
    {
        var bg = Hex1bColor.FromRgb(5, 5, 15);
        
        for (int y = 0; y < surface.Height; y++)
        {
            for (int x = 0; x < surface.Width; x++)
            {
                string ch = " ";
                Hex1bColor? fg = null;
                
                int hash = (x * 7 + y * 13) % 100;
                if (hash < 2)
                {
                    ch = hash == 0 ? "·" : "∘";
                    byte brightness = (byte)(40 + (hash * 20));
                    fg = Hex1bColor.FromRgb(brightness, brightness, (byte)(brightness + 10));
                }
                
                surface[x, y] = new SurfaceCell(ch, fg, bg);
            }
        }
    }
    
    private static void RenderOrbits(Surface surface)
    {
        if (_orbiters == null) return;
        
        for (int o = 0; o < _orbiters.Length; o++)
        {
            var orbitData = PredictToExtreme(o);
            
            // Dim the orbit color
            var orbitColor = Hex1bColor.FromRgb(
                (byte)(_orbiters[o].Color.R / 3),
                (byte)(_orbiters[o].Color.G / 3),
                (byte)(_orbiters[o].Color.B / 3)
            );
            
            // Group path points by cell using braille
            var orbitCells = new Dictionary<(int, int), int>();
            
            foreach (var (x, y) in orbitData.PathToExtreme)
            {
                int dotX = (int)x;
                int dotY = (int)y;
                
                if (dotX < 0 || dotX >= _dotWidth || dotY < 0 || dotY >= _dotHeight)
                    continue;
                
                int cellX = dotX / DotsPerCellX;
                int cellY = dotY / DotsPerCellY;
                
                if (cellX < 0 || cellX >= surface.Width || cellY < 0 || cellY >= surface.Height)
                    continue;
                
                int localX = dotX % DotsPerCellX;
                int localY = dotY % DotsPerCellY;
                
                int bit = localY switch
                {
                    0 => localX == 0 ? 0x01 : 0x08,
                    1 => localX == 0 ? 0x02 : 0x10,
                    2 => localX == 0 ? 0x04 : 0x20,
                    3 => localX == 0 ? 0x40 : 0x80,
                    _ => 0
                };
                
                var key = (cellX, cellY);
                orbitCells[key] = orbitCells.GetValueOrDefault(key) | bit;
            }
            
            // Render orbit cells
            foreach (var (cell, bits) in orbitCells)
            {
                var existing = surface[cell.Item1, cell.Item2];
                
                int existingBits = 0;
                if (existing.Character.Length == 1 && existing.Character[0] >= 0x2800 && existing.Character[0] <= 0x28FF)
                {
                    existingBits = existing.Character[0] - 0x2800;
                }
                
                int mergedBits = existingBits | bits;
                var ch = (char)(0x2800 + mergedBits);
                
                var fg = existingBits > 0 && existing.Foreground.HasValue
                    ? BlendColors(existing.Foreground.Value, orbitColor)
                    : orbitColor;
                
                surface[cell.Item1, cell.Item2] = new SurfaceCell(
                    ch.ToString(),
                    fg,
                    existing.Background
                );
            }
            
            // Render extreme point marker (+ for apoapsis, - for periapsis)
            int extremeCellX = (int)orbitData.ExtremePoint.x / DotsPerCellX;
            int extremeCellY = (int)orbitData.ExtremePoint.y / DotsPerCellY;
            
            if (extremeCellX >= 0 && extremeCellX < surface.Width && 
                extremeCellY >= 0 && extremeCellY < surface.Height)
            {
                var existing = surface[extremeCellX, extremeCellY];
                string marker = orbitData.IsApoapsis ? "+" : "-";
                surface[extremeCellX, extremeCellY] = new SurfaceCell(marker, _orbiters[o].Color, existing.Background);
            }
        }
    }
    
    private static void RenderBodies(Surface surface)
    {
        if (_orbiters == null) return;
        
        // Render central sun as a static braille circle
        int centerCellX = (int)_centerX / QuadrantsPerCellX;
        int centerCellY = (int)_centerY / QuadrantsPerCellY;
        
        var sunColor = Hex1bColor.FromRgb(255, 220, 100);
        
        // Draw a 3x3 braille circle
        int sunRadius = 1;
        for (int dy = -sunRadius; dy <= sunRadius; dy++)
        {
            for (int dx = -sunRadius; dx <= sunRadius; dx++)
            {
                int cellX = centerCellX + dx;
                int cellY = centerCellY + dy;
                
                if (cellX < 0 || cellX >= surface.Width || cellY < 0 || cellY >= surface.Height)
                    continue;
                
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > sunRadius + 0.5) continue;
                
                // Full braille block for center, partial for edges
                int brailleBits = dist < 0.5 ? 0xFF : 0xFF;
                var ch = (char)(0x2800 + brailleBits);
                var existing = surface[cellX, cellY];
                
                surface[cellX, cellY] = new SurfaceCell(ch.ToString(), sunColor, existing.Background);
            }
        }
        
        // Render orbiters using quadrant blocks
        var orbitersByCell = new Dictionary<(int, int), List<(int quadrant, Hex1bColor color)>>();
        
        for (int i = 0; i < _orbiters.Length; i++)
        {
            var orbiter = _orbiters[i];
            
            int quadX = (int)orbiter.X;
            int quadY = (int)orbiter.Y;
            
            // Respawn if off-screen
            if (quadX < 0 || quadX >= _quadWidth || quadY < 0 || quadY >= _quadHeight)
            {
                RespawnOrbiter(i);
                continue;
            }
            
            int cellX = quadX / QuadrantsPerCellX;
            int cellY = quadY / QuadrantsPerCellY;
            
            if (cellX < 0 || cellX >= surface.Width || cellY < 0 || cellY >= surface.Height)
                continue;
            
            int localX = quadX % QuadrantsPerCellX;
            int localY = quadY % QuadrantsPerCellY;
            int quadrant = localX + localY * 2;
            
            var key = (cellX, cellY);
            if (!orbitersByCell.TryGetValue(key, out var list))
                orbitersByCell[key] = list = [];
            list.Add((quadrant, orbiter.Color));
        }
        
        foreach (var (cell, orbiters) in orbitersByCell)
        {
            int bits = 0;
            Hex1bColor blendedColor = orbiters[0].color;
            
            foreach (var (quadrant, color) in orbiters)
            {
                int bit = quadrant switch
                {
                    0 => 1, 1 => 2, 2 => 4, 3 => 8, _ => 0
                };
                bits |= bit;
                blendedColor = BlendColors(blendedColor, color);
            }
            
            var existing = surface[cell.Item1, cell.Item2];
            surface[cell.Item1, cell.Item2] = new SurfaceCell(QuadrantChars[bits], blendedColor, existing.Background);
        }
    }
    
    private static Hex1bColor BlendColors(Hex1bColor a, Hex1bColor b)
    {
        return Hex1bColor.FromRgb(
            (byte)((a.R + b.R) / 2),
            (byte)((a.G + b.G) / 2),
            (byte)((a.B + b.B) / 2)
        );
    }
}
