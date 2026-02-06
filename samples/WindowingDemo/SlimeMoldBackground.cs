using System.Diagnostics;
using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace WindowingDemo;

/// <summary>
/// A gentle, single-organism slime mold simulation for ambient backgrounds.
/// Creates organic, flowing patterns that slowly evolve over time.
/// </summary>
public static class SlimeMoldBackground
{
    // Dimensions
    private const int DotsPerCellX = 2;
    private const int DotsPerCellY = 4;
    
    // Simulation parameters - tuned for slow, ambient movement
    private const double AgentsPerCell = 0.02;  // Density for intricate patterns
    private const int MaxAgentCount = 2000;    // Prevent excessive agents on large screens
    
    // Frame timing for adaptive refresh
    private const int TargetFps = 20;          // Target framerate
    private const int MinRedrawMs = 50;        // Minimum redraw interval (20 FPS)
    private const int MaxRedrawMs = 200;       // Maximum redraw interval (5 FPS)
    private static readonly Stopwatch _frameTimer = new();
    private static readonly Queue<double> _frameTimes = new();
    private const int FrameTimeSamples = 5;    // Rolling average over N frames
    private const double SensorAngle = Math.PI / 4;
    private const double SensorDistance = 4.0;
    private const double TurnSpeed = Math.PI / 8;
    private const double MoveSpeed = 0.5;
    private const double TrailDeposit = 0.8;
    private const double TrailDecay = 0.97;         // Slow decay for longer trails
    private const double TrailDiffuse = 0.15;       // More diffusion for softer look
    
    // Colors - deep blue/purple palette with bioluminescent glow
    private static readonly Hex1bColor AgentColor = Hex1bColor.FromRgb(160, 220, 255);
    private static readonly Hex1bColor GlowColor = Hex1bColor.FromRgb(80, 180, 255);
    private static readonly Hex1bColor TrailColorBright = Hex1bColor.FromRgb(40, 60, 120);
    private static readonly Hex1bColor TrailColorDim = Hex1bColor.FromRgb(15, 20, 45);
    private static readonly Hex1bColor BackgroundColor = Hex1bColor.FromRgb(8, 10, 25);
    
    // Glow parameters
    private const double GlowRadius = 6.0;              // Radius in cells (wider spread)
    private const double GlowIntensity = 0.08;          // Max glow brightness (very subtle)
    private const double MaxGlowContribution = 12.0;    // Max RGB added by glow
    
    // State
    private static Agent[]? _agents;
    private static double[,]? _trailMap;
    private static int _width;
    private static int _height;
    private static int _dotWidth;
    private static int _dotHeight;
    private static bool _initialized;
    private static Random? _random;
    
    /// <summary>
    /// Gets the recommended redraw interval based on recent frame times.
    /// Automatically adjusts to maintain smooth animation.
    /// </summary>
    public static int RecommendedRedrawMs { get; private set; } = MinRedrawMs;
    
    private struct Agent
    {
        public double X;
        public double Y;
        public double Angle;
    }
    
    public static IEnumerable<SurfaceLayer> BuildLayers(SurfaceLayerContext ctx, Random random)
    {
        // Start timing this frame
        _frameTimer.Restart();
        
        int width = ctx.Width;
        int height = ctx.Height;
        
        if (!_initialized || _width != width || _height != height)
        {
            Initialize(width, height, random);
        }
        
        _random = random;
        Update();
        
        // Initialize and update pets
        BackgroundPets.Initialize(width, height, random);
        BackgroundPets.Update(random);
        
        // Slime mold layer (base)
        yield return ctx.Layer(RenderBackground);
        
        // Pets layer (on top)
        yield return ctx.Layer(surface =>
        {
            BackgroundPets.Render(surface);
            
            // Record frame time and update recommended redraw
            _frameTimer.Stop();
            UpdateFrameTiming(_frameTimer.Elapsed.TotalMilliseconds);
        });
    }
    
    private static void UpdateFrameTiming(double frameMs)
    {
        _frameTimes.Enqueue(frameMs);
        while (_frameTimes.Count > FrameTimeSamples)
        {
            _frameTimes.Dequeue();
        }
        
        // Calculate average frame time
        double avgFrameMs = _frameTimes.Average();
        
        // Set redraw interval to be slightly longer than frame time
        // to allow for variance, but clamp to reasonable range
        int targetInterval = (int)(avgFrameMs * 1.5) + 10;
        RecommendedRedrawMs = Math.Clamp(targetInterval, MinRedrawMs, MaxRedrawMs);
    }
    
    private static void Initialize(int width, int height, Random random)
    {
        _width = width;
        _height = height;
        _dotWidth = width * DotsPerCellX;
        _dotHeight = height * DotsPerCellY;
        
        // Scale agent count with cell area for consistent density, clamped to max
        int agentCount = Math.Min(MaxAgentCount, (int)(width * height * AgentsPerCell));
        _agents = new Agent[agentCount];
        _trailMap = new double[_dotWidth, _dotHeight];
        _random = random;
        
        // Spawn agents in a dispersed pattern from center
        double centerX = _dotWidth / 2.0;
        double centerY = _dotHeight / 2.0;
        double spawnRadius = Math.Min(_dotWidth, _dotHeight) / 3.0;
        
        for (int i = 0; i < agentCount; i++)
        {
            var angle = random.NextDouble() * Math.PI * 2;
            var dist = random.NextDouble() * spawnRadius;
            _agents[i] = new Agent
            {
                X = centerX + Math.Cos(angle) * dist,
                Y = centerY + Math.Sin(angle) * dist,
                Angle = angle + Math.PI  // Face outward initially
            };
        }
        
        _initialized = true;
    }
    
    private static void Update()
    {
        if (_agents == null || _trailMap == null || _random == null) return;
        
        // Update each agent
        for (int i = 0; i < _agents.Length; i++)
        {
            var agent = _agents[i];
            
            // Sense: sample trail at sensor positions
            double senseLeft = SampleTrail(agent.X, agent.Y, agent.Angle - SensorAngle);
            double senseCenter = SampleTrail(agent.X, agent.Y, agent.Angle);
            double senseRight = SampleTrail(agent.X, agent.Y, agent.Angle + SensorAngle);
            
            // Turn based on sensed values with some randomness
            if (senseCenter >= senseLeft && senseCenter >= senseRight)
            {
                // Keep going straight, maybe wiggle a bit
                agent.Angle += (_random.NextDouble() - 0.5) * TurnSpeed * 0.3;
            }
            else if (senseLeft > senseRight)
            {
                agent.Angle -= TurnSpeed + _random.NextDouble() * TurnSpeed * 0.5;
            }
            else
            {
                agent.Angle += TurnSpeed + _random.NextDouble() * TurnSpeed * 0.5;
            }
            
            // Move
            double newX = agent.X + Math.Cos(agent.Angle) * MoveSpeed;
            double newY = agent.Y + Math.Sin(agent.Angle) * MoveSpeed;
            
            // Wrap around edges for continuous flow
            if (newX < 0) newX += _dotWidth;
            if (newX >= _dotWidth) newX -= _dotWidth;
            if (newY < 0) newY += _dotHeight;
            if (newY >= _dotHeight) newY -= _dotHeight;
            
            agent.X = newX;
            agent.Y = newY;
            _agents[i] = agent;
            
            // Deposit trail
            int tx = (int)agent.X;
            int ty = (int)agent.Y;
            if (tx >= 0 && tx < _dotWidth && ty >= 0 && ty < _dotHeight)
            {
                _trailMap[tx, ty] = Math.Min(1.0, _trailMap[tx, ty] + TrailDeposit);
            }
        }
        
        // Decay and diffuse trail
        DecayTrail();
    }
    
    private static double SampleTrail(double x, double y, double angle)
    {
        double sx = x + Math.Cos(angle) * SensorDistance;
        double sy = y + Math.Sin(angle) * SensorDistance;
        
        // Wrap coordinates
        if (sx < 0) sx += _dotWidth;
        if (sx >= _dotWidth) sx -= _dotWidth;
        if (sy < 0) sy += _dotHeight;
        if (sy >= _dotHeight) sy -= _dotHeight;
        
        int tx = (int)sx;
        int ty = (int)sy;
        if (tx < 0 || tx >= _dotWidth || ty < 0 || ty >= _dotHeight) return 0;
        return _trailMap![tx, ty];
    }
    
    private static void DecayTrail()
    {
        var newTrail = new double[_dotWidth, _dotHeight];
        
        for (int y = 0; y < _dotHeight; y++)
        {
            for (int x = 0; x < _dotWidth; x++)
            {
                double sum = _trailMap![x, y];
                int count = 1;
                
                // Sample neighbors for diffusion
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        
                        // Wrap coordinates
                        int nx = (x + dx + _dotWidth) % _dotWidth;
                        int ny = (y + dy + _dotHeight) % _dotHeight;
                        
                        sum += _trailMap[nx, ny] * TrailDiffuse;
                        count++;
                    }
                }
                
                newTrail[x, y] = (sum / count) * TrailDecay;
            }
        }
        
        _trailMap = newTrail;
    }
    
    private static void RenderBackground(Surface surface)
    {
        if (_trailMap == null || _agents == null) return;
        
        // Pre-compute glow map from agent positions
        var glowMap = new double[surface.Width, surface.Height];
        double glowRadiusSq = GlowRadius * GlowRadius;
        
        foreach (var agent in _agents)
        {
            int agentCellX = (int)agent.X / DotsPerCellX;
            int agentCellY = (int)agent.Y / DotsPerCellY;
            
            // Add glow to surrounding cells
            int radius = (int)Math.Ceiling(GlowRadius);
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int cellX = agentCellX + dx;
                    int cellY = agentCellY + dy;
                    
                    // Wrap coordinates
                    if (cellX < 0) cellX += surface.Width;
                    if (cellX >= surface.Width) cellX -= surface.Width;
                    if (cellY < 0) cellY += surface.Height;
                    if (cellY >= surface.Height) cellY -= surface.Height;
                    
                    double distSq = dx * dx + dy * dy;
                    if (distSq <= glowRadiusSq)
                    {
                        // Smooth falloff using cosine interpolation for very gradual transition
                        double dist = Math.Sqrt(distSq);
                        double t = dist / GlowRadius;
                        // Smoothstep-like falloff: 1 - (3t² - 2t³) for gradual edges
                        double falloff = 1.0 - (3 * t * t - 2 * t * t * t);
                        glowMap[cellX, cellY] += GlowIntensity * falloff;
                    }
                }
            }
        }
        
        for (int cellY = 0; cellY < surface.Height; cellY++)
        {
            for (int cellX = 0; cellX < surface.Width; cellX++)
            {
                // Sample trail intensity in this cell
                double trailTotal = 0;
                int brailleBits = 0;
                bool hasAgent = false;
                
                for (int dy = 0; dy < DotsPerCellY; dy++)
                {
                    for (int dx = 0; dx < DotsPerCellX; dx++)
                    {
                        int dotX = cellX * DotsPerCellX + dx;
                        int dotY = cellY * DotsPerCellY + dy;
                        if (dotX >= _dotWidth || dotY >= _dotHeight) continue;
                        
                        trailTotal += _trailMap[dotX, dotY];
                    }
                }
                
                // Check for agents in this cell
                foreach (var agent in _agents)
                {
                    int agentCellX = (int)agent.X / DotsPerCellX;
                    int agentCellY = (int)agent.Y / DotsPerCellY;
                    
                    if (agentCellX == cellX && agentCellY == cellY)
                    {
                        hasAgent = true;
                        int localX = (int)agent.X % DotsPerCellX;
                        int localY = (int)agent.Y % DotsPerCellY;
                        
                        brailleBits |= localY switch
                        {
                            0 => localX == 0 ? 0x01 : 0x08,
                            1 => localX == 0 ? 0x02 : 0x10,
                            2 => localX == 0 ? 0x04 : 0x20,
                            3 => localX == 0 ? 0x40 : 0x80,
                            _ => 0
                        };
                    }
                }
                
                // Normalize trail intensity
                double trailIntensity = Math.Min(1.0, trailTotal / (DotsPerCellX * DotsPerCellY) * 2.5);
                
                // Get glow intensity (clamped)
                double glow = Math.Min(1.0, glowMap[cellX, cellY]);
                
                // Start with trail-based color
                double r = BackgroundColor.R + (TrailColorBright.R - BackgroundColor.R) * trailIntensity;
                double g = BackgroundColor.G + (TrailColorBright.G - BackgroundColor.G) * trailIntensity;
                double b = BackgroundColor.B + (TrailColorBright.B - BackgroundColor.B) * trailIntensity;
                
                // Add glow contribution (subtle additive blending, clamped to stay close to background)
                double glowR = Math.Min(MaxGlowContribution, GlowColor.R * glow * 0.15);
                double glowG = Math.Min(MaxGlowContribution, GlowColor.G * glow * 0.12);
                double glowB = Math.Min(MaxGlowContribution, GlowColor.B * glow * 0.10);
                r += glowR;
                g += glowG;
                b += glowB;
                
                var bgColor = Hex1bColor.FromRgb(
                    (byte)Math.Min(255, r),
                    (byte)Math.Min(255, g),
                    (byte)Math.Min(255, b)
                );
                
                if (hasAgent && brailleBits > 0)
                {
                    var ch = (char)(0x2800 + brailleBits);
                    surface[cellX, cellY] = new SurfaceCell(ch.ToString(), AgentColor, bgColor);
                }
                else
                {
                    surface[cellX, cellY] = new SurfaceCell(" ", null, bgColor);
                }
            }
        }
    }
}
