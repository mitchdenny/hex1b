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
    // Ocean swell. The depth field starts flat and a flat field
    // gives advection nothing to organise into spirals — pulling
    // the plug on a glassy bathtub doesn't form a visible whirlpool
    // either; you need the river/ocean's existing texture for the
    // vortex to wind into spiral arms.
    //
    // Each frame (while active) we relax depth toward a moving
    // multi-octave sinusoidal swell. The relaxation rate is small
    // enough that the drain and inlets always dominate locally, but
    // in calm regions the field gets continuously stirred and
    // advection winds those wave patterns into the drain.
    //
    // _swellAmpScale ramps to 1 while there's any activity (drain
    // open, inlets running) and decays back to zero when everything
    // settles — so the truly idle ocean is glassy still and the
    // page can sleep.
    // ----------------------------------------------------------------
    private float _swellAmpScale;
    private const float SwellAmpBase = 0.06f;       // depth units, peak deviation
    private const float SwellBlendRate = 0.035f;    // toward target per frame
    private const float SwellPhaseRate = 0.05f;     // radians per frame
    private const float SwellNominalLevel = 0.93f;  // mean depth the swell sits around
    private const float SwellAmpDecay = 0.96f;      // per frame when no activity

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
        // Start full — deep ocean everywhere.
        Array.Fill(_depth, MaxDepth);
        _inlets.Clear();
        _swellAmpScale = 0f;
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

        // ---- Ocean swell. Runs whenever there's activity (drain
        //      open OR inlets running). When activity stops, the
        //      amplitude decays to zero so the basin returns to a
        //      perfectly still mirror — which is when the page can
        //      finally idle.
        if (_drainActive || _inlets.Count > 0)
        {
            _swellAmpScale = 1f;
        }
        else
        {
            _swellAmpScale *= SwellAmpDecay;
            if (_swellAmpScale < 0.01f) _swellAmpScale = 0f;
        }
        var swellActive = _swellAmpScale > 0.01f;
        if (swellActive)
        {
            InjectSwell(depth, dw, dh);
        }

        _quiescent = !changed
            && _inlets.Count == 0
            && !_drainActive
            && !swellActive;
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

        for (var cy = 0; cy < h; cy++)
        {
            var topRow = (cy * 2) * dw;
            var botRow = (cy * 2 + 1) * dw;
            for (var cx = 0; cx < w; cx++)
            {
                var topCol = DepthColour(depth[topRow + cx]);
                var botCol = DepthColour(depth[botRow + cx]);
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

    /// <summary>
    /// Blends each depth cell toward a slowly evolving multi-octave
    /// sinusoidal swell. This injects the ocean-like surface texture
    /// that a real whirlpool organises into spiral arms — a perfectly
    /// flat field has nothing for advection to wind up. The blend
    /// rate is small enough that the drain and inlets always
    /// dominate locally, but the swell continuously stirs calm
    /// regions and (via advection in Pass 0) gets pulled into the
    /// drain as visible spiral contours.
    /// </summary>
    private void InjectSwell(float[] depth, int dw, int dh)
    {
        var amp = SwellAmpBase * _swellAmpScale;
        var blend = SwellBlendRate * _swellAmpScale;
        var t = _frame * SwellPhaseRate;
        // Pre-bake phase terms that don't depend on x so the inner
        // loop reduces to a couple of muls + sin per octave.
        var t1 = t;
        var t2 = t * 1.7f;
        var t3 = t * 2.4f;
        for (var y = 0; y < dh; y++)
        {
            var yt1 = y * 0.08f;
            var yt2 = y * -0.13f;
            var yt3 = y * 0.21f;
            var row = y * dw;
            for (var x = 0; x < dw; x++)
            {
                // 3 octaves: long swell + cross-direction medium + fine ripple.
                var n = MathF.Sin(x * 0.07f + yt1 + t1) * 0.55f
                      + MathF.Sin(x * 0.13f + yt2 + t2) * 0.30f
                      + MathF.Sin(x * 0.21f + yt3 + t3) * 0.15f;
                var target = SwellNominalLevel + amp * n;
                if (target > MaxDepth) target = MaxDepth;
                else if (target < 0f) target = 0f;
                var i = row + x;
                depth[i] += (target - depth[i]) * blend;
            }
        }
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
