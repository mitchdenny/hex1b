/// <summary>
/// A single mote of the dust cloud, tracked in pixel space.
/// </summary>
/// <remarks>
/// Position and velocity are pixel-space doubles rather than cell coordinates so a
/// mote can move by less than one cell per frame. The cloud only converts to pixel
/// indices when it rasterises, which is what makes this demo exercise graphics that
/// cross cell boundaries.
/// </remarks>
internal sealed class DustMote
{
    public double X;
    public double Y;
    public double VelocityX;
    public double VelocityY;

    /// <summary>
    /// The palette entry this mote paints with, recomputed from its speed each frame.
    /// </summary>
    public int ColorIndex;

    /// <summary>The mote's current speed, in pixels per second.</summary>
    public double Speed => Math.Sqrt((VelocityX * VelocityX) + (VelocityY * VelocityY));
}

/// <summary>
/// Simulates a haze of motes orbiting a strong central attractor, with an optional
/// weaker attractor that follows the mouse pointer.
/// </summary>
/// <remarks>
/// <para>
/// Gravity is inverse-square and softened near the centre, so pull grows sharply as a
/// mote falls inward and fades toward the edges. That gradient is what lets motes
/// keep orbiting instead of collapsing: a constant inward acceleration removes the
/// same amount of outward energy on every pass regardless of distance, which
/// inevitably drains the cloud into a single point.
/// </para>
/// <para>
/// Motes are launched onto near-circular orbits rather than in purely random
/// directions, and damping is applied only in the outer field. Damping everywhere is
/// the other half of the collapse problem, because it bleeds the orbital energy that
/// keeps a mote clear of the centre.
/// </para>
/// <para>
/// The simulation is pure state; it never emits bytes. <see cref="SixelCloudRenderer"/>
/// converts a frame of motes into raw DCS sequences, keeping the physics independent
/// of how the cloud is painted.
/// </para>
/// </remarks>
internal sealed class DustCloud
{
    // Gravitational strength, in pixel^3/second^2. Tuned so a mote at mid-field
    // completes an orbit in a handful of seconds at the default frame rate.
    private const double CentralGravity = 5_600_000.0;

    // The mouse attractor is deliberately much weaker than the centre so it bends
    // orbits into visible swirls without capturing the whole cloud.
    private const double PointerGravity = 1_600_000.0;

    // Softening length. Without it, acceleration goes to infinity at r=0 and motes
    // get catapulted off the field in a single step.
    private const double SofteningPixels = 44.0;

    // Speed cap, as a multiple of the local circular-orbit speed. Motes that slingshot
    // through the centre would otherwise leave and never return.
    private const double MaxSpeedFactor = 1.85;

    // Damping applies only well outside the field radius, so it recaptures genuine
    // escapees in the corners without bleeding energy from ordinary wide orbits.
    // Damping the whole outer field instead empties it: every mote that ventures out
    // loses energy and never returns, and the cloud shrinks to a middle band.
    private const double OuterDampingRadiusFraction = 1.15;
    private const double OuterDamping = 0.992;

    // Orbits are launched close to circular. A small spread keeps the cloud from
    // looking like a single rigid ring.
    private const double MinOrbitFactor = 0.86;
    private const double MaxOrbitFactor = 1.12;

    // A mote is "arrived" when it falls inside this radius, at which point it is
    // relaunched. A strict centre test would essentially never fire.
    private const double CentreRadiusPixels = 10.0;

    // The speed window the colour ramp spans, as multiples of the circular-orbit
    // speed at the field radius. The window is wider than the typical orbit spread
    // so that both ends of the palette are actually reached.
    private const double HeatColdSpeedFactor = 0.45;
    private const double HeatHotSpeedFactor = 1.85;

    private readonly Random _random;
    private readonly List<DustMote> _motes = [];

    private double _centreX;
    private double _centreY;
    private double _fieldRadius;

    private double _pointerX;
    private double _pointerY;
    private bool _pointerActive;

    public DustCloud(int seed) => _random = new Random(seed);

    public IReadOnlyList<DustMote> Motes => _motes;

    public int PixelWidth { get; private set; }

    public int PixelHeight { get; private set; }

    /// <summary>
    /// Resizes the simulation field and repopulates it with <paramref name="moteCount"/> motes.
    /// </summary>
    public void Reset(int pixelWidth, int pixelHeight, int moteCount)
    {
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        _centreX = pixelWidth / 2.0;
        _centreY = pixelHeight / 2.0;
        _fieldRadius = Math.Max(1.0, Math.Min(_centreX, _centreY));

        _pointerActive = false;

        _motes.Clear();
        for (var index = 0; index < moteCount; index++)
        {
            var mote = new DustMote();
            PlaceOnOrbit(mote);
            _motes.Add(mote);
        }
    }

    /// <summary>
    /// Records the current pointer position, in pixel space, as a secondary attractor.
    /// </summary>
    public void SetPointer(double pixelX, double pixelY)
    {
        _pointerX = pixelX;
        _pointerY = pixelY;
        _pointerActive = true;
    }

    /// <summary>Removes the pointer attractor, for example when the pointer leaves.</summary>
    public void ClearPointer() => _pointerActive = false;

    /// <summary>
    /// Advances every mote by <paramref name="deltaSeconds"/>.
    /// </summary>
    public void Advance(double deltaSeconds)
    {
        foreach (var mote in _motes)
        {
            var offsetX = mote.X - _centreX;
            var offsetY = mote.Y - _centreY;
            var distance = Math.Sqrt((offsetX * offsetX) + (offsetY * offsetY));

            if (distance <= CentreRadiusPixels)
            {
                // Reaching the centre is the relaunch trigger, as requested: the mote
                // is fired back out onto a fresh orbit in a new random direction.
                PlaceOnOrbit(mote);
                continue;
            }

            ApplyGravity(mote, _centreX, _centreY, CentralGravity, deltaSeconds);

            if (_pointerActive)
            {
                ApplyGravity(mote, _pointerX, _pointerY, PointerGravity, deltaSeconds);
            }

            if (distance > _fieldRadius * OuterDampingRadiusFraction)
            {
                mote.VelocityX *= OuterDamping;
                mote.VelocityY *= OuterDamping;
            }

            ClampSpeed(mote, distance);

            mote.X += mote.VelocityX * deltaSeconds;
            mote.Y += mote.VelocityY * deltaSeconds;

            ReflectAtEdges(mote);
            UpdateHeat(mote);
        }
    }

    /// <summary>
    /// Recolours a mote from its speed, so fast motes read hot and slow motes cold.
    /// </summary>
    /// <remarks>
    /// The ramp is anchored to the circular-orbit speed at the field radius, which
    /// tracks the field size and so keeps the colouring stable across resizes. The
    /// window deliberately spans well below and above that speed: wide slow orbits
    /// sit under it and fast centre passes sit over it, so the full ramp is used.
    /// Normalising against each mote's own local circular speed would instead paint
    /// every orbit the same colour, erasing the gradient this is meant to show.
    /// </remarks>
    private void UpdateHeat(DustMote mote)
    {
        var reference = CircularSpeed(_fieldRadius);
        var coldest = reference * HeatColdSpeedFactor;
        var hottest = reference * HeatHotSpeedFactor;
        var heat = (mote.Speed - coldest) / (hottest - coldest);
        mote.ColorIndex = SixelCloudPalette.IndexForHeat(heat);
    }

    private void ApplyGravity(DustMote mote, double sourceX, double sourceY, double strength, double deltaSeconds)
    {
        var towardX = sourceX - mote.X;
        var towardY = sourceY - mote.Y;
        var distanceSquared = (towardX * towardX) + (towardY * towardY);
        var softened = distanceSquared + (SofteningPixels * SofteningPixels);
        var distance = Math.Sqrt(distanceSquared);
        if (distance < 1e-6)
        {
            return;
        }

        // Inverse-square magnitude, projected onto the unit vector toward the source.
        var acceleration = strength / softened;
        mote.VelocityX += towardX / distance * acceleration * deltaSeconds;
        mote.VelocityY += towardY / distance * acceleration * deltaSeconds;
    }

    private static void ClampSpeed(DustMote mote, double distance)
    {
        var circular = CircularSpeed(distance);
        var maximum = circular * MaxSpeedFactor;
        var speed = Math.Sqrt((mote.VelocityX * mote.VelocityX) + (mote.VelocityY * mote.VelocityY));
        if (speed <= maximum || speed < 1e-6)
        {
            return;
        }

        var scale = maximum / speed;
        mote.VelocityX *= scale;
        mote.VelocityY *= scale;
    }

    private static double CircularSpeed(double distance)
    {
        var softened = (distance * distance) + (SofteningPixels * SofteningPixels);
        return Math.Sqrt(CentralGravity * distance / softened);
    }

    private void PlaceOnOrbit(DustMote mote)
    {
        var angle = _random.NextDouble() * Math.PI * 2.0;

        // Square-rooting a uniform sample spreads motes evenly over the disc area
        // rather than bunching them near the centre.
        var radius = (0.18 + (0.82 * Math.Sqrt(_random.NextDouble()))) * _fieldRadius;

        mote.X = _centreX + (Math.Cos(angle) * radius);
        mote.Y = _centreY + (Math.Sin(angle) * radius);

        // Velocity perpendicular to the radius produces an orbit. The direction is
        // chosen at random so the cloud counter-rotates against itself.
        var direction = _random.Next(2) == 0 ? 1.0 : -1.0;
        var speed = CircularSpeed(radius)
            * (MinOrbitFactor + (_random.NextDouble() * (MaxOrbitFactor - MinOrbitFactor)));

        mote.VelocityX = -Math.Sin(angle) * speed * direction;
        mote.VelocityY = Math.Cos(angle) * speed * direction;

        UpdateHeat(mote);
    }

    private void ReflectAtEdges(DustMote mote)
    {
        // A mote that escapes the field would coast forever out of sight, so the
        // edges reflect it back into the visible cloud.
        if (mote.X < 0)
        {
            mote.X = 0;
            mote.VelocityX = Math.Abs(mote.VelocityX);
        }
        else if (mote.X > PixelWidth - 1)
        {
            mote.X = PixelWidth - 1;
            mote.VelocityX = -Math.Abs(mote.VelocityX);
        }

        if (mote.Y < 0)
        {
            mote.Y = 0;
            mote.VelocityY = Math.Abs(mote.VelocityY);
        }
        else if (mote.Y > PixelHeight - 1)
        {
            mote.Y = PixelHeight - 1;
            mote.VelocityY = -Math.Abs(mote.VelocityY);
        }
    }
}
