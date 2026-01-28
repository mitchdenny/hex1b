using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace SurfaceDemo;

/// <summary>
/// Shadow ray casting demo - mouse acts as light source, 2x2 obstructions cast shadows.
/// </summary>
public static class ShadowDemo
{
    // Obstruction positions (top-left corner of 2x2 blocks)
    private static readonly List<(int x, int y)> Obstructions = [];
    
    private static int _width;
    private static int _height;
    private static bool _initialized;
    private static Random? _random;
    
    public static IEnumerable<SurfaceLayer> BuildLayers(SurfaceLayerContext ctx, Random random)
    {
        int width = ctx.Width;
        int height = ctx.Height;
        
        if (!_initialized || _width != width || _height != height)
        {
            Initialize(width, height, random);
        }
        
        _random = random;
        
        // Get mouse position as light source
        int lightX = ctx.MouseX;
        int lightY = ctx.MouseY;
        
        // If mouse not over surface, use center
        if (lightX < 0 || lightY < 0)
        {
            lightX = width / 2;
            lightY = height / 2;
        }
        
        yield return ctx.Layer(surface => RenderScene(surface, lightX, lightY));
    }
    
    private static void Initialize(int width, int height, Random random)
    {
        _width = width;
        _height = height;
        _random = random;
        
        Obstructions.Clear();
        
        // Place 5-8 random 2x2 obstructions
        int count = random.Next(5, 9);
        int attempts = 0;
        
        while (Obstructions.Count < count && attempts < 100)
        {
            attempts++;
            int x = random.Next(2, width - 3);
            int y = random.Next(2, height - 3);
            
            // Check for overlap with existing obstructions
            bool overlaps = false;
            foreach (var (ox, oy) in Obstructions)
            {
                if (Math.Abs(x - ox) < 3 && Math.Abs(y - oy) < 3)
                {
                    overlaps = true;
                    break;
                }
            }
            
            if (!overlaps)
            {
                Obstructions.Add((x, y));
            }
        }
        
        _initialized = true;
    }
    
    private static void RenderScene(Surface surface, int lightX, int lightY)
    {
        // Build obstruction lookup
        var obstructionCells = new HashSet<(int, int)>();
        foreach (var (ox, oy) in Obstructions)
        {
            obstructionCells.Add((ox, oy));
            obstructionCells.Add((ox + 1, oy));
            obstructionCells.Add((ox, oy + 1));
            obstructionCells.Add((ox + 1, oy + 1));
        }
        
        // Render each cell
        for (int y = 0; y < surface.Height; y++)
        {
            for (int x = 0; x < surface.Width; x++)
            {
                if (obstructionCells.Contains((x, y)))
                {
                    // Obstruction - solid block
                    surface[x, y] = new SurfaceCell("█", Hex1bColor.FromRgb(80, 80, 100), Hex1bColor.FromRgb(40, 40, 50));
                }
                else if (x == lightX && y == lightY)
                {
                    // Light source
                    surface[x, y] = new SurfaceCell("☼", Hex1bColor.FromRgb(255, 255, 200), Hex1bColor.FromRgb(100, 100, 60));
                }
                else
                {
                    // Check if in shadow
                    bool inShadow = IsInShadow(x, y, lightX, lightY, obstructionCells);
                    
                    // Calculate distance for light falloff
                    double dist = Math.Sqrt((x - lightX) * (x - lightX) + (y - lightY) * (y - lightY));
                    double brightness = Math.Max(0, 1.0 - dist / 20.0);
                    
                    if (inShadow)
                    {
                        brightness *= 0.15; // Shadow darkens significantly
                    }
                    
                    // Floor color with lighting
                    byte r = (byte)(30 + 80 * brightness);
                    byte g = (byte)(35 + 90 * brightness);
                    byte b = (byte)(50 + 100 * brightness);
                    
                    // Use a subtle floor pattern
                    string ch = ((x + y) % 2 == 0) ? "·" : " ";
                    var fg = Hex1bColor.FromRgb((byte)(r + 20), (byte)(g + 20), (byte)(b + 20));
                    
                    surface[x, y] = new SurfaceCell(ch, fg, Hex1bColor.FromRgb(r, g, b));
                }
            }
        }
    }
    
    private static bool IsInShadow(int x, int y, int lightX, int lightY, HashSet<(int, int)> obstructions)
    {
        // Ray cast from cell to light source
        // If any obstruction blocks the path, cell is in shadow
        
        double dx = lightX - x;
        double dy = lightY - y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        
        if (dist < 1) return false; // At light source
        
        // Normalize direction
        dx /= dist;
        dy /= dist;
        
        // Step along ray
        double stepSize = 0.5;
        double cx = x + 0.5; // Start at center of cell
        double cy = y + 0.5;
        
        for (double t = stepSize; t < dist; t += stepSize)
        {
            int checkX = (int)(cx + dx * t);
            int checkY = (int)(cy + dy * t);
            
            // Don't check the cell we're testing
            if (checkX == x && checkY == y) continue;
            
            // Don't check the light source cell
            if (checkX == lightX && checkY == lightY) continue;
            
            if (obstructions.Contains((checkX, checkY)))
            {
                return true; // Blocked by obstruction
            }
        }
        
        return false; // Clear path to light
    }
}
