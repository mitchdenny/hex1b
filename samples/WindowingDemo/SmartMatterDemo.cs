using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;
using SkiaSharp;

namespace WindowingDemo;

/// <summary>
/// Smart Matter simulation: braille dot particles flow upward from the bottom
/// to form shapes defined by monochrome images, hold for 5 seconds, then 
/// lose cohesion and fall back down before cycling to the next shape.
/// </summary>
public static class SmartMatterDemo
{
    // Braille dimensions (2x4 dots per terminal cell)
    private const int DotsPerCellX = 2;
    private const int DotsPerCellY = 4;
    
    // Physics constants
    private const double RiseSpeed = 0.8;            // Base upward velocity
    private const double GravitySpeed = 0.15;        // Falling speed when dissolving
    private const double AttractionStrength = 0.12;  // How strongly particles seek targets
    private const double Turbulence = 0.15;          // Random horizontal drift
    private const double LockDistance = 1.5;         // Distance to snap to target
    
    // Timing (in frames at ~50ms per frame)
    private const int HoldDurationFrames = 100;      // 5 seconds at 50ms/frame
    private const int DissolveFrames = 60;           // 3 seconds to dissolve
    private const int ReformDelayFrames = 40;        // 2 seconds before next shape
    
    // State
    private static List<Particle>? _particles;
    private static bool[,]? _targetShape;            // Current target shape in dot space
    private static bool[,]? _targetOccupied;         // Which target dots are claimed
    private static List<string>? _shapePaths;        // Paths to shape images
    private static int _currentShapeIndex;
    private static int _width;
    private static int _height;
    private static int _dotWidth;
    private static int _dotHeight;
    private static bool _initialized;
    private static SimulationPhase _phase;
    private static int _phaseFrameCount;
    private static Random? _random;
    
    private enum SimulationPhase
    {
        Rising,      // Particles flowing up to form shape
        Holding,     // Shape is formed, holding steady
        Dissolving,  // Particles falling back down
        Reforming    // Brief pause before next shape
    }
    
    private struct Particle
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public int TargetX;      // Assigned target position
        public int TargetY;
        public bool HasTarget;    // Has been assigned a target
        public bool Locked;       // Has reached target
        public double Brightness; // For visual effects (0.0 - 1.0)
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
        yield return ctx.Layer(RenderParticles);
    }
    
    private static void Initialize(int width, int height, Random random)
    {
        _width = width;
        _height = height;
        _dotWidth = width * DotsPerCellX;
        _dotHeight = height * DotsPerCellY;
        _random = random;
        
        // Discover shape images
        _shapePaths = DiscoverShapes();
        _currentShapeIndex = 0;
        
        // Initialize particles - start with pool at bottom
        int particleCount = _dotWidth * _dotHeight / 6; // ~17% coverage max
        _particles = new List<Particle>(particleCount);
        
        for (int i = 0; i < particleCount; i++)
        {
            _particles.Add(new Particle
            {
                X = random.NextDouble() * _dotWidth,
                Y = _dotHeight - 1 - random.NextDouble() * 4,
                Vx = 0,
                Vy = 0,
                HasTarget = false,
                Locked = false,
                Brightness = 0.6 + random.NextDouble() * 0.4
            });
        }
        
        // Load first shape
        LoadCurrentShape();
        AssignTargets();
        
        _phase = SimulationPhase.Rising;
        _phaseFrameCount = 0;
        _initialized = true;
    }
    
    private static List<string> DiscoverShapes()
    {
        var shapes = new List<string>();
        
        // Look for shape images in wwwroot/shapes/
        var baseDir = AppContext.BaseDirectory;
        var shapesDir = Path.Combine(baseDir, "wwwroot", "shapes");
        
        if (Directory.Exists(shapesDir))
        {
            var extensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" };
            foreach (var ext in extensions)
            {
                shapes.AddRange(Directory.GetFiles(shapesDir, ext));
            }
        }
        
        // If no shapes found, we'll use a fallback procedural shape
        return shapes;
    }
    
    private static void LoadCurrentShape()
    {
        _targetShape = new bool[_dotWidth, _dotHeight];
        _targetOccupied = new bool[_dotWidth, _dotHeight];
        
        if (_shapePaths != null && _shapePaths.Count > 0)
        {
            // Try to load from file
            var shapePath = _shapePaths[_currentShapeIndex % _shapePaths.Count];
            if (TryLoadShapeFromFile(shapePath))
            {
                return;
            }
        }
        
        // Use procedural shapes - we have 5 built-in shapes
        int proceduralIndex = _currentShapeIndex % 5;
        switch (proceduralIndex)
        {
            case 0:
                CreateProceduralHeart();
                break;
            case 1:
                CreateProceduralStar();
                break;
            case 2:
                CreateProceduralCircle();
                break;
            case 3:
                CreateProceduralTree();
                break;
            case 4:
                CreateProceduralRocket();
                break;
        }
    }
    
    private static bool TryLoadShapeFromFile(string shapePath)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(shapePath);
            if (bitmap == null) return false;
            
            // Scale bitmap to dot dimensions
            using var scaled = bitmap.Resize(new SKImageInfo(_dotWidth, _dotHeight), SKSamplingOptions.Default);
            if (scaled == null) return false;
            
            // Convert to monochrome target (dark pixels = target)
            for (int y = 0; y < _dotHeight; y++)
            {
                for (int x = 0; x < _dotWidth; x++)
                {
                    var pixel = scaled.GetPixel(x, y);
                    double gray = (pixel.Red + pixel.Green + pixel.Blue) / 3.0;
                    _targetShape![x, y] = gray < 128 && pixel.Alpha > 128;
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private static void CreateProceduralHeart()
    {
        // Procedural heart shape
        double centerX = _dotWidth / 2.0;
        double centerY = _dotHeight / 2.0;
        double scale = Math.Min(_dotWidth, _dotHeight) / 3.0;
        
        for (int y = 0; y < _dotHeight; y++)
        {
            for (int x = 0; x < _dotWidth; x++)
            {
                // Normalize coordinates to -1..1 range
                double nx = (x - centerX) / scale;
                double ny = (y - centerY) / scale;
                
                // Heart equation: (x² + y² - 1)³ - x²y³ < 0
                double eq = Math.Pow(nx * nx + ny * ny - 1, 3) - nx * nx * Math.Pow(ny, 3);
                _targetShape![x, y] = eq < 0;
            }
        }
    }
    
    private static void CreateProceduralStar()
    {
        // 5-pointed star
        double centerX = _dotWidth / 2.0;
        double centerY = _dotHeight / 2.0;
        double outerRadius = Math.Min(_dotWidth, _dotHeight) / 2.5;
        double innerRadius = outerRadius * 0.4;
        
        for (int y = 0; y < _dotHeight; y++)
        {
            for (int x = 0; x < _dotWidth; x++)
            {
                double dx = x - centerX;
                double dy = y - centerY;
                double angle = Math.Atan2(dy, dx);
                double dist = Math.Sqrt(dx * dx + dy * dy);
                
                // Create 5 points
                double starAngle = ((angle + Math.PI) / (2 * Math.PI)) * 5;
                double frac = starAngle - Math.Floor(starAngle);
                
                // Interpolate between inner and outer radius
                double radiusAtAngle;
                if (frac < 0.5)
                {
                    radiusAtAngle = innerRadius + (outerRadius - innerRadius) * (1 - frac * 2);
                }
                else
                {
                    radiusAtAngle = innerRadius + (outerRadius - innerRadius) * ((frac - 0.5) * 2);
                }
                
                _targetShape![x, y] = dist < radiusAtAngle;
            }
        }
    }
    
    private static void CreateProceduralCircle()
    {
        // Simple filled circle
        double centerX = _dotWidth / 2.0;
        double centerY = _dotHeight / 2.0;
        double radius = Math.Min(_dotWidth, _dotHeight) / 2.5;
        
        for (int y = 0; y < _dotHeight; y++)
        {
            for (int x = 0; x < _dotWidth; x++)
            {
                double dx = x - centerX;
                double dy = y - centerY;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                _targetShape![x, y] = dist < radius;
            }
        }
    }
    
    private static void CreateProceduralTree()
    {
        // Stylized tree (triangle + trunk)
        double centerX = _dotWidth / 2.0;
        double treeWidth = _dotWidth * 0.7;
        double treeTop = _dotHeight * 0.1;
        double treeBottom = _dotHeight * 0.75;
        double trunkTop = _dotHeight * 0.72;
        double trunkBottom = _dotHeight * 0.95;
        double trunkWidth = _dotWidth * 0.15;
        
        for (int y = 0; y < _dotHeight; y++)
        {
            for (int x = 0; x < _dotWidth; x++)
            {
                // Check trunk
                if (y >= trunkTop && y <= trunkBottom)
                {
                    if (Math.Abs(x - centerX) < trunkWidth / 2)
                    {
                        _targetShape![x, y] = true;
                        continue;
                    }
                }
                
                // Check tree triangle
                if (y >= treeTop && y < treeBottom)
                {
                    double progress = (y - treeTop) / (treeBottom - treeTop);
                    double widthAtY = treeWidth * progress;
                    if (Math.Abs(x - centerX) < widthAtY / 2)
                    {
                        _targetShape![x, y] = true;
                        continue;
                    }
                }
                
                _targetShape![x, y] = false;
            }
        }
    }
    
    private static void CreateProceduralRocket()
    {
        // Stylized rocket (body + fins + nose cone)
        double centerX = _dotWidth / 2.0;
        double rocketTop = _dotHeight * 0.05;
        double noseBottom = _dotHeight * 0.25;
        double bodyBottom = _dotHeight * 0.75;
        double finBottom = _dotHeight * 0.9;
        double bodyWidth = _dotWidth * 0.3;
        double finWidth = _dotWidth * 0.5;
        
        for (int y = 0; y < _dotHeight; y++)
        {
            for (int x = 0; x < _dotWidth; x++)
            {
                // Nose cone (triangle pointing up)
                if (y >= rocketTop && y < noseBottom)
                {
                    double progress = (y - rocketTop) / (noseBottom - rocketTop);
                    double widthAtY = bodyWidth * progress;
                    if (Math.Abs(x - centerX) < widthAtY / 2)
                    {
                        _targetShape![x, y] = true;
                        continue;
                    }
                }
                
                // Body (rectangle)
                if (y >= noseBottom && y < bodyBottom)
                {
                    if (Math.Abs(x - centerX) < bodyWidth / 2)
                    {
                        _targetShape![x, y] = true;
                        continue;
                    }
                }
                
                // Fins (trapezoid)
                if (y >= bodyBottom && y < finBottom)
                {
                    double progress = (y - bodyBottom) / (finBottom - bodyBottom);
                    double widthAtY = bodyWidth + (finWidth - bodyWidth) * progress;
                    if (Math.Abs(x - centerX) < widthAtY / 2)
                    {
                        _targetShape![x, y] = true;
                        continue;
                    }
                }
                
                _targetShape![x, y] = false;
            }
        }
    }
    
    private static void AssignTargets()
    {
        if (_particles == null || _targetShape == null || _targetOccupied == null) return;
        
        // Collect all target positions
        var targetPositions = new List<(int x, int y)>();
        for (int y = 0; y < _dotHeight; y++)
        {
            for (int x = 0; x < _dotWidth; x++)
            {
                if (_targetShape[x, y])
                {
                    targetPositions.Add((x, y));
                }
            }
        }
        
        // Shuffle targets
        for (int i = targetPositions.Count - 1; i > 0; i--)
        {
            int j = _random!.Next(i + 1);
            (targetPositions[i], targetPositions[j]) = (targetPositions[j], targetPositions[i]);
        }
        
        // Clear all assignments
        for (int i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            p.HasTarget = false;
            p.Locked = false;
            _particles[i] = p;
        }
        
        // Clear occupied array
        for (int y = 0; y < _dotHeight; y++)
        {
            for (int x = 0; x < _dotWidth; x++)
            {
                _targetOccupied[x, y] = false;
            }
        }
        
        // Assign targets to particles (1:1)
        int assignCount = Math.Min(_particles.Count, targetPositions.Count);
        for (int i = 0; i < assignCount; i++)
        {
            var p = _particles[i];
            var target = targetPositions[i];
            p.TargetX = target.x;
            p.TargetY = target.y;
            p.HasTarget = true;
            _targetOccupied[target.x, target.y] = true;
            _particles[i] = p;
        }
    }
    
    private static void Update()
    {
        if (_particles == null || _random == null) return;
        
        _phaseFrameCount++;
        
        switch (_phase)
        {
            case SimulationPhase.Rising:
                UpdateRising();
                // Check if all targeted particles are locked
                if (AllParticlesLocked())
                {
                    _phase = SimulationPhase.Holding;
                    _phaseFrameCount = 0;
                }
                break;
                
            case SimulationPhase.Holding:
                // Particles just hold position
                if (_phaseFrameCount >= HoldDurationFrames)
                {
                    _phase = SimulationPhase.Dissolving;
                    _phaseFrameCount = 0;
                    ReleaseParticles();
                }
                break;
                
            case SimulationPhase.Dissolving:
                UpdateDissolving();
                if (_phaseFrameCount >= DissolveFrames && AllParticlesSettled())
                {
                    _phase = SimulationPhase.Reforming;
                    _phaseFrameCount = 0;
                }
                break;
                
            case SimulationPhase.Reforming:
                if (_phaseFrameCount >= ReformDelayFrames)
                {
                    // Move to next shape
                    _currentShapeIndex++;
                    LoadCurrentShape();
                    AssignTargets();
                    _phase = SimulationPhase.Rising;
                    _phaseFrameCount = 0;
                }
                break;
        }
    }
    
    private static void UpdateRising()
    {
        for (int i = 0; i < _particles!.Count; i++)
        {
            var p = _particles[i];
            
            if (p.Locked) continue;
            
            if (p.HasTarget)
            {
                // Move toward target
                double dx = p.TargetX - p.X;
                double dy = p.TargetY - p.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                
                if (dist < LockDistance)
                {
                    // Snap to target
                    p.X = p.TargetX;
                    p.Y = p.TargetY;
                    p.Vx = 0;
                    p.Vy = 0;
                    p.Locked = true;
                }
                else
                {
                    // Apply attraction toward target
                    p.Vx += (dx / dist) * AttractionStrength;
                    p.Vy += (dy / dist) * AttractionStrength;
                    
                    // Add some upward bias
                    if (p.Y > p.TargetY)
                    {
                        p.Vy -= RiseSpeed * 0.1;
                    }
                    
                    // Apply turbulence
                    p.Vx += (_random!.NextDouble() - 0.5) * Turbulence;
                    p.Vy += (_random.NextDouble() - 0.5) * Turbulence * 0.5;
                    
                    // Damping
                    p.Vx *= 0.92;
                    p.Vy *= 0.92;
                    
                    // Update position
                    p.X += p.Vx;
                    p.Y += p.Vy;
                }
            }
            else
            {
                // Particles without targets just drift at bottom
                p.Vx += (_random!.NextDouble() - 0.5) * Turbulence * 0.3;
                p.Vx *= 0.9;
                p.X += p.Vx;
                
                // Keep at bottom
                p.Y = Math.Max(p.Y, _dotHeight - 4);
            }
            
            // Clamp to bounds
            p.X = Math.Clamp(p.X, 0, _dotWidth - 1);
            p.Y = Math.Clamp(p.Y, 0, _dotHeight - 1);
            
            _particles[i] = p;
        }
    }
    
    private static void ReleaseParticles()
    {
        for (int i = 0; i < _particles!.Count; i++)
        {
            var p = _particles[i];
            p.Locked = false;
            p.HasTarget = false;
            // Give a small random initial velocity
            p.Vx = (_random!.NextDouble() - 0.5) * 0.5;
            p.Vy = _random.NextDouble() * 0.3;
            _particles[i] = p;
        }
    }
    
    private static void UpdateDissolving()
    {
        for (int i = 0; i < _particles!.Count; i++)
        {
            var p = _particles[i];
            
            // Apply gravity
            p.Vy += GravitySpeed;
            
            // Apply some horizontal drift
            p.Vx += (_random!.NextDouble() - 0.5) * Turbulence * 0.5;
            
            // Damping
            p.Vx *= 0.95;
            p.Vy *= 0.98;
            
            // Update position
            p.X += p.Vx;
            p.Y += p.Vy;
            
            // Bounce off bottom
            if (p.Y >= _dotHeight - 1)
            {
                p.Y = _dotHeight - 1;
                p.Vy = -p.Vy * 0.3; // Inelastic bounce
                if (Math.Abs(p.Vy) < 0.1) p.Vy = 0;
            }
            
            // Wrap horizontally
            if (p.X < 0) p.X += _dotWidth;
            if (p.X >= _dotWidth) p.X -= _dotWidth;
            
            _particles[i] = p;
        }
    }
    
    private static bool AllParticlesLocked()
    {
        if (_particles == null) return true;
        
        foreach (var p in _particles)
        {
            if (p.HasTarget && !p.Locked) return false;
        }
        return true;
    }
    
    private static bool AllParticlesSettled()
    {
        if (_particles == null) return true;
        
        foreach (var p in _particles)
        {
            // Check if particle is near bottom and not moving much
            if (p.Y < _dotHeight - 5 || Math.Abs(p.Vy) > 0.5) return false;
        }
        return true;
    }
    
    private static void RenderBackground(Surface surface)
    {
        // Dark background with subtle gradient
        for (int y = 0; y < surface.Height; y++)
        {
            for (int x = 0; x < surface.Width; x++)
            {
                double gradient = (double)y / surface.Height;
                byte r = (byte)(5 + gradient * 10);
                byte g = (byte)(8 + gradient * 12);
                byte b = (byte)(15 + gradient * 15);
                
                surface[x, y] = new SurfaceCell(" ", null, Hex1bColor.FromRgb(r, g, b));
            }
        }
    }
    
    private static void RenderParticles(Surface surface)
    {
        if (_particles == null) return;
        
        // Group particles by cell for braille rendering
        var particlesByCell = new Dictionary<(int, int), (int bits, double maxBright)>();
        
        foreach (var p in _particles)
        {
            int dotX = (int)Math.Round(p.X);
            int dotY = (int)Math.Round(p.Y);
            
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
            var current = particlesByCell.GetValueOrDefault(key, (0, 0.0));
            particlesByCell[key] = (current.Item1 | bit, Math.Max(current.Item2, p.Brightness));
        }
        
        // Render braille characters
        foreach (var kvp in particlesByCell)
        {
            var (cellX, cellY) = kvp.Key;
            var (bits, maxBright) = kvp.Value;
            
            var ch = (char)(0x2800 + bits);
            var existing = surface[cellX, cellY];
            
            // Color based on phase and brightness
            Hex1bColor particleColor;
            
            switch (_phase)
            {
                case SimulationPhase.Rising:
                    // Cyan-white gradient during rise
                    byte riseR = (byte)(100 + maxBright * 155);
                    byte riseG = (byte)(200 + maxBright * 55);
                    byte riseB = (byte)(230 + maxBright * 25);
                    particleColor = Hex1bColor.FromRgb(riseR, riseG, riseB);
                    break;
                    
                case SimulationPhase.Holding:
                    // Bright white when formed
                    byte holdV = (byte)(230 + maxBright * 25);
                    particleColor = Hex1bColor.FromRgb(holdV, holdV, holdV);
                    break;
                    
                case SimulationPhase.Dissolving:
                    // Fade to orange during dissolve
                    double dissolveProgress = Math.Min(1.0, _phaseFrameCount / (double)DissolveFrames);
                    byte dissR = (byte)(255 * (0.9 + dissolveProgress * 0.1));
                    byte dissG = (byte)(255 * (0.9 - dissolveProgress * 0.4));
                    byte dissB = (byte)(255 * (0.9 - dissolveProgress * 0.6));
                    particleColor = Hex1bColor.FromRgb(dissR, dissG, dissB);
                    break;
                    
                default:
                    // Dim during reform wait
                    byte dimV = (byte)(80 + maxBright * 40);
                    particleColor = Hex1bColor.FromRgb(dimV, dimV, dimV);
                    break;
            }
            
            surface[cellX, cellY] = new SurfaceCell(
                ch.ToString(),
                particleColor,
                existing.Background
            );
        }
    }
}
