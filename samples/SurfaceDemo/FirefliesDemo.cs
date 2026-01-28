using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace SurfaceDemo;

/// <summary>
/// Fireflies demo - glowing dots moving through darkness using braille sub-cell rendering.
/// </summary>
public static class FirefliesDemo
{
    // Surface dimensions
    public const int WidthCells = 80;
    public const int HeightCells = 24;
    public const int BorderPadding = 2;
    public const int RequiredWidth = WidthCells + BorderPadding;
    public const int RequiredHeight = HeightCells + BorderPadding;
    
    // Braille dimensions per cell (2 wide x 4 tall)
    private const int DotsPerCellX = 2;
    private const int DotsPerCellY = 4;
    private const int WidthDots = WidthCells * DotsPerCellX;
    private const int HeightDots = HeightCells * DotsPerCellY;
    
    // Glow configuration
    private const double GlowRadius = 4.0;
    
    // Colors
    private static readonly Hex1bColor PaleYellow = Hex1bColor.FromRgb(255, 255, 200);
    private static readonly Hex1bColor BrightYellow = Hex1bColor.FromRgb(255, 255, 0);
    private static readonly Hex1bColor White = Hex1bColor.FromRgb(255, 255, 255);
    private static readonly Hex1bColor DarkBg = Hex1bColor.FromRgb(5, 10, 5);
    
    // Direction deltas (N, NE, E, SE, S, SW, W, NW)
    private static readonly (int dx, int dy)[] Directions =
    [
        (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)
    ];

    /// <summary>
    /// Firefly state.
    /// </summary>
    public record struct Firefly(int DotX, int DotY, bool IsFlying, int Heading, double ColorPhase, bool WasActive);

    /// <summary>
    /// Creates initial firefly state.
    /// </summary>
    public static Firefly[] CreateFireflies(int count = 20)
    {
        var random = new Random();
        var fireflies = new Firefly[count];
        int flying = 0;
        
        for (int i = 0; i < count; i++)
        {
            bool startFlying = flying < 2 && random.NextDouble() > 0.9;
            if (startFlying) flying++;
            
            fireflies[i] = new Firefly(
                DotX: random.Next(WidthDots),
                DotY: random.Next(HeightDots),
                IsFlying: startFlying,
                Heading: random.Next(8),
                ColorPhase: random.NextDouble(),
                WasActive: startFlying
            );
        }
        return fireflies;
    }

    /// <summary>
    /// Updates firefly positions and states.
    /// </summary>
    public static void Update(Firefly[] fireflies, Random random)
    {
        int flyingCount = fireflies.Count(f => f.IsFlying);
        
        for (int i = 0; i < fireflies.Length; i++)
        {
            var fly = fireflies[i];
            
            // Update color phase
            var newPhase = (fly.ColorPhase + 0.03) % 1.0;
            
            // Toggle flying/resting
            var newFlying = fly.IsFlying;
            var newWasActive = fly.WasActive;
            
            if (newFlying)
            {
                if (random.NextDouble() < 0.005)
                {
                    newFlying = false;
                    flyingCount--;
                }
            }
            else if (flyingCount < 2)
            {
                var takeoffChance = fly.WasActive ? 0.03 : 0.003;
                if (random.NextDouble() < takeoffChance)
                {
                    newFlying = true;
                    newWasActive = true;
                    flyingCount++;
                }
            }
            
            var newX = fly.DotX;
            var newY = fly.DotY;
            var newHeading = fly.Heading;
            
            // Move if flying
            if (newFlying && random.NextDouble() < 0.25)
            {
                var turnChance = random.NextDouble();
                if (turnChance < 0.10)
                    newHeading = (newHeading + 7) % 8;
                else if (turnChance < 0.20)
                    newHeading = (newHeading + 1) % 8;
                
                var (dx, dy) = Directions[newHeading];
                newX += dx;
                newY += dy;
                
                // Bounce off edges
                if (newX < 0 || newX >= WidthDots)
                {
                    newHeading = (newHeading + 3 + random.Next(3)) % 8;
                    newX = Math.Clamp(newX, 0, WidthDots - 1);
                }
                if (newY < 0 || newY >= HeightDots)
                {
                    newHeading = (newHeading + 3 + random.Next(3)) % 8;
                    newY = Math.Clamp(newY, 0, HeightDots - 1);
                }
            }
            
            fireflies[i] = new Firefly(newX, newY, newFlying, newHeading, newPhase, newWasActive);
        }
    }

    /// <summary>
    /// Builds the surface layers for the fireflies demo.
    /// </summary>
    public static IEnumerable<SurfaceLayer> BuildLayers(SurfaceLayerContext ctx, Firefly[] fireflies)
    {
        // Pre-compute cell data
        var (cells, byCell, brightness) = ComputeCellData(fireflies);
        
        // Layer 1: Dark background
        yield return ctx.Layer(surface =>
        {
            surface.Fill(new Rect(0, 0, surface.Width, surface.Height), new SurfaceCell(" ", null, DarkBg));
        });
        
        // Layer 2: Glow effect
        yield return ctx.Layer(surface => RenderGlow(surface, cells, brightness));
        
        // Layer 3: Fireflies
        yield return ctx.Layer(surface => RenderFireflies(surface, fireflies, cells, byCell, brightness));
    }

    private static (HashSet<(int, int)> cells, Dictionary<(int, int), List<int>> byCell, Dictionary<(int, int), double> brightness) 
        ComputeCellData(Firefly[] fireflies)
    {
        var cells = new HashSet<(int, int)>();
        var byCell = new Dictionary<(int, int), List<int>>();
        var brightness = new Dictionary<(int, int), double>();
        
        for (int i = 0; i < fireflies.Length; i++)
        {
            var fly = fireflies[i];
            int cellX = fly.DotX / DotsPerCellX;
            int cellY = fly.DotY / DotsPerCellY;
            
            if (cellX < 0 || cellX >= WidthCells || cellY < 0 || cellY >= HeightCells)
                continue;
            
            var key = (cellX, cellY);
            cells.Add(key);
            
            if (!byCell.TryGetValue(key, out var list))
                byCell[key] = list = new List<int>();
            list.Add(i);
            
            var flyBrightness = fly.IsFlying ? 1.0 : 0.3;
            if (!brightness.TryGetValue(key, out var existing) || flyBrightness > existing)
                brightness[key] = flyBrightness;
        }
        
        return (cells, byCell, brightness);
    }

    private static void RenderGlow(Surface surface, HashSet<(int, int)> cells, Dictionary<(int, int), double> brightness)
    {
        for (int y = 0; y < HeightCells; y++)
        {
            for (int x = 0; x < WidthCells; x++)
            {
                if (cells.Contains((x, y)))
                    continue;
                
                double total = 0;
                foreach (var cell in cells)
                {
                    double dx = x - cell.Item1;
                    double dy = y - cell.Item2;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    
                    if (dist > GlowRadius) continue;
                    if (dist < 0.01) dist = 0.01;
                    
                    var source = brightness.GetValueOrDefault(cell, 1.0);
                    total += source / (1.0 + dist * dist * 0.5);
                }
                
                total = Math.Min(1.0, total);
                if (total < 0.01) continue;
                
                var b = Math.Pow(total, 0.7);
                var glow = Hex1bColor.FromRgb((byte)(b * 100), (byte)(b * 80), (byte)(b * 20));
                surface[x, y] = new SurfaceCell(" ", null, glow);
            }
        }
    }

    private static void RenderFireflies(Surface surface, Firefly[] fireflies, 
        HashSet<(int, int)> cells, Dictionary<(int, int), List<int>> byCell, Dictionary<(int, int), double> brightness)
    {
        foreach (var (cell, indices) in byCell)
        {
            int brailleBits = 0;
            var primary = fireflies[indices[0]];
            
            foreach (var idx in indices)
            {
                var fly = fireflies[idx];
                int dotX = fly.DotX % DotsPerCellX;
                int dotY = fly.DotY % DotsPerCellY;
                
                brailleBits |= dotY switch
                {
                    0 => dotX == 0 ? 0x01 : 0x08,
                    1 => dotX == 0 ? 0x02 : 0x10,
                    2 => dotX == 0 ? 0x04 : 0x20,
                    3 => dotX == 0 ? 0x40 : 0x80,
                    _ => 0
                };
            }
            
            var ch = (char)(0x2800 + brailleBits);
            var color = GetFireflyColor(primary.IsFlying, primary.ColorPhase);
            
            // Calculate background with glow from nearby fireflies
            double total = brightness.GetValueOrDefault(cell, 1.0);
            foreach (var other in cells)
            {
                if (other == cell) continue;
                double dx = cell.Item1 - other.Item1;
                double dy = cell.Item2 - other.Item2;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > GlowRadius) continue;
                total += brightness.GetValueOrDefault(other, 1.0) / (1.0 + dist * dist * 0.5);
            }
            
            var b = Math.Pow(Math.Min(1.0, total), 0.7);
            var bg = Hex1bColor.FromRgb((byte)(100 * b), (byte)(80 * b), (byte)(20 * b));
            
            surface[cell.Item1, cell.Item2] = new SurfaceCell(ch.ToString(), color, bg);
        }
    }

    private static Hex1bColor GetFireflyColor(bool isFlying, double phase)
    {
        var t = (Math.Sin(phase * Math.PI * 2) + 1) / 2;
        var target = isFlying ? BrightYellow : White;
        return Hex1bColor.FromRgb(
            (byte)(PaleYellow.R + (target.R - PaleYellow.R) * t),
            (byte)(PaleYellow.G + (target.G - PaleYellow.G) * t),
            (byte)(PaleYellow.B + (target.B - PaleYellow.B) * t)
        );
    }
}
