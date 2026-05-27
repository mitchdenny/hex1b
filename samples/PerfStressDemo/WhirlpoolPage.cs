using Hex1b;
using Hex1b.Input;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace PerfStressDemo;

/// <summary>
/// Page 3 — "Whirlpool".
///
/// Top-down water simulated on a half-block grid (each terminal cell holds
/// two stacked sub-cells, rendered as <c>▀</c> with foreground = top
/// sub-cell colour and background = bottom sub-cell colour, doubling the
/// vertical resolution of the simulation).
///
/// Water has a per-sub-cell depth in [0, 1]. The whole screen starts at
/// full depth — a deep blue ocean. Pressure equalises between neighbours
/// each frame using mass-conserving diffusion, so disturbances spread
/// outward as flat waves of slightly different depth.
///
/// <list type="bullet">
///   <item>Left click — open a drain at the cursor. Water in a small disc
///         around the drain is consumed each frame; the surrounding water
///         flows in to fill the void.</item>
///   <item>Right click — close the drain. Edge inlets begin pulsing water
///         back into the basin at random positions; the basin gradually
///         refills. Once every sub-cell is at maximum depth no further
///         inlets spawn and the system goes idle.</item>
///   <item>Scroll — adjust drain strength (clamped). Scroll up = stronger.</item>
/// </list>
///
/// Colour: empty sub-cell = warm sandy beach; shallow = light blue;
/// full = deep ocean blue. Lerped continuously so a draining basin
/// reads as a colour gradient from beach (at the centre) to ocean (at
/// the edges).
///
/// Stress profile: two w·2h-sized passes per frame (diffusion delta
/// pass + apply pass) over a flat float[] grid, plus one cell write per
/// surface cell on the render pass. Exercises tight numeric loops on
/// large flat buffers — distinct from the scattered sparse writes of
/// the earlier particle prototype.
/// </summary>
internal sealed class WhirlpoolPage : IStressPage
{
    public string Name => "Whirlpool";

    public string Description =>
        "Half-block water field with mouse-controlled drain and edge inlets. "
        + "Left click opens a drain, right click closes and basin refills.";

    // ----------------------------------------------------------------
    // Simulation grid. _depth[y * dotWidth + x] holds the water depth
    // in [0, MaxDepth] for sub-cell (x, y). _delta is the change to
    // apply this step (computed in pass 1, applied in pass 2 so flow
    // is order-independent).
    // ----------------------------------------------------------------
    private float[] _depth = Array.Empty<float>();
    private float[] _delta = Array.Empty<float>();
    private int _surfaceWidth;
    private int _surfaceHeight;
    private int _dotWidth;
    private int _dotHeight;

    // ----------------------------------------------------------------
    // Drain state. _drainActive gates consumption; the well can be
    // placed (left click) or removed (right click) independently of
    // its position. Coords are in surface (cell) units — they're
    // doubled to sub-cell units when the drain is applied.
    // ----------------------------------------------------------------
    private bool _drainActive;
    private float _drainX;
    private float _drainY;
    private float _strength = 1.0f;
    private int _frame;
    private const float MinStrength = 0.3f;
    private const float MaxStrength = 4.0f;

    // ----------------------------------------------------------------
    // Surface bubbles. Modelled as PASSIVE tracers carried by the
    // velocity field — no momentum, no orbits, no escape. Real
    // surface particles in a fluid don't have inertia separate from
    // the fluid itself; the mass of the water behind them keeps
    // pushing them toward the sink. To simulate that we look up the
    // local potential-flow velocity each frame and step by it.
    // ----------------------------------------------------------------
    private struct Bubble
    {
        public float X;   // sub-cell coords
        public float Y;
    }
    private Bubble[] _bubbles = Array.Empty<Bubble>();
    private float[] _foam = Array.Empty<float>();
    private const float FoamDecay = 0.84f;             // per frame
    private const float BubbleMaxSpeed = 2.5f;         // sub-cells / frame (anti-overshoot)
    private const float BubbleAbsorbRadius = 1.6f;     // sub-cells

    // ----------------------------------------------------------------
    // Potential-flow velocity field. A 2D sink at the drain has
    // velocity v_r = -Q/r (inward); adding a free vortex of
    // circulation Γ gives v_θ = Γ/r (tangential). Streamlines are
    // logarithmic spirals at angle atan(Γ/Q) from the radial.
    // VelocitySoftening avoids the 1/0 singularity at the drain
    // centre and keeps the cell-skip rate manageable.
    // ----------------------------------------------------------------
    private const float SinkStrength = 9.0f;
    private const float SwirlStrength = 18.0f;         // Γ ~ 2Q → ~63° spirals
    private const float VelocitySoftening = 2.5f;

    // ----------------------------------------------------------------
    // Inlets — small periodically-spawned sources on the screen edge.
    // Active only when the drain is OFF and the basin isn't full;
    // each inlet pulses its flow with a sinusoidal envelope so they
    // visibly fade in and out at varying strengths instead of being
    // hard step functions.
    // ----------------------------------------------------------------
    private struct Inlet
    {
        public int X;            // sub-cell coords (dot units)
        public int Y;
        public float PeakRate;   // depth added per frame at envelope peak
        public int Age;          // frames since spawned
        public int Lifetime;     // total frames before removal
    }

    private readonly List<Inlet> _inlets = new(16);
    private int _framesUntilNextInletCheck;

    // ----------------------------------------------------------------
    // Physics constants.
    // ----------------------------------------------------------------
    private const float MaxDepth = 1.0f;
    // Diffusion rate per neighbour per frame. Kept small now that
    // advection is the primary transport — too much diffusion blurs
    // away the spiral contours that advection deliberately creates.
    private const float DiffusionK = 0.045f;
    private const float DrainRadius = 6.5f;       // sub-cell units
    private const float DrainPerFrameBase = 0.28f; // strength multiplier
    private const int   InletCheckInterval = 12;   // frames between spawn attempts
    private const int   InletMaxConcurrent = 6;
    private const int   InletLifetimeMin = 90;
    private const int   InletLifetimeMax = 220;
    private const float InletPeakRateMin = 0.015f;
    private const float InletPeakRateMax = 0.045f;
    private const float InletRadius = 2.5f;        // sub-cell units, spread of deposit

    // ----------------------------------------------------------------
    // Quiescence tracking — used by IsIdle so the framework can sleep
    // when the basin is completely full, the drain is off, and no
    // inlets are active. The diffusion delta sum is the cheapest
    // "anything moving?" detector available since we already compute
    // every cell's delta in the diffusion pass.
    // ----------------------------------------------------------------
    private bool _quiescent = true;
    public bool IsIdle => _quiescent;

    // Status-bar accessors.
    public static float CurrentStrength { get; private set; } = 1.0f;
    public static bool DrainOpen { get; private set; }

    // xorshift32 — small, fast, allocation-free, deterministic seed.
    private uint _rng = 0xDEADBEEFu;
    private uint NextRandom()
    {
        var x = _rng;
        x ^= x << 13; x ^= x >> 17; x ^= x << 5;
        _rng = x;
        return x;
    }
    private float NextFloat() => (NextRandom() & 0xFFFFFF) / (float)0x1000000;

    // Pre-computed colour endpoints. SurfaceCell stores Hex1bColor
    // values so we avoid allocating new ones in the hot render loop
    // by lerping bytes and constructing the colour once per cell.
    private static readonly (byte R, byte G, byte B) SandColour = (235, 215, 175); // warm beach
    private static readonly (byte R, byte G, byte B) ShallowColour = (160, 220, 245); // pale aqua
    private static readonly (byte R, byte G, byte B) MidColour = (40, 130, 200);      // mid teal-blue
    private static readonly (byte R, byte G, byte B) DeepColour = (8, 40, 100);       // deep ocean

    public Hex1bWidget Build(StressContext sc)
    {
        // Surface needs an Interactable wrapper to participate in mouse
        // event routing; the pond's position-tracking goes through a
        // separate (poll-based) path that doesn't need this.
        var widget = sc.Root.Interactable(ic =>
            ic.Surface(layer =>
            {
                EnsureField(layer.Width, layer.Height);
                Step();
                return new[] { layer.Layer(DrawWater) };
            }))
            .InputBindings(bindings =>
            {
                bindings.Mouse(MouseButton.Left).Action(ctx =>
                {
                    if (ctx.MouseX < 0 || ctx.MouseY < 0) return;
                    _drainX = ctx.MouseX;
                    _drainY = ctx.MouseY;
                    _drainActive = true;
                    _quiescent = false;
                });
                bindings.Mouse(MouseButton.Right).Action(_ =>
                {
                    _drainActive = false;
                    // Don't flip _quiescent here — inlets / refilling
                    // still need ticks. The diffusion pass will set it
                    // true again once everything settles.
                });
                bindings.Mouse(MouseButton.ScrollUp).Action(_ =>
                {
                    _strength = MathF.Min(MaxStrength, _strength * 1.2f);
                });
                bindings.Mouse(MouseButton.ScrollDown).Action(_ =>
                {
                    _strength = MathF.Max(MinStrength, _strength * 0.83f);
                });
            });

        // Drop RedrawAfter when the basin is fully settled so the root
        // can idle.
        return _quiescent ? widget : widget.RedrawAfter(sc.RedrawIntervalMs);
    }

    private void EnsureField(int w, int h)
    {
        if (_surfaceWidth == w && _surfaceHeight == h && _depth.Length > 0)
            return;
        _surfaceWidth = w;
        _surfaceHeight = h;
        _dotWidth = w;          // one sub-cell per cell horizontally
        _dotHeight = h * 2;     // two sub-cells per cell vertically (half-block)
        var n = _dotWidth * _dotHeight;
        _depth = new float[n];
        _delta = new float[n];
        _foam = new float[n];
        // Start full — deep ocean everywhere.
        Array.Fill(_depth, MaxDepth);
        _inlets.Clear();
        // Bubble count scales with surface area but is bounded so a
        // very large terminal doesn't degenerate into a foam wash.
        var bubbleCount = Math.Clamp(n / 70, 80, 600);
        _bubbles = new Bubble[bubbleCount];
        for (var i = 0; i < bubbleCount; i++)
        {
            ref var b = ref _bubbles[i];
            b.X = NextFloat() * _dotWidth;
            b.Y = NextFloat() * _dotHeight;
        }
        _quiescent = true;
    }

    private void Step()
    {
        if (_depth.Length == 0) return;
        _frame++;
        CurrentStrength = _strength;
        DrainOpen = _drainActive;

        var dw = _dotWidth;
        var dh = _dotHeight;
        var depth = _depth;
        var delta = _delta;

        // ---- Pass 0: advection. When the drain is open, semi-
        //      Lagrangian-advect the depth field by the analytic
        //      potential-flow velocity. This is what makes spiral
        //      contours appear: the radial-plus-tangential velocity
        //      drags the depth dip created by the drain around in
        //      logarithmic spirals. Without this step the depth
        //      field would stay rotationally symmetric no matter
        //      what.
        if (_drainActive)
        {
            AdvectField(depth, delta, dw, dh);
            // delta now holds the advected field. Swap so the rest
            // of Step reads/writes the advected one.
            (depth, delta) = (delta, depth);
            _depth = depth;
            _delta = delta;
        }

        Array.Clear(delta, 0, delta.Length);

        // ---- Pass 1: diffusion. Flow between each cell and its right
        //      and down neighbours; doing only two of the four edges
        //      per cell visits every edge exactly once (every pair
        //      shares exactly one direction). Symmetric add/subtract
        //      makes the operation mass-conserving by construction.
        //      Kept gentle so spiral contours from advection survive
        //      a few frames before being smoothed out.
        var anyFlow = false;
        for (var y = 0; y < dh; y++)
        {
            var row = y * dw;
            for (var x = 0; x < dw; x++)
            {
                var i = row + x;
                var di = depth[i];
                // Right neighbour
                if (x + 1 < dw)
                {
                    var j = i + 1;
                    var f = (di - depth[j]) * DiffusionK;
                    if (f != 0f) anyFlow = true;
                    delta[i] -= f;
                    delta[j] += f;
                }
                // Down neighbour
                if (y + 1 < dh)
                {
                    var j = i + dw;
                    var f = (di - depth[j]) * DiffusionK;
                    if (f != 0f) anyFlow = true;
                    delta[i] -= f;
                    delta[j] += f;
                }
            }
        }

        // ---- Pass 2: apply deltas, then drain, then inlets.
        var changed = anyFlow;
        for (var i = 0; i < depth.Length; i++)
        {
            var d = depth[i] + delta[i];
            if (d < 0f) d = 0f;
            else if (d > MaxDepth) d = MaxDepth;
            depth[i] = d;
        }

        if (_drainActive)
        {
            changed |= ApplyDrain(depth, dw, dh);
        }

        // Inlets only spawn / pulse when the drain is OFF, mirroring
        // the spec: open drain == active draining, closed drain ==
        // refill phase.
        if (!_drainActive)
        {
            changed |= TickInlets(depth, dw, dh);
        }

        // Surface bubbles run every frame: they need to keep coasting
        // (and decaying foam) after the drain closes so the basin
        // smoothly settles back to glassy water.
        var bubblesMoving = StepBubbles(dw, dh);

        _quiescent = !changed && _inlets.Count == 0 && !_drainActive && !bubblesMoving;
    }

    /// <summary>
    /// Subtracts water inside a disc around the drain. Falls off linearly
    /// with distance from the centre so the boundary doesn't appear as a
    /// hard ring; clamps each cell to zero so we don't generate negative
    /// depth (which would otherwise inject "anti-water" into the
    /// diffusion pass on subsequent frames).
    /// </summary>
    private bool ApplyDrain(float[] depth, int dw, int dh)
    {
        // Drain coords arrive in cell units; convert to sub-cell. The
        // user clicked between two stacked sub-cells, so target the
        // boundary by adding 1 (so y*2 + 1 lands on the lower half).
        var cx = _drainX;
        var cy = _drainY * 2 + 0.5f;
        var rate = DrainPerFrameBase * _strength;
        var r2 = DrainRadius * DrainRadius;
        var x0 = Math.Max(0, (int)(cx - DrainRadius));
        var x1 = Math.Min(dw - 1, (int)(cx + DrainRadius + 0.5f));
        var y0 = Math.Max(0, (int)(cy - DrainRadius * 2));   // x2 to compensate for cell aspect
        var y1 = Math.Min(dh - 1, (int)(cy + DrainRadius * 2 + 0.5f));
        var changed = false;
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var dx = x - cx;
                var dy = (y - cy) * 0.5f; // halve y delta — terminal cells are ~2x tall as wide
                var d2 = dx * dx + dy * dy;
                if (d2 > r2) continue;
                var falloff = 1f - MathF.Sqrt(d2) / DrainRadius;
                var sub = rate * falloff;
                if (sub <= 0f) continue;
                var i = y * dw + x;
                var nv = depth[i] - sub;
                if (nv < 0f) nv = 0f;
                if (nv != depth[i]) { depth[i] = nv; changed = true; }
            }
        }
        return changed;
    }

    /// <summary>
    /// Periodically spawns new inlet sources on the screen edge (only
    /// when not already at saturation), advances existing inlets, and
    /// retires expired ones. Each inlet's flow follows a sinusoidal
    /// envelope so it visibly fades in and out instead of stepping
    /// on/off.
    /// </summary>
    private bool TickInlets(float[] depth, int dw, int dh)
    {
        var changed = false;

        // Cheap saturation check — if any cell is below MaxDepth -
        // epsilon, we still have room. Single pass, early exit.
        var roomToFill = false;
        for (var i = 0; i < depth.Length; i++)
        {
            if (depth[i] < MaxDepth - 0.005f) { roomToFill = true; break; }
        }

        // Maybe spawn a new inlet.
        if (--_framesUntilNextInletCheck <= 0)
        {
            _framesUntilNextInletCheck = InletCheckInterval;
            if (roomToFill && _inlets.Count < InletMaxConcurrent)
            {
                _inlets.Add(SpawnInlet(dw, dh));
            }
        }

        // Advance / apply / retire inlets.
        for (var k = _inlets.Count - 1; k >= 0; k--)
        {
            var inlet = _inlets[k];
            inlet.Age++;
            if (inlet.Age >= inlet.Lifetime)
            {
                _inlets.RemoveAt(k);
                continue;
            }
            // Sin envelope: 0 at birth, 1 at midlife, 0 at death.
            var phase = MathF.PI * inlet.Age / inlet.Lifetime;
            var envelope = MathF.Sin(phase);
            var rate = inlet.PeakRate * envelope;
            if (DepositInlet(depth, dw, dh, inlet.X, inlet.Y, rate))
                changed = true;
            _inlets[k] = inlet;
        }

        return changed;
    }

    private Inlet SpawnInlet(int dw, int dh)
    {
        var edge = NextRandom() & 3;
        int x, y;
        switch (edge)
        {
            case 0: x = (int)(NextFloat() * dw); y = 0; break;
            case 1: x = (int)(NextFloat() * dw); y = dh - 1; break;
            case 2: x = 0; y = (int)(NextFloat() * dh); break;
            default: x = dw - 1; y = (int)(NextFloat() * dh); break;
        }
        var peak = InletPeakRateMin
            + NextFloat() * (InletPeakRateMax - InletPeakRateMin);
        var life = InletLifetimeMin
            + (int)(NextFloat() * (InletLifetimeMax - InletLifetimeMin));
        return new Inlet
        {
            X = x,
            Y = y,
            PeakRate = peak,
            Age = 0,
            Lifetime = life,
        };
    }

    private bool DepositInlet(float[] depth, int dw, int dh, int cx, int cy, float rate)
    {
        if (rate <= 0f) return false;
        var r2 = InletRadius * InletRadius;
        var x0 = Math.Max(0, cx - (int)InletRadius);
        var x1 = Math.Min(dw - 1, cx + (int)InletRadius);
        var y0 = Math.Max(0, cy - (int)(InletRadius * 2));
        var y1 = Math.Min(dh - 1, cy + (int)(InletRadius * 2));
        var changed = false;
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var dx = x - cx;
                var dy = (y - cy) * 0.5f;
                var d2 = dx * dx + dy * dy;
                if (d2 > r2) continue;
                var falloff = 1f - MathF.Sqrt(d2) / InletRadius;
                var add = rate * falloff;
                var i = y * dw + x;
                var nv = depth[i] + add;
                if (nv > MaxDepth) nv = MaxDepth;
                if (nv != depth[i]) { depth[i] = nv; changed = true; }
            }
        }
        return changed;
    }

    /// <summary>
    /// Renders the depth field using half-blocks. Each terminal cell
    /// shows two stacked sub-cells via the upper-half-block character
    /// (<c>▀</c>): foreground is the top sub-cell colour, background
    /// is the bottom sub-cell colour. This doubles the vertical
    /// simulation resolution at no extra render cost.
    /// </summary>
    private void DrawWater(Surface surface)
    {
        var w = surface.Width;
        var h = surface.Height;
        var dw = _dotWidth;
        var depth = _depth;
        var foam = _foam;

        for (var cy = 0; cy < h; cy++)
        {
            var topRow = (cy * 2) * dw;
            var botRow = (cy * 2 + 1) * dw;
            for (var cx = 0; cx < w; cx++)
            {
                var topD = depth[topRow + cx];
                var botD = depth[botRow + cx];
                var topCol = DepthColour(topD);
                var botCol = DepthColour(botD);

                // Foam blend: bubbles deposit foam along their path
                // and it decays each frame, so fast-moving bubbles
                // leave bright streaks that read as stretching as they
                // accelerate toward the drain.
                var topFoam = foam[topRow + cx];
                var botFoam = foam[botRow + cx];
                if (topFoam > 0f) topCol = BlendFoam(topCol, topFoam);
                if (botFoam > 0f) botCol = BlendFoam(botCol, botFoam);

                surface[cx, cy] = new SurfaceCell("▀", topCol, botCol);
            }
        }

        if (_drainActive)
        {
            var mx = (int)_drainX;
            var my = (int)_drainY;
            if ((uint)mx < (uint)w && (uint)my < (uint)h)
            {
                var bg = DepthColour(depth[(my * 2 + 1) * dw + mx]);
                surface[mx, my] = new SurfaceCell("◉",
                    Hex1bColor.FromRgb(255, 240, 200), bg);
            }
        }
    }

    private static Hex1bColor BlendFoam(Hex1bColor col, float intensity)
    {
        if (intensity > 1f) intensity = 1f;
        var r = (byte)(col.R + (255 - col.R) * intensity);
        var g = (byte)(col.G + (255 - col.G) * intensity);
        var b = (byte)(col.B + (255 - col.B) * intensity);
        return Hex1bColor.FromRgb(r, g, b);
    }

    /// <summary>
    /// Advances all surface bubbles one frame as PASSIVE TRACERS in
    /// the potential-flow velocity field. There is no momentum: each
    /// bubble's new position is simply its old position plus the
    /// local velocity. This is physically right for a surface particle
    /// floating on a draining fluid — it goes wherever the surface
    /// goes, with the mass of water behind it continuously pushing.
    /// Particles cannot escape orbit; they follow streamlines
    /// (logarithmic spirals) inevitably into the drain.
    /// </summary>
    /// <returns>true if anything is moving or any foam remains —
    /// used by the quiescence check.</returns>
    private bool StepBubbles(int dw, int dh)
    {
        // ---- foam decay pass (and "any foam left?" probe)
        var foam = _foam;
        var anyFoam = false;
        for (var i = 0; i < foam.Length; i++)
        {
            var f = foam[i] * FoamDecay;
            if (f < 0.005f) f = 0f;
            else anyFoam = true;
            foam[i] = f;
        }

        if (!_drainActive)
        {
            // No drain → no flow → tracers are stationary. (Inlet
            // flow is too localised to bother sampling here.)
            return anyFoam;
        }

        var cx = _drainX;
        var cy = _drainY * 2f + 0.5f;
        var Q = SinkStrength * _strength;
        var G = SwirlStrength * _strength;
        var soft = VelocitySoftening;
        var absorbR2 = BubbleAbsorbRadius * BubbleAbsorbRadius;

        for (var i = 0; i < _bubbles.Length; i++)
        {
            ref var b = ref _bubbles[i];
            var px = b.X;
            var py = b.Y;

            var ddx = cx - px;
            var ddy = cy - py;
            if (ddx * ddx + ddy * ddy < absorbR2)
            {
                RespawnBubbleAtEdge(ref b, dw, dh);
                continue;
            }

            VelocityAt(px, py, cx, cy, Q, G, soft, out var vx, out var vy);

            // Speed cap — guards against the centre singularity and
            // keeps semi-Lagrangian / line rasterisation stable.
            var vmag2 = vx * vx + vy * vy;
            if (vmag2 > BubbleMaxSpeed * BubbleMaxSpeed)
            {
                var s = BubbleMaxSpeed / MathF.Sqrt(vmag2);
                vx *= s;
                vy *= s;
            }

            var nx = px + vx;
            var ny = py + vy;

            if (nx < 0f || nx >= dw || ny < 0f || ny >= dh)
            {
                RespawnBubbleAtEdge(ref b, dw, dh);
                continue;
            }

            b.X = nx;
            b.Y = ny;
            DepositFoamLine(foam, dw, dh, px, py, nx, ny);
        }

        return true; // drain active → always moving
    }

    /// <summary>
    /// Computes the 2D potential-flow velocity at a sample point: a
    /// sink (radial inflow) plus a free vortex (tangential swirl),
    /// both with <c>1/(r + softening)</c> falloff. With Γ ≈ 2Q the
    /// streamlines are tight logarithmic spirals at ≈63° from the
    /// radial — visually a strong whirlpool.
    /// </summary>
    private static void VelocityAt(float x, float y, float cx, float cy,
        float Q, float G, float soft, out float vx, out float vy)
    {
        var dx = x - cx;
        var dy = y - cy;
        var r2 = dx * dx + dy * dy;
        var r = MathF.Sqrt(r2);
        var inv = 1f / (r + 1e-4f);
        var mag = 1f / (r + soft);
        // Unit radial outward (rx, ry); tangential CCW is (-ry, rx).
        var rx = dx * inv;
        var ry = dy * inv;
        // v = -Q * r_hat + G * t_hat
        vx = -Q * mag * rx + G * mag * (-ry);
        vy = -Q * mag * ry + G * mag *  rx;
    }

    /// <summary>
    /// Semi-Lagrangian advection: for each destination cell, trace
    /// the velocity field backwards by one step and sample the source
    /// field (bilinearly) at that upstream position. Unconditionally
    /// stable for any timestep — the price is some numerical
    /// diffusion, but at our 1-frame dt and small velocities outside
    /// the drain core, it's tolerable. Cells whose upstream sample
    /// falls outside the grid clamp to MaxDepth, on the assumption
    /// that "outside" is an effectively infinite ocean.
    /// </summary>
    private void AdvectField(float[] src, float[] dst, int dw, int dh)
    {
        var cx = _drainX;
        var cy = _drainY * 2f + 0.5f;
        var Q = SinkStrength * _strength;
        var G = SwirlStrength * _strength;
        var soft = VelocitySoftening;
        for (var y = 0; y < dh; y++)
        {
            var row = y * dw;
            for (var x = 0; x < dw; x++)
            {
                VelocityAt(x, y, cx, cy, Q, G, soft, out var vx, out var vy);
                var sx = x - vx;
                var sy = y - vy;
                dst[row + x] = SampleBilinear(src, dw, dh, sx, sy);
            }
        }
    }

    private static float SampleBilinear(float[] field, int dw, int dh, float x, float y)
    {
        // Outside-grid samples treat the basin as surrounded by full
        // ocean — this is what makes the basin try to "refill" itself
        // along the streamlines that wrap in from the screen edges.
        if (x < 0f || x > dw - 1f || y < 0f || y > dh - 1f)
            return MaxDepth;
        var x0 = (int)x;
        var y0 = (int)y;
        var x1 = Math.Min(x0 + 1, dw - 1);
        var y1 = Math.Min(y0 + 1, dh - 1);
        var fx = x - x0;
        var fy = y - y0;
        var a = field[y0 * dw + x0];
        var b = field[y0 * dw + x1];
        var c = field[y1 * dw + x0];
        var d = field[y1 * dw + x1];
        var top = a + (b - a) * fx;
        var bot = c + (d - c) * fx;
        return top + (bot - top) * fy;
    }

    /// <summary>
    /// Rasterises a line from (x0,y0) to (x1,y1) into the foam grid,
    /// accumulating intensity at each integer sub-cell. Brightness
    /// scales with segment length so the streak fades to invisible
    /// for stationary bubbles and pops for fast ones.
    /// </summary>
    private static void DepositFoamLine(float[] foam, int dw, int dh,
        float x0, float y0, float x1, float y1)
    {
        var dx = x1 - x0;
        var dy = y1 - y0;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        var steps = Math.Max(1, (int)MathF.Ceiling(len));
        var invSteps = 1f / steps;
        var stepX = dx * invSteps;
        var stepY = dy * invSteps;
        // Base brightness + speed boost. Drifting bubbles add a faint
        // sheen; fast in-rushing ones add bright streaks.
        var intensity = MathF.Min(1f, 0.35f + len * 0.4f);
        for (var s = 0; s <= steps; s++)
        {
            var fx = (int)(x0 + stepX * s);
            var fy = (int)(y0 + stepY * s);
            if ((uint)fx >= (uint)dw || (uint)fy >= (uint)dh) continue;
            var idx = fy * dw + fx;
            var existing = foam[idx];
            // Saturating add — foam approaches 1 but never overshoots.
            var v = existing + intensity * (1f - existing);
            foam[idx] = v;
        }
    }

    private void RespawnBubbleAtEdge(ref Bubble b, int dw, int dh)
    {
        var edge = NextRandom() & 3;
        switch (edge)
        {
            case 0: b.X = NextFloat() * dw; b.Y = 0f; break;
            case 1: b.X = NextFloat() * dw; b.Y = dh - 1f; break;
            case 2: b.X = 0f; b.Y = NextFloat() * dh; break;
            default: b.X = dw - 1f; b.Y = NextFloat() * dh; break;
        }
    }

    private static Hex1bColor DepthColour(float d)
    {
        // Three-segment lerp tuned so the full [0, 1] depth range is
        // visually distinguishable: a partially-drained cell at 0.7
        // must read clearly differently from the surrounding 1.0
        // ocean, otherwise the whirlpool's gradient is invisible.
        // Bands: sand → shallow (0..0.2) → mid (0.2..0.6) → deep (0.6..1).
        if (d <= 0f) return Hex1bColor.FromRgb(SandColour.R, SandColour.G, SandColour.B);
        if (d >= 1f) return Hex1bColor.FromRgb(DeepColour.R, DeepColour.G, DeepColour.B);
        if (d < 0.2f)
        {
            return Lerp(SandColour, ShallowColour, d / 0.2f);
        }
        if (d < 0.6f)
        {
            return Lerp(ShallowColour, MidColour, (d - 0.2f) / 0.4f);
        }
        return Lerp(MidColour, DeepColour, (d - 0.6f) / 0.4f);
    }

    private static Hex1bColor Lerp((byte R, byte G, byte B) a, (byte R, byte G, byte B) b, float t)
    {
        if (t < 0f) t = 0f;
        else if (t > 1f) t = 1f;
        var r = (byte)(a.R + (b.R - a.R) * t);
        var g = (byte)(a.G + (b.G - a.G) * t);
        var bl = (byte)(a.B + (b.B - a.B) * t);
        return Hex1bColor.FromRgb(r, g, bl);
    }
}
