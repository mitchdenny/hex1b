using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace WindowingDemo;

/// <summary>
/// Fluid dynamics simulation inside a rotating container.
/// Uses braille characters for sub-cell fluid particles that slosh around
/// when the container rotates every ~10 seconds.
/// </summary>
public static class FluidDemo
{
    // Braille dimensions (2x4 dots per cell)
    private const int DotsPerCellX = 2;
    private const int DotsPerCellY = 4;

    // Container settings
    private const double ContainerSizeRatio = 0.6; // Container size relative to surface

    // Fluid physics - SPH-inspired for realistic liquid behavior
    private const int ParticleCount = 600;
    private const double Gravity = 0.18;              // Moderate gravity
    private const double Damping = 0.96;              // More aggressive velocity decay
    private const double WallBounce = 0.0;            // No bounce - water absorbs impact
    private const double WallFriction = 0.5;          // Strong friction along walls
    private const double RestDensity = 2.2;           // Higher rest density = tighter packing
    private const double Stiffness = 0.04;            // Gentler pressure response
    private const double NearStiffness = 0.08;        // Gentler close-range push
    private const double Viscosity = 0.15;            // Strong velocity averaging for cohesion
    private const double InteractionRadius = 3.0;     // How far particles interact
    private const double MaxVelocity = 2.5;           // Limit max speed

    // Rotation settings
    private const int FramesBetweenRotations = 200;   // ~10 seconds at 50ms/frame
    private const int RotationFrames = 40;            // Rotate over 40 frames (~2s)
    private const double RotationAngle = Math.PI / 2; // 90 degrees clockwise

    // State
    private static Particle[]? _particles;
    private static int _width;
    private static int _height;
    private static int _dotWidth;
    private static int _dotHeight;
    private static double _containerCenterX;
    private static double _containerCenterY;
    private static double _containerHalfSize;
    private static double _containerAngle;            // Current container rotation (radians)
    private static double _targetContainerAngle;      // Target during rotation animation
    private static double _angularVelocity;           // Current rotation speed (for centrifugal effects)
    private static int _framesSinceRotation;
    private static bool _isRotating;
    private static int _rotationFrame;
    private static bool _initialized;
    private static Random? _random;

    private struct Particle
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
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
        yield return ctx.Layer(RenderContainer);
        yield return ctx.Layer(RenderFluid);
    }

    private static void Initialize(int width, int height, Random random)
    {
        _width = width;
        _height = height;
        _dotWidth = width * DotsPerCellX;
        _dotHeight = height * DotsPerCellY;

        // Container centered in the surface
        _containerCenterX = _dotWidth / 2.0;
        _containerCenterY = _dotHeight / 2.0;
        _containerHalfSize = Math.Min(_dotWidth, _dotHeight) * ContainerSizeRatio / 2.0;

        // Initialize container angle (no rotation)
        _containerAngle = 0;
        _targetContainerAngle = 0;
        _angularVelocity = 0;
        _framesSinceRotation = 0;
        _isRotating = false;
        _rotationFrame = 0;

        // Initialize particles at bottom of container
        _particles = new Particle[ParticleCount];
        double floorY = _containerCenterY + _containerHalfSize - 2;
        double floorHeight = _containerHalfSize * 0.5; // Particles fill bottom ~50%

        for (int i = 0; i < ParticleCount; i++)
        {
            _particles[i] = new Particle
            {
                X = _containerCenterX + (random.NextDouble() * 2 - 1) * (_containerHalfSize - 3),
                Y = floorY - random.NextDouble() * floorHeight,
                Vx = 0,
                Vy = 0
            };
        }

        _initialized = true;
    }

    private static void Update()
    {
        if (_particles == null) return;

        // Handle rotation timing
        _framesSinceRotation++;
        double prevAngle = _containerAngle;

        if (!_isRotating && _framesSinceRotation >= FramesBetweenRotations)
        {
            // Start a new rotation
            _isRotating = true;
            _rotationFrame = 0;
            _targetContainerAngle = _containerAngle + RotationAngle; // 90 degrees clockwise
        }

        if (_isRotating)
        {
            _rotationFrame++;

            // Ease-in-out rotation animation
            double t = (double)_rotationFrame / RotationFrames;
            double eased = t < 0.5
                ? 4 * t * t * t
                : 1 - Math.Pow(-2 * t + 2, 3) / 2;

            double startAngle = _targetContainerAngle - RotationAngle;
            _containerAngle = startAngle + eased * RotationAngle;
            _angularVelocity = _containerAngle - prevAngle;

            if (_rotationFrame >= RotationFrames)
            {
                _isRotating = false;
                _containerAngle = _targetContainerAngle;
                _angularVelocity = 0;
                _framesSinceRotation = 0;
            }
        }
        else
        {
            _angularVelocity = 0;
        }

        // Gravity always points down in world space
        double gravityX = 0;
        double gravityY = Gravity;

        // First pass: calculate density at each particle location
        var densities = new double[_particles.Length];
        var nearDensities = new double[_particles.Length];

        for (int i = 0; i < _particles.Length; i++)
        {
            var p = _particles[i];
            double density = 0;
            double nearDensity = 0;

            for (int j = 0; j < _particles.Length; j++)
            {
                if (i == j) continue;

                double dx = _particles[j].X - p.X;
                double dy = _particles[j].Y - p.Y;
                double distSq = dx * dx + dy * dy;

                if (distSq < InteractionRadius * InteractionRadius)
                {
                    double dist = Math.Sqrt(distSq);
                    double q = 1 - dist / InteractionRadius;

                    // Density contribution (linear kernel)
                    density += q * q;
                    // Near-density for close-range repulsion (cubic kernel)
                    nearDensity += q * q * q;
                }
            }

            densities[i] = density;
            nearDensities[i] = nearDensity;
        }

        // Second pass: apply forces based on density (pressure) and viscosity
        for (int i = 0; i < _particles.Length; i++)
        {
            var p = _particles[i];

            // Apply gravity
            p.Vx += gravityX;
            p.Vy += gravityY;

            // Calculate pressure from density
            double pressure = Stiffness * (densities[i] - RestDensity);
            double nearPressure = NearStiffness * nearDensities[i];

            double forceX = 0;
            double forceY = 0;

            for (int j = 0; j < _particles.Length; j++)
            {
                if (i == j) continue;

                double dx = _particles[j].X - p.X;
                double dy = _particles[j].Y - p.Y;
                double distSq = dx * dx + dy * dy;

                if (distSq < InteractionRadius * InteractionRadius && distSq > 0.001)
                {
                    double dist = Math.Sqrt(distSq);
                    double q = 1 - dist / InteractionRadius;

                    // Pressure force (pushes apart when compressed)
                    double pressureJ = Stiffness * (densities[j] - RestDensity);
                    double sharedPressure = (pressure + pressureJ) / 2;
                    double nearPressureJ = NearStiffness * nearDensities[j];
                    double sharedNearPressure = (nearPressure + nearPressureJ) / 2;

                    double pressureForceMag = sharedPressure * q + sharedNearPressure * q * q;

                    // Direction from j to i (repulsion)
                    double nx = -dx / dist;
                    double ny = -dy / dist;

                    forceX += nx * pressureForceMag;
                    forceY += ny * pressureForceMag;

                    // Viscosity force (velocity averaging - creates cohesive flow)
                    double dvx = _particles[j].Vx - p.Vx;
                    double dvy = _particles[j].Vy - p.Vy;

                    forceX += dvx * Viscosity * q;
                    forceY += dvy * Viscosity * q;
                }
            }

            p.Vx += forceX;
            p.Vy += forceY;

            // Clamp velocity
            double speed = Math.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy);
            if (speed > MaxVelocity)
            {
                p.Vx = p.Vx / speed * MaxVelocity;
                p.Vy = p.Vy / speed * MaxVelocity;
            }

            // Apply damping
            p.Vx *= Damping;
            p.Vy *= Damping;

            // Update position
            p.X += p.Vx;
            p.Y += p.Vy;

            // Constrain to rotated container walls
            ConstrainToRotatedContainer(ref p);

            _particles[i] = p;
        }
    }

    /// <summary>
    /// Constrains a particle to stay inside the rotated square container.
    /// </summary>
    private static void ConstrainToRotatedContainer(ref Particle p)
    {
        // Transform particle position to container-local coordinates
        double relX = p.X - _containerCenterX;
        double relY = p.Y - _containerCenterY;

        double cos = Math.Cos(-_containerAngle);
        double sin = Math.Sin(-_containerAngle);

        double localX = relX * cos - relY * sin;
        double localY = relX * sin + relY * cos;

        // Also transform velocity to local space
        double localVx = p.Vx * cos - p.Vy * sin;
        double localVy = p.Vx * sin + p.Vy * cos;

        // Constrain in local space (axis-aligned box)
        double margin = _containerHalfSize - 1.5;
        bool hitWall = false;

        if (localX < -margin)
        {
            localX = -margin;
            localVx = -localVx * WallBounce;
            localVy *= WallFriction;
            hitWall = true;
        }
        else if (localX > margin)
        {
            localX = margin;
            localVx = -localVx * WallBounce;
            localVy *= WallFriction;
            hitWall = true;
        }

        if (localY < -margin)
        {
            localY = -margin;
            localVy = -localVy * WallBounce;
            localVx *= WallFriction;
            hitWall = true;
        }
        else if (localY > margin)
        {
            localY = margin;
            localVy = -localVy * WallBounce;
            localVx *= WallFriction;
            hitWall = true;
        }

        if (hitWall)
        {
            // Transform back to world space
            cos = Math.Cos(_containerAngle);
            sin = Math.Sin(_containerAngle);

            p.X = localX * cos - localY * sin + _containerCenterX;
            p.Y = localX * sin + localY * cos + _containerCenterY;

            p.Vx = localVx * cos - localVy * sin;
            p.Vy = localVx * sin + localVy * cos;
        }
    }

    private static void RenderBackground(Surface surface)
    {
        var bg = Hex1bColor.FromRgb(15, 15, 25);

        for (int y = 0; y < surface.Height; y++)
        {
            for (int x = 0; x < surface.Width; x++)
            {
                surface[x, y] = new SurfaceCell(" ", null, bg);
            }
        }
    }

    private static void RenderContainer(Surface surface)
    {
        // Draw rotated container outline using braille dots
        var containerCells = new Dictionary<(int, int), int>();
        var outlineColor = Hex1bColor.FromRgb(180, 180, 200);

        double cos = Math.Cos(_containerAngle);
        double sin = Math.Sin(_containerAngle);

        // Calculate rotated corners
        double hs = _containerHalfSize;
        (double x, double y)[] corners =
        [
            RotatePoint(-hs, -hs, cos, sin), // top-left
            RotatePoint(hs, -hs, cos, sin),  // top-right
            RotatePoint(hs, hs, cos, sin),   // bottom-right
            RotatePoint(-hs, hs, cos, sin),  // bottom-left
        ];

        // Draw edges between corners
        for (int i = 0; i < 4; i++)
        {
            var (x1, y1) = corners[i];
            var (x2, y2) = corners[(i + 1) % 4];

            // Translate to world coordinates
            x1 += _containerCenterX;
            y1 += _containerCenterY;
            x2 += _containerCenterX;
            y2 += _containerCenterY;

            // Draw line between corners using Bresenham-style stepping
            DrawBrailleLine(containerCells, x1, y1, x2, y2);
        }

        // Render container braille characters
        foreach (var (cell, bits) in containerCells)
        {
            int cellX = cell.Item1;
            int cellY = cell.Item2;

            if (cellX < 0 || cellX >= surface.Width || cellY < 0 || cellY >= surface.Height)
                continue;

            var ch = (char)(0x2800 + bits);
            var existing = surface[cellX, cellY];

            surface[cellX, cellY] = new SurfaceCell(
                ch.ToString(),
                outlineColor,
                existing.Background
            );
        }

        // Draw rotation indicator showing current angle
        int indicatorCellX = (int)(_containerCenterX / DotsPerCellX);
        int indicatorCellY = (int)((_containerCenterY - _containerHalfSize - 6) / DotsPerCellY);

        if (indicatorCellX >= 0 && indicatorCellX < surface.Width &&
            indicatorCellY >= 0 && indicatorCellY < surface.Height)
        {
            string indicator = _isRotating ? "⟳" : "◇";

            var indicatorColor = _isRotating
                ? Hex1bColor.FromRgb(255, 200, 100)
                : Hex1bColor.FromRgb(100, 100, 120);

            var existing = surface[indicatorCellX, indicatorCellY];
            surface[indicatorCellX, indicatorCellY] = new SurfaceCell(indicator, indicatorColor, existing.Background);
        }
    }

    private static (double x, double y) RotatePoint(double x, double y, double cos, double sin)
    {
        return (x * cos - y * sin, x * sin + y * cos);
    }

    private static void DrawBrailleLine(Dictionary<(int, int), int> cells, double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        double length = Math.Sqrt(dx * dx + dy * dy);

        if (length < 0.5) return;

        // Step along the line with sub-dot precision
        int steps = (int)(length * 1.5); // Ensure dense coverage
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            int dotX = (int)(x1 + dx * t);
            int dotY = (int)(y1 + dy * t);
            AddDotToCell(cells, dotX, dotY);
        }
    }

    private static void AddDotToCell(Dictionary<(int, int), int> cells, int dotX, int dotY)
    {
        int cellX = dotX / DotsPerCellX;
        int cellY = dotY / DotsPerCellY;
        int localX = dotX % DotsPerCellX;
        int localY = dotY % DotsPerCellY;

        // Handle negative coordinates
        if (localX < 0) localX += DotsPerCellX;
        if (localY < 0) localY += DotsPerCellY;

        int bit = localY switch
        {
            0 => localX == 0 ? 0x01 : 0x08,
            1 => localX == 0 ? 0x02 : 0x10,
            2 => localX == 0 ? 0x04 : 0x20,
            3 => localX == 0 ? 0x40 : 0x80,
            _ => 0
        };

        var key = (cellX, cellY);
        cells[key] = cells.GetValueOrDefault(key) | bit;
    }

    private static void RenderFluid(Surface surface)
    {
        if (_particles == null) return;

        // Group particles by cell
        var fluidCells = new Dictionary<(int, int), int>();

        // Various shades of blue for fluid
        var fluidColor = Hex1bColor.FromRgb(80, 150, 255);

        foreach (var p in _particles)
        {
            int dotX = (int)p.X;
            int dotY = (int)p.Y;

            if (dotX < 0 || dotX >= _dotWidth || dotY < 0 || dotY >= _dotHeight)
                continue;

            AddDotToCell(fluidCells, dotX, dotY);
        }

        // Render fluid braille characters
        foreach (var (cell, bits) in fluidCells)
        {
            int cellX = cell.Item1;
            int cellY = cell.Item2;

            if (cellX < 0 || cellX >= surface.Width || cellY < 0 || cellY >= surface.Height)
                continue;

            var existing = surface[cellX, cellY];

            // Merge with existing braille (container outline)
            int existingBits = 0;
            if (existing.Character.Length == 1 &&
                existing.Character[0] >= 0x2800 &&
                existing.Character[0] <= 0x28FF)
            {
                existingBits = existing.Character[0] - 0x2800;
            }

            int mergedBits = existingBits | bits;
            var ch = (char)(0x2800 + mergedBits);

            // Blend colors if there's existing content
            var finalColor = existingBits > 0 && existing.Foreground.HasValue
                ? BlendColors(existing.Foreground.Value, fluidColor)
                : fluidColor;

            surface[cellX, cellY] = new SurfaceCell(
                ch.ToString(),
                finalColor,
                existing.Background
            );
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
