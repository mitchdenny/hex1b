using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace SurfaceDemo;

/// <summary>
/// Slime mold demo - simulates Physarum polycephalum behavior using braille sub-cell rendering.
/// Agents follow and deposit trails, creating organic branching patterns.
/// </summary>
public static class SlimeMoldDemo
{
    // Dimensions
    private const int DotsPerCellX = 2;
    private const int DotsPerCellY = 4;
    
    // Simulation parameters
    private const int AgentCount = 500;
    private const double SensorAngle = Math.PI / 4;  // 45 degrees
    private const double SensorDistance = 3.0;
    private const double TurnSpeed = Math.PI / 6;    // 30 degrees
    private const double MoveSpeed = 0.8;
    private const double TrailDeposit = 1.0;
    private const double TrailDecay = 0.95;
    private const double TrailDiffuse = 0.1;
    
    // State
    private static Agent[]? _agents;
    private static double[,]? _trailMap;
    private static int _width;
    private static int _height;
    private static int _dotWidth;
    private static int _dotHeight;
    private static bool _initialized;
    
    private struct Agent
    {
        public double X;
        public double Y;
        public double Angle;
    }
    
    public static IEnumerable<SurfaceLayer> BuildLayers(SurfaceLayerContext ctx, Random random)
    {
        int width = ctx.Width;
        int height = ctx.Height;
        
        // Initialize or reinitialize if size changed
        if (!_initialized || _width != width || _height != height)
        {
            Initialize(width, height, random);
        }
        
        // Update simulation
        Update(random);
        
        // Layer 1: Trail map background
        yield return ctx.Layer(RenderTrailMap);
        
        // Layer 2: Agents as braille dots
        yield return ctx.Layer(surface => RenderAgents(surface));
    }
    
    private static void Initialize(int width, int height, Random random)
    {
        _width = width;
        _height = height;
        _dotWidth = width * DotsPerCellX;
        _dotHeight = height * DotsPerCellY;
        
        // Initialize trail map
        _trailMap = new double[_dotWidth, _dotHeight];
        
        // Initialize agents in center with random angles
        _agents = new Agent[AgentCount];
        double centerX = _dotWidth / 2.0;
        double centerY = _dotHeight / 2.0;
        double spawnRadius = Math.Min(_dotWidth, _dotHeight) / 4.0;
        
        for (int i = 0; i < AgentCount; i++)
        {
            var angle = random.NextDouble() * Math.PI * 2;
            var dist = random.NextDouble() * spawnRadius;
            _agents[i] = new Agent
            {
                X = centerX + Math.Cos(angle) * dist,
                Y = centerY + Math.Sin(angle) * dist,
                Angle = random.NextDouble() * Math.PI * 2
            };
        }
        
        _initialized = true;
    }
    
    private static void Update(Random random)
    {
        if (_agents == null || _trailMap == null) return;
        
        // Move agents
        for (int i = 0; i < _agents.Length; i++)
        {
            ref var agent = ref _agents[i];
            
            // Sense ahead in three directions
            double senseLeft = Sense(agent.X, agent.Y, agent.Angle - SensorAngle);
            double senseCenter = Sense(agent.X, agent.Y, agent.Angle);
            double senseRight = Sense(agent.X, agent.Y, agent.Angle + SensorAngle);
            
            // Turn based on sensed values
            if (senseCenter > senseLeft && senseCenter > senseRight)
            {
                // Keep going straight
            }
            else if (senseLeft > senseRight)
            {
                agent.Angle -= TurnSpeed + random.NextDouble() * TurnSpeed * 0.5;
            }
            else if (senseRight > senseLeft)
            {
                agent.Angle += TurnSpeed + random.NextDouble() * TurnSpeed * 0.5;
            }
            else
            {
                // Random turn when equal
                agent.Angle += (random.NextDouble() - 0.5) * TurnSpeed * 2;
            }
            
            // Move forward
            double newX = agent.X + Math.Cos(agent.Angle) * MoveSpeed;
            double newY = agent.Y + Math.Sin(agent.Angle) * MoveSpeed;
            
            // Bounce off edges
            if (newX < 0 || newX >= _dotWidth)
            {
                agent.Angle = Math.PI - agent.Angle;
                newX = Math.Clamp(newX, 0, _dotWidth - 1);
            }
            if (newY < 0 || newY >= _dotHeight)
            {
                agent.Angle = -agent.Angle;
                newY = Math.Clamp(newY, 0, _dotHeight - 1);
            }
            
            agent.X = newX;
            agent.Y = newY;
            
            // Deposit trail
            int tx = (int)agent.X;
            int ty = (int)agent.Y;
            if (tx >= 0 && tx < _dotWidth && ty >= 0 && ty < _dotHeight)
            {
                _trailMap[tx, ty] = Math.Min(1.0, _trailMap[tx, ty] + TrailDeposit);
            }
        }
        
        // Decay and diffuse trail
        var newTrail = new double[_dotWidth, _dotHeight];
        for (int y = 0; y < _dotHeight; y++)
        {
            for (int x = 0; x < _dotWidth; x++)
            {
                // Diffuse: average with neighbors
                double sum = _trailMap[x, y];
                int count = 1;
                
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx >= 0 && nx < _dotWidth && ny >= 0 && ny < _dotHeight)
                        {
                            sum += _trailMap[nx, ny] * TrailDiffuse;
                            count++;
                        }
                    }
                }
                
                newTrail[x, y] = (sum / count) * TrailDecay;
            }
        }
        _trailMap = newTrail;
    }
    
    private static double Sense(double x, double y, double angle)
    {
        if (_trailMap == null) return 0;
        
        int sx = (int)(x + Math.Cos(angle) * SensorDistance);
        int sy = (int)(y + Math.Sin(angle) * SensorDistance);
        
        if (sx >= 0 && sx < _dotWidth && sy >= 0 && sy < _dotHeight)
        {
            return _trailMap[sx, sy];
        }
        return 0;
    }
    
    private static void RenderTrailMap(Surface surface)
    {
        if (_trailMap == null) return;
        
        // Render trail as background color intensity
        for (int cellY = 0; cellY < surface.Height; cellY++)
        {
            for (int cellX = 0; cellX < surface.Width; cellX++)
            {
                // Sample trail in this cell (average of dots)
                double total = 0;
                for (int dy = 0; dy < DotsPerCellY; dy++)
                {
                    for (int dx = 0; dx < DotsPerCellX; dx++)
                    {
                        int dotX = cellX * DotsPerCellX + dx;
                        int dotY = cellY * DotsPerCellY + dy;
                        if (dotX < _dotWidth && dotY < _dotHeight)
                        {
                            total += _trailMap[dotX, dotY];
                        }
                    }
                }
                double avg = total / (DotsPerCellX * DotsPerCellY);
                
                // Dark background with yellowish trail glow
                var intensity = Math.Min(1.0, avg * 2);
                var bg = Hex1bColor.FromRgb(
                    (byte)(5 + intensity * 60),
                    (byte)(5 + intensity * 50),
                    (byte)(15 + intensity * 20)
                );
                
                surface[cellX, cellY] = new SurfaceCell(" ", null, bg);
            }
        }
    }
    
    private static void RenderAgents(Surface surface)
    {
        if (_agents == null) return;
        
        // Group agents by cell
        var agentsByCell = new Dictionary<(int, int), List<(int dotX, int dotY)>>();
        
        foreach (var agent in _agents)
        {
            int dotX = (int)agent.X;
            int dotY = (int)agent.Y;
            int cellX = dotX / DotsPerCellX;
            int cellY = dotY / DotsPerCellY;
            
            if (cellX < 0 || cellX >= surface.Width || cellY < 0 || cellY >= surface.Height)
                continue;
            
            var key = (cellX, cellY);
            if (!agentsByCell.TryGetValue(key, out var list))
                agentsByCell[key] = list = new List<(int, int)>();
            list.Add((dotX % DotsPerCellX, dotY % DotsPerCellY));
        }
        
        // Render braille characters for agent positions
        foreach (var (cell, dots) in agentsByCell)
        {
            int brailleBits = 0;
            foreach (var (dotX, dotY) in dots)
            {
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
            
            // Bright white/yellow agents on trail background
            var existing = surface[cell.Item1, cell.Item2];
            surface[cell.Item1, cell.Item2] = new SurfaceCell(
                ch.ToString(),
                Hex1bColor.FromRgb(255, 240, 180),
                existing.Background ?? Hex1bColor.FromRgb(5, 5, 15)
            );
        }
    }
}
