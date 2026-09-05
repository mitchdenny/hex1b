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

    /// <summary>
    /// Seconds of reduced mass remaining after this mote was caught in a collapse.
    /// </summary>
    /// <remarks>
    /// While venting, a mote both pulls less and counts for less toward local density.
    /// The second half matters more than the first: if venting motes still counted at
    /// full mass, the cell they are escaping would immediately re-trigger and trap
    /// them, which is the failure this whole mechanism exists to avoid.
    /// </remarks>
    public double VentSecondsRemaining;

    /// <summary>Whether this mote is currently in the reduced-mass vent state.</summary>
    public bool IsVenting => VentSecondsRemaining > 0.0;

    /// <summary>The mote's gravitational mass, reduced while venting.</summary>
    public double Mass => IsVenting ? DustCloud.VentingMass : 1.0;

    /// <summary>The mote's current speed, in pixels per second.</summary>
    public double Speed => Math.Sqrt((VelocityX * VelocityX) + (VelocityY * VelocityY));
}

/// <summary>
/// Simulates a haze of mutually attracting motes, with an optional attractor that
/// follows the mouse pointer.
/// </summary>
/// <remarks>
/// <para>
/// There is no central attractor. Structure is not imposed on the cloud; it emerges,
/// because the only sustained force is motes pulling on each other. Motes start spread
/// across the field drifting slowly, and gravity wells form wherever the initial
/// scatter happens to be slightly denser.
/// </para>
/// <para>
/// Mote-on-mote attraction is approximated on a uniform grid: each occupied cell is
/// treated as one aggregate mass at its centre of mass, so cost is motes times
/// occupied cells rather than motes squared.
/// </para>
/// <para>
/// Clumping left alone is terminal, because a clump's pull grows as it gathers and
/// nothing disperses it. So a grid cell that passes a mass threshold collapses: its
/// well is removed for that step, its motes drop to a reduced mass for a short window,
/// and they are kicked outward from the cell's centre of mass. Reduced mass is what
/// lets them escape without instantly forming a fresh well where they land, and the
/// kick is what makes a collapse read as a burst rather than a clump quietly ceasing
/// to grow. The result is a cycle: gather, clump, detonate, disperse, gather again.
/// </para>
/// <para>
/// Global damping bleeds a little speed every step. With a central well this would be
/// fatal, since it drains the orbital energy that keeps motes clear of the centre, but
/// without one there are no orbits to preserve: damping instead keeps drift slow
/// enough to watch and stops collapse kicks accumulating into a field of fast debris.
/// Containment comes from reflecting edges rather than from a central pull.
/// </para>
/// <para>
/// The simulation is pure state; it never emits bytes. An <see cref="ICloudRenderer"/>
/// converts a frame of motes into raw escape sequences, keeping the physics
/// independent of how the cloud is painted — as Sixel rasters or as KGP placements.
/// </para>
/// </remarks>
internal sealed class DustCloud
{
    // The mouse attractor. Without a central well this is the only externally imposed
    // force, so it is kept modest: enough to gather a clump under the pointer, not
    // enough to drag the whole cloud onto it.
    private const double PointerGravity = 3_000_000.0;

    // Softening length for the pointer. Without it, acceleration goes to infinity at
    // r=0 and motes get catapulted off the field in a single step.
    private const double SofteningPixels = 44.0;

    // Reference drift speed, as a fraction of field radius per second. Every other
    // speed in the simulation is expressed as a multiple of this. With no central
    // attractor there is no circular-orbit speed to derive a scale from, so the scale
    // is anchored to field size instead, which keeps motion proportionate on resize.
    private const double ReferenceSpeedFraction = 0.055;

    // Speed cap, as a multiple of the reference speed. Bounds the debris thrown by a
    // collapse so a burst cannot fling motes across the field in a few frames.
    private const double MaxSpeedFactor = 6.0;

    // Per-second damping factor, applied everywhere. This is what keeps the cloud slow
    // and readable, and stops collapse kicks accumulating over time.
    private const double GlobalDampingPerSecond = 0.25;

    // Initial speed spread, as multiples of the reference speed. Motes start drifting
    // slowly in random directions; the structure comes from gravity, not from launch.
    private const double MinStartSpeedFactor = 0.05;
    private const double MaxStartSpeedFactor = 0.35;

    // The speed window the colour ramp spans, as multiples of the reference speed.
    // The window is wider than typical drift so both ends of the palette are reached:
    // settled motes read cold, and motes freshly thrown by a collapse read hot.
    private const double HeatColdSpeedFactor = 0.1;
    private const double HeatHotSpeedFactor = 2.2;

    // Mote-on-mote gravity, in pixel^3/second^2 per unit mass. This is now the only
    // force that shapes the cloud, so it carries the structure that the central well
    // used to impose.
    private const double ClusterGravity = 26_000.0;

    // Softening for mote-on-mote attraction. Larger than the pointer softening because
    // motes get far closer to each other than they get to the pointer, and a hard
    // singularity between neighbours ejects both at implausible speed.
    private const double ClusterSofteningPixels = 26.0;

    // Side of a density grid cell, in pixels. This is both the many-body approximation
    // scale (a cell is treated as one aggregate mass at its centre of mass) and the
    // unit of density detection.
    private const double GridCellPixels = 34.0;

    // Mass in one grid cell that triggers collapse. Reached only where a genuine clump
    // has formed, not merely where the cloud is locally busy.
    private const double CollapseMassThreshold = 12.0;

    // How long a caught mote stays at reduced mass. Long enough to clear the cell that
    // trapped it, but short: a long window leaves a large standing fraction of the
    // cloud permanently venting, which flattens the gather-detonate cycle.
    private const double VentSeconds = 0.7;

    // Mass multiplier while venting. Not zero: venting motes still participate weakly,
    // so a collapse disperses rather than vanishing.
    internal const double VentingMass = 0.12;

    // Outward kick applied at collapse, as a multiple of the reference speed. This is
    // what actually breaks the clump apart; reduced mass alone only stops it growing.
    private const double CollapseKickFactor = 2.4;

    private readonly Random _random;
    private readonly List<DustMote> _motes = [];

    // Density grid, rebuilt each frame. Keyed by packed cell coordinate so the grid
    // costs memory proportional to occupied cells rather than to field area.
    private readonly Dictionary<long, GridCell> _grid = [];

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
            PlaceInField(mote);
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
    /// <remarks>
    /// Each step builds a density grid, collapses any cell that has grown too massive,
    /// then integrates under the pointer and the aggregated pull of every other
    /// occupied cell. There is no central attractor, so all structure comes from the
    /// motes themselves.
    /// </remarks>
    public void Advance(double deltaSeconds)
    {
        BuildGrid();
        CollapseDenseCells();

        // Frame-rate independent damping: applying a fixed factor per step would make
        // the cloud settle faster simply because frames arrived faster.
        var damping = Math.Pow(GlobalDampingPerSecond, deltaSeconds);

        foreach (var mote in _motes)
        {
            if (mote.VentSecondsRemaining > 0.0)
            {
                mote.VentSecondsRemaining = Math.Max(0.0, mote.VentSecondsRemaining - deltaSeconds);
            }

            if (_pointerActive)
            {
                ApplyGravity(mote, _pointerX, _pointerY, PointerGravity, deltaSeconds);
            }

            ApplyClusterGravity(mote, deltaSeconds);

            mote.VelocityX *= damping;
            mote.VelocityY *= damping;

            ClampSpeed(mote);

            mote.X += mote.VelocityX * deltaSeconds;
            mote.Y += mote.VelocityY * deltaSeconds;

            ReflectAtEdges(mote);
            UpdateHeat(mote);
        }
    }

    /// <summary>Number of grid cells currently holding at least one mote.</summary>
    internal int OccupiedCellCount => _grid.Count;

    /// <summary>Number of motes currently in the reduced-mass vent state.</summary>
    internal int VentingCount
    {
        get
        {
            var venting = 0;
            foreach (var mote in _motes)
            {
                if (mote.IsVenting)
                {
                    venting++;
                }
            }

            return venting;
        }
    }

    /// <summary>Bins every mote into the density grid and accumulates centre of mass.</summary>
    private void BuildGrid()
    {
        _grid.Clear();

        foreach (var mote in _motes)
        {
            var key = CellKey(mote.X, mote.Y);
            ref var cell = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(_grid, key, out _);

            var mass = mote.Mass;
            cell.Mass += mass;
            cell.WeightedX += mote.X * mass;
            cell.WeightedY += mote.Y * mass;
            cell.Count++;
        }
    }

    /// <summary>
    /// Detonates any grid cell whose mass has passed the collapse threshold.
    /// </summary>
    /// <remarks>
    /// The cell's gravity well is removed for this step by zeroing its aggregate mass,
    /// its motes drop to reduced mass so they neither hold each other nor immediately
    /// re-trigger the cell, and they are kicked outward from the cell's centre of mass.
    /// Without the kick a clump merely stops growing and sits there; the kick is what
    /// makes a collapse read as a burst.
    /// </remarks>
    private void CollapseDenseCells()
    {
        List<long>? collapsing = null;

        foreach (var (key, cell) in _grid)
        {
            if (cell.Mass >= CollapseMassThreshold)
            {
                (collapsing ??= []).Add(key);
            }
        }

        if (collapsing is null)
        {
            return;
        }

        // Motes are visited once and matched against the collapsing set, rather than
        // rescanning every mote per collapsing cell.
        foreach (var mote in _motes)
        {
            var key = CellKey(mote.X, mote.Y);
            if (!collapsing.Contains(key))
            {
                continue;
            }

            var cell = _grid[key];
            var comX = cell.WeightedX / cell.Mass;
            var comY = cell.WeightedY / cell.Mass;

            mote.VentSecondsRemaining = VentSeconds;

            var awayX = mote.X - comX;
            var awayY = mote.Y - comY;
            var length = Math.Sqrt((awayX * awayX) + (awayY * awayY));
            if (length < 1e-6)
            {
                // Exactly at the centre of mass, so there is no outward direction to
                // use. Any direction will do.
                var angle = _random.NextDouble() * Math.PI * 2.0;
                awayX = Math.Cos(angle);
                awayY = Math.Sin(angle);
                length = 1.0;
            }

            var kick = ReferenceSpeed * CollapseKickFactor;

            mote.VelocityX += awayX / length * kick;
            mote.VelocityY += awayY / length * kick;
        }

        // Remove the wells for this step so collapsed cells exert no pull while venting.
        foreach (var key in collapsing)
        {
            _grid[key] = new GridCell();
        }
    }

    /// <summary>
    /// Applies the aggregated pull of every occupied grid cell to one mote.
    /// </summary>
    /// <remarks>
    /// This is the many-body approximation: rather than summing over every other mote,
    /// which is quadratic, each cell is treated as a single mass at its centre of mass.
    /// Cost is motes times occupied cells, and the cell containing the mote is skipped
    /// so a mote never pulls on itself.
    /// </remarks>
    private void ApplyClusterGravity(DustMote mote, double deltaSeconds)
    {
        var ownKey = CellKey(mote.X, mote.Y);
        var scale = mote.IsVenting ? VentingMass : 1.0;

        foreach (var (key, cell) in _grid)
        {
            if (key == ownKey || cell.Mass <= 0.0)
            {
                continue;
            }

            var towardX = (cell.WeightedX / cell.Mass) - mote.X;
            var towardY = (cell.WeightedY / cell.Mass) - mote.Y;
            var distanceSquared = (towardX * towardX) + (towardY * towardY);
            var distance = Math.Sqrt(distanceSquared);
            if (distance < 1e-6)
            {
                continue;
            }

            var softened = distanceSquared + (ClusterSofteningPixels * ClusterSofteningPixels);
            var acceleration = ClusterGravity * cell.Mass * scale / softened;

            mote.VelocityX += towardX / distance * acceleration * deltaSeconds;
            mote.VelocityY += towardY / distance * acceleration * deltaSeconds;
        }
    }

    /// <summary>Packs a pixel position into a grid cell key.</summary>
    private static long CellKey(double x, double y)
    {
        var cellX = (long)Math.Floor(x / GridCellPixels);
        var cellY = (long)Math.Floor(y / GridCellPixels);
        return (cellX << 32) ^ (cellY & 0xFFFFFFFFL);
    }

    /// <summary>Aggregate mass and centre of mass for one grid cell.</summary>
    private struct GridCell
    {
        public double Mass;
        public double WeightedX;
        public double WeightedY;
        public int Count;
    }

    /// <summary>
    /// Recolours a mote from its speed, so fast motes read hot and slow motes cold.
    /// </summary>
    /// <remarks>
    /// The ramp is anchored to the reference drift speed, which tracks field size and
    /// so keeps colouring stable across resizes. The window deliberately spans well
    /// below and above that speed: settled motes sit under it and motes freshly thrown
    /// by a collapse sit over it, so the full ramp is used.
    /// </remarks>
    private void UpdateHeat(DustMote mote)
    {
        var reference = ReferenceSpeed;
        var coldest = reference * HeatColdSpeedFactor;
        var hottest = reference * HeatHotSpeedFactor;
        var heat = (mote.Speed - coldest) / (hottest - coldest);
        mote.ColorIndex = CloudPalette.IndexForHeat(heat);
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

    private void ClampSpeed(DustMote mote)
    {
        var maximum = ReferenceSpeed * MaxSpeedFactor;
        var speed = Math.Sqrt((mote.VelocityX * mote.VelocityX) + (mote.VelocityY * mote.VelocityY));
        if (speed <= maximum || speed < 1e-6)
        {
            return;
        }

        var scale = maximum / speed;
        mote.VelocityX *= scale;
        mote.VelocityY *= scale;
    }

    /// <summary>
    /// The speed scale every other speed in the simulation is expressed against, in
    /// pixels per second.
    /// </summary>
    /// <remarks>
    /// With no central attractor there is no circular-orbit speed to derive a scale
    /// from, so it is anchored to field size: motion stays visually proportionate when
    /// the terminal is resized rather than appearing faster in a smaller window.
    /// </remarks>
    private double ReferenceSpeed => _fieldRadius * ReferenceSpeedFraction;

    /// <summary>Scatters a mote across the field with a slow random drift.</summary>
    /// <remarks>
    /// Motes are placed uniformly rather than on orbits, and given only enough speed to
    /// drift. All structure is left to emerge from mutual gravity, so seeding
    /// deliberately imposes none.
    /// </remarks>
    private void PlaceInField(DustMote mote)
    {
        mote.X = _random.NextDouble() * Math.Max(1, PixelWidth - 1);
        mote.Y = _random.NextDouble() * Math.Max(1, PixelHeight - 1);

        var angle = _random.NextDouble() * Math.PI * 2.0;
        var speed = ReferenceSpeed
            * (MinStartSpeedFactor + (_random.NextDouble() * (MaxStartSpeedFactor - MinStartSpeedFactor)));

        mote.VelocityX = Math.Cos(angle) * speed;
        mote.VelocityY = Math.Sin(angle) * speed;
        mote.VentSecondsRemaining = 0.0;

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
