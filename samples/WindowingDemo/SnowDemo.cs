using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace WindowingDemo;

/// <summary>
/// Snow simulation with braille snowflakes, wind gusts, piling, and ice formation.
/// </summary>
public static class SnowDemo
{
    // Dimensions
    private const int DotsPerCellX = 2;
    private const int DotsPerCellY = 4;
    
    // Simulation parameters
    private const double SnowSpawnChance = 0.08;        // Chance per frame to spawn snow at top
    private const double GravitySpeed = 0.3;            // Base falling speed
    private const double MaxWindStrength = 0.4;         // Maximum horizontal wind force
    private const double WindChangeSpeed = 0.02;        // How fast wind direction changes
    private const double WindGustFrequency = 0.005;     // Chance for wind gust change
    private const double GroundedWindResistance = 0.7;  // Grounded snow resists wind more
    private const double IceFormationRate = 0.002;      // How fast full cells turn to ice
    private const int DotsForIce = 8;                   // Dots needed to start ice formation
    
    // State
    private static List<Snowflake>? _snowflakes;
    private static double[,]? _groundSnow;              // Snow accumulation per dot
    private static double[,]? _iceLevel;                // Ice formation per cell (0-1)
    private static int _width;
    private static int _height;
    private static int _dotWidth;
    private static int _dotHeight;
    private static bool _initialized;
    private static double _currentWind;                 // Current wind strength (-1 to 1)
    private static double _targetWind;                  // Target wind for smooth transitions
    private static Random? _random;
    
    private struct Snowflake
    {
        public double X;
        public double Y;
        public double Drift;        // Individual drift tendency
        public bool Grounded;       // Has landed
        public int GroundedNeighbors; // How many neighbors (for wind resistance)
    }
    
    public static IEnumerable<SurfaceLayer> BuildLayers(SurfaceLayerContext ctx, Random random)
    {
        int width = ctx.Width;
        int height = ctx.Height;
        
        if (!_initialized || _width != width || _height != height)
        {
            Initialize(width, height, random);
        }
        
        _random = random;
        Update();
        
        yield return ctx.Layer(RenderBackground);
        yield return ctx.Layer(RenderSnow);
    }
    
    private static void Initialize(int width, int height, Random random)
    {
        _width = width;
        _height = height;
        _dotWidth = width * DotsPerCellX;
        _dotHeight = height * DotsPerCellY;
        
        _snowflakes = new List<Snowflake>();
        _groundSnow = new double[_dotWidth, _dotHeight];
        _iceLevel = new double[width, height];
        _currentWind = 0;
        _targetWind = 0;
        _random = random;
        
        _initialized = true;
    }
    
    private static void Update()
    {
        if (_snowflakes == null || _groundSnow == null || _iceLevel == null || _random == null) return;
        
        // Update wind
        UpdateWind();
        
        // Spawn new snowflakes at top
        SpawnSnow();
        
        // Update falling snowflakes
        UpdateSnowflakes();
        
        // Settle unstable snow piles
        SettleSnow();
        
        // Update grounded snow (wind effects)
        UpdateGroundedSnow();
        
        // Update ice formation
        UpdateIce();
    }
    
    private static void UpdateWind()
    {
        // Occasionally change wind target
        if (_random!.NextDouble() < WindGustFrequency)
        {
            _targetWind = (_random.NextDouble() * 2 - 1) * MaxWindStrength;
        }
        
        // Smoothly transition current wind toward target
        double diff = _targetWind - _currentWind;
        _currentWind += diff * WindChangeSpeed;
    }
    
    private static void SpawnSnow()
    {
        // Spawn across the top
        for (int x = 0; x < _dotWidth; x++)
        {
            if (_random!.NextDouble() < SnowSpawnChance / _dotWidth * 10)
            {
                _snowflakes!.Add(new Snowflake
                {
                    X = x,
                    Y = 0,
                    Drift = (_random.NextDouble() - 0.5) * 0.3,
                    Grounded = false,
                    GroundedNeighbors = 0
                });
            }
        }
    }
    
    private static void UpdateSnowflakes()
    {
        for (int i = _snowflakes!.Count - 1; i >= 0; i--)
        {
            var flake = _snowflakes[i];
            if (flake.Grounded) continue;
            
            // Apply gravity
            double newY = flake.Y + GravitySpeed;
            
            // Apply wind + individual drift
            double windEffect = _currentWind + flake.Drift;
            double newX = flake.X + windEffect;
            
            // Wrap horizontally
            if (newX < 0) newX += _dotWidth;
            if (newX >= _dotWidth) newX -= _dotWidth;
            
            // Check for landing
            int checkX = (int)newX;
            int checkY = (int)newY;
            
            bool landed = false;
            
            // Check ground or existing snow
            if (checkY >= _dotHeight - 1)
            {
                // Hit bottom
                landed = true;
                newY = _dotHeight - 1;
            }
            else if (checkX >= 0 && checkX < _dotWidth && checkY >= 0 && checkY < _dotHeight)
            {
                // Check if there's snow below
                if (_groundSnow![checkX, checkY + 1] > 0.5)
                {
                    landed = true;
                }
            }
            
            if (landed)
            {
                // Add to ground snow
                int gx = Math.Clamp((int)newX, 0, _dotWidth - 1);
                int gy = Math.Clamp((int)newY, 0, _dotHeight - 1);
                
                // Check for tumbling - if only supported by one thing below, tumble sideways
                int supportBelow = CountSupportBelow(gx, gy);
                if (supportBelow == 1 && _random!.NextDouble() < 0.9)
                {
                    // Tumble left or right
                    int tumbleDir = _random.NextDouble() < 0.5 ? -1 : 1;
                    int tumbleX = gx + tumbleDir;
                    
                    // Wrap horizontally
                    if (tumbleX < 0) tumbleX = _dotWidth - 1;
                    if (tumbleX >= _dotWidth) tumbleX = 0;
                    
                    // Check if tumble position is valid (empty and has support or is bottom)
                    bool canTumble = _groundSnow![tumbleX, gy] < 0.5;
                    if (canTumble && gy < _dotHeight - 1)
                    {
                        canTumble = _groundSnow[tumbleX, gy + 1] > 0.5 || gy >= _dotHeight - 1;
                    }
                    
                    if (canTumble)
                    {
                        gx = tumbleX;
                    }
                    else
                    {
                        // Try other direction
                        tumbleX = gx - tumbleDir;
                        if (tumbleX < 0) tumbleX = _dotWidth - 1;
                        if (tumbleX >= _dotWidth) tumbleX = 0;
                        
                        canTumble = _groundSnow[tumbleX, gy] < 0.5;
                        if (canTumble && gy < _dotHeight - 1)
                        {
                            canTumble = _groundSnow[tumbleX, gy + 1] > 0.5;
                        }
                        
                        if (canTumble)
                        {
                            gx = tumbleX;
                        }
                    }
                }
                
                _groundSnow![gx, gy] = Math.Min(1.0, _groundSnow[gx, gy] + 1.0);
                
                flake.Grounded = true;
                flake.X = gx;
                flake.Y = gy;
                flake.GroundedNeighbors = CountGroundedNeighbors(gx, gy);
                _snowflakes[i] = flake;
            }
            else
            {
                flake.X = newX;
                flake.Y = newY;
                _snowflakes[i] = flake;
            }
        }
        
        // Remove old grounded flakes that have been absorbed into ice
        _snowflakes.RemoveAll(f => f.Grounded && IsIceAt((int)f.X, (int)f.Y));
    }
    
    private static void SettleSnow()
    {
        // Scan from bottom to top, looking for unstable snow that should tumble
        // A dot is unstable if it has no lateral support and can move sideways
        
        for (int y = _dotHeight - 1; y >= 0; y--)
        {
            for (int x = 0; x < _dotWidth; x++)
            {
                if (_groundSnow![x, y] < 0.5) continue;
                
                // Skip if in ice cell
                if (IsIceAt(x, y)) continue;
                
                // Check if this dot has lateral support at same level
                bool hasLeftNeighbor = (x > 0 && _groundSnow[x - 1, y] > 0.5);
                bool hasRightNeighbor = (x < _dotWidth - 1 && _groundSnow[x + 1, y] > 0.5);
                
                // If no lateral support, try to tumble
                if (!hasLeftNeighbor && !hasRightNeighbor)
                {
                    // 90% chance to tumble
                    if (_random!.NextDouble() < 0.9)
                    {
                        TryTumbleDot(x, y);
                    }
                }
            }
        }
    }
    
    private static void TryTumbleDot(int x, int y)
    {
        // Try to tumble to a lower position diagonally
        int dir = _random!.NextDouble() < 0.5 ? -1 : 1;
        
        for (int attempt = 0; attempt < 2; attempt++)
        {
            int newX = x + dir;
            
            // Wrap horizontally
            if (newX < 0) newX = _dotWidth - 1;
            if (newX >= _dotWidth) newX = 0;
            
            // Try to fall diagonally down first, then sideways
            int[] tryYs = (y < _dotHeight - 1) ? new[] { y + 1, y } : new[] { y };
            
            foreach (int newY in tryYs)
            {
                if (_groundSnow![newX, newY] < 0.5)
                {
                    // Need support below or be at bottom
                    bool hasSupport = (newY >= _dotHeight - 1) || (_groundSnow[newX, newY + 1] > 0.5);
                    
                    if (hasSupport)
                    {
                        // Move the snow
                        _groundSnow[x, y] = 0;
                        _groundSnow[newX, newY] = 1.0;
                        
                        // Update any grounded flake at this position
                        for (int i = 0; i < _snowflakes!.Count; i++)
                        {
                            var f = _snowflakes[i];
                            if (f.Grounded && (int)f.X == x && (int)f.Y == y)
                            {
                                f.X = newX;
                                f.Y = newY;
                                _snowflakes[i] = f;
                                break;
                            }
                        }
                        return;
                    }
                }
            }
            
            // Try other direction
            dir = -dir;
        }
    }
    
    private static int CountGroundedNeighbors(int x, int y)
    {
        int count = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < _dotWidth && ny >= 0 && ny < _dotHeight)
                {
                    if (_groundSnow![nx, ny] > 0.5) count++;
                }
            }
        }
        return count;
    }
    
    private static int CountSupportBelow(int x, int y)
    {
        // Count snow in the row below (3 positions: left-below, below, right-below)
        int count = 0;
        int belowY = y + 1;
        
        if (belowY >= _dotHeight) return 3; // At bottom, fully supported
        
        for (int dx = -1; dx <= 1; dx++)
        {
            int nx = x + dx;
            if (nx >= 0 && nx < _dotWidth && _groundSnow![nx, belowY] > 0.5)
            {
                count++;
            }
        }
        return count;
    }
    
    private static bool IsIceAt(int dotX, int dotY)
    {
        int cellX = dotX / DotsPerCellX;
        int cellY = dotY / DotsPerCellY;
        if (cellX < 0 || cellX >= _width || cellY < 0 || cellY >= _height) return false;
        return _iceLevel![cellX, cellY] >= 1.0;
    }
    
    private static void UpdateGroundedSnow()
    {
        // Wind can blow loose snow (not ice) sideways
        for (int i = 0; i < _snowflakes!.Count; i++)
        {
            var flake = _snowflakes[i];
            if (!flake.Grounded) continue;
            
            // Skip if it's in an ice cell
            if (IsIceAt((int)flake.X, (int)flake.Y)) continue;
            
            // Calculate wind resistance based on neighbors
            double resistance = 1.0 - (flake.GroundedNeighbors / 8.0) * GroundedWindResistance;
            double windEffect = _currentWind * resistance * 0.3;
            
            // Only move if wind is strong enough
            if (Math.Abs(windEffect) < 0.05) continue;
            
            double newX = flake.X + windEffect;
            
            // Wrap
            if (newX < 0) newX += _dotWidth;
            if (newX >= _dotWidth) newX -= _dotWidth;
            
            int oldX = (int)flake.X;
            int oldY = (int)flake.Y;
            int newXInt = (int)newX;
            
            // Check if new position is valid (not occupied, has ground support)
            bool canMove = newXInt >= 0 && newXInt < _dotWidth;
            if (canMove && oldY < _dotHeight - 1)
            {
                // Need support below
                canMove = _groundSnow![newXInt, oldY + 1] > 0.5 || oldY >= _dotHeight - 1;
            }
            
            if (canMove && _groundSnow![newXInt, oldY] < 0.5)
            {
                // Move the snow
                _groundSnow[oldX, oldY] = Math.Max(0, _groundSnow[oldX, oldY] - 1.0);
                _groundSnow[newXInt, oldY] = Math.Min(1.0, _groundSnow[newXInt, oldY] + 1.0);
                
                flake.X = newXInt;
                flake.GroundedNeighbors = CountGroundedNeighbors(newXInt, oldY);
                _snowflakes[i] = flake;
            }
        }
    }
    
    private static void UpdateIce()
    {
        // Cells with 8 dots of snow slowly turn to ice
        for (int cellY = 0; cellY < _height; cellY++)
        {
            for (int cellX = 0; cellX < _width; cellX++)
            {
                // Count snow dots in this cell
                int snowDots = 0;
                for (int dy = 0; dy < DotsPerCellY; dy++)
                {
                    for (int dx = 0; dx < DotsPerCellX; dx++)
                    {
                        int dotX = cellX * DotsPerCellX + dx;
                        int dotY = cellY * DotsPerCellY + dy;
                        if (dotX < _dotWidth && dotY < _dotHeight && _groundSnow![dotX, dotY] > 0.5)
                        {
                            snowDots++;
                        }
                    }
                }
                
                // Ice forms when cell is full
                if (snowDots >= DotsForIce)
                {
                    _iceLevel![cellX, cellY] = Math.Min(1.0, _iceLevel[cellX, cellY] + IceFormationRate);
                }
            }
        }
    }
    
    private static void RenderBackground(Surface surface)
    {
        // Dark blue night sky, transitioning to white for ice
        for (int cellY = 0; cellY < surface.Height; cellY++)
        {
            for (int cellX = 0; cellX < surface.Width; cellX++)
            {
                double ice = _iceLevel![cellX, cellY];
                
                // Sky gradient (darker at top)
                double skyGradient = (double)cellY / surface.Height;
                byte baseR = (byte)(10 + skyGradient * 15);
                byte baseG = (byte)(15 + skyGradient * 20);
                byte baseB = (byte)(35 + skyGradient * 25);
                
                // Blend with white for ice
                byte r = (byte)(baseR + (255 - baseR) * ice);
                byte g = (byte)(baseG + (255 - baseG) * ice);
                byte b = (byte)(baseB + (255 - baseB) * ice);
                
                surface[cellX, cellY] = new SurfaceCell(" ", null, Hex1bColor.FromRgb(r, g, b));
            }
        }
    }
    
    private static void RenderSnow(Surface surface)
    {
        if (_snowflakes == null || _groundSnow == null) return;
        
        // Group snow by cell
        var snowByCell = new Dictionary<(int, int), int>(); // braille bits
        
        // Render grounded snow from groundSnow array
        for (int dotY = 0; dotY < _dotHeight; dotY++)
        {
            for (int dotX = 0; dotX < _dotWidth; dotX++)
            {
                if (_groundSnow[dotX, dotY] < 0.5) continue;
                
                int cellX = dotX / DotsPerCellX;
                int cellY = dotY / DotsPerCellY;
                
                // Skip fully frozen cells
                if (cellX < _width && cellY < _height && _iceLevel![cellX, cellY] >= 1.0) continue;
                
                if (cellX < 0 || cellX >= surface.Width || cellY < 0 || cellY >= surface.Height)
                    continue;
                
                var key = (cellX, cellY);
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
                
                snowByCell[key] = snowByCell.GetValueOrDefault(key) | bit;
            }
        }
        
        // Render falling snowflakes
        foreach (var flake in _snowflakes)
        {
            if (flake.Grounded) continue;
            
            int dotX = (int)flake.X;
            int dotY = (int)flake.Y;
            int cellX = dotX / DotsPerCellX;
            int cellY = dotY / DotsPerCellY;
            
            if (cellX < 0 || cellX >= surface.Width || cellY < 0 || cellY >= surface.Height)
                continue;
            
            var key = (cellX, cellY);
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
            
            snowByCell[key] = snowByCell.GetValueOrDefault(key) | bit;
        }
        
        // Render braille characters
        foreach (var (cell, bits) in snowByCell)
        {
            var ch = (char)(0x2800 + bits);
            var existing = surface[cell.Item1, cell.Item2];
            
            // Snow color - white with slight blue tint
            var snowColor = Hex1bColor.FromRgb(240, 245, 255);
            
            surface[cell.Item1, cell.Item2] = new SurfaceCell(
                ch.ToString(),
                snowColor,
                existing.Background
            );
        }
    }
}
