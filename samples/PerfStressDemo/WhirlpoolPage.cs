using Hex1b;
using Hex1b.Input;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace PerfStressDemo;

/// <summary>
/// Page 3 — "Whirlpool".
///
/// Top-down water simulated on a half-block grid (each terminal cell
/// renders as <c>▀</c> with foreground = top sub-cell colour and
/// background = bottom sub-cell colour, doubling the vertical
/// simulation resolution). Two layered physics fields:
/// <list type="bullet">
///   <item><b>Baseline depth</b> — slow-evolving bulk water level in
///         <c>[0, 1]</c>. Drain consumes it at the cursor; refill
///         relaxes the edges back toward full when the drain is shut.
///         Semi-Lagrangian-advected by a potential-flow velocity
///         field while the drain is open, which spirals the radial
///         depth dip into logarithmic-spiral contours.</item>
///   <item><b>Wave field</b> — high-frequency signed displacement
///         around zero, evolved by the same 2D wave-equation scheme
///         as the pond page (ping-pong buffers, neighbour-average
///         minus previous, damped). Sources of energy:
///         a continuous negative impulse at the drain (turbulence at
///         the hole), continuous positive impulses along the edges
///         during refill (water rushing in), and a sprinkle of
///         random small chop while anything is active (the ocean's
///         own movement).</item>
/// </list>
///
/// Display blends the two: each sub-cell renders
/// <c>baseline + WaveDisplayScale * wave</c>, clamped to <c>[0, 1]</c>
/// and lerped through the sand → shallow → deep colour ramp.
///
/// Controls:
/// <list type="bullet">
///   <item><b>Left click</b> — open the drain at the cursor. Water
///         drains; if held long enough the basin empties completely.</item>
///   <item><b>Right click</b> — close the drain. The basin refills
///         from the edges; once full and the ripples die out, the
///         page reports IsIdle and the framework sleeps.</item>
///   <item><b>Scroll up/down</b> — adjust drain strength.</item>
/// </list>
/// </summary>
internal sealed class WhirlpoolPage : IStressPage
{
    public string Name => "Whirlpool";

    public string Description =>
        "Layered baseline + wave-equation water. Left click drains; "
        + "right click refills from the edges; scroll for drain strength.";

    // ----------------------------------------------------------------
    // Baseline depth grid. _depth[y * dotWidth + x] holds the water
    // depth in [0, MaxDepth] for sub-cell (x, y). _delta is a scratch
    // buffer used both as the advection destination and as the
    // diffusion delta accumulator.
    // ----------------------------------------------------------------
    private float[] _depth = Array.Empty<float>();
    private float[] _delta = Array.Empty<float>();
    private int _surfaceWidth;
    private int _surfaceHeight;
    private int _dotWidth;
    private int _dotHeight;

    // ----------------------------------------------------------------
    // Wave field — pond-style 2D wave equation with ping-pong
    // buffers. _wave / _wavePrev oscillate around zero; the renderer
    // adds a scaled fraction onto the baseline depth for the final
    // sub-cell value.
    // ----------------------------------------------------------------
    private float[] _wave = Array.Empty<float>();
    private float[] _wavePrev = Array.Empty<float>();

    // ----------------------------------------------------------------
    // Drain state. _drainActive gates consumption; placement (left
    // click) and removal (right click) are independent.
    // ----------------------------------------------------------------
    private bool _drainActive;
    private float _drainX;
    private float _drainY;
    private float _strength = 1.0f;
    private int _frame;
    private const float MinStrength = 0.3f;
    private const float MaxStrength = 4.0f;

    // ----------------------------------------------------------------
    // Physics constants — baseline.
    // ----------------------------------------------------------------
    private const float MaxDepth = 1.0f;
    private const float DiffusionK = 0.045f;
    private const float DrainRadius = 6.5f;        // sub-cells
    private const float DrainPerFrameBase = 0.32f;
    // Refill (drain off): pull baseline toward MaxDepth at an edge-
    // weighted rate so water visibly "flows in" from outside. Edge
    // cells fill in ~1s, interior fills via diffusion + the small
    // base rate over a few more seconds.
    private const float RefillEdgeRate = 0.045f;
    private const float RefillBaseRate = 0.004f;
    private const int   RefillReachCells = 8;       // edge boost falls off over this many sub-cells

    // ----------------------------------------------------------------
    // Physics constants — wave equation. Matches pond's "light"
    // preset feel: lively but settles in reasonable time without
    // continuous forcing.
    // ----------------------------------------------------------------
    private const float WaveDamping = 0.985f;
    private const float WaveDisplayScale = 0.32f;   // how much wave modulates rendered depth
    private const float WaveFloor = 0.005f;          // floor for "alive" tracking
    private const float DrainImpulse = -0.18f;       // per frame, at drain centre
    private const float RefillImpulse = 0.12f;       // per frame, per active edge cell during refill
    private const float ChopAmplitude = 0.06f;       // random impulse magnitude while active
    private const int   ChopPerFrame = 4;            // random chop sites per frame while active

    // ----------------------------------------------------------------
    // Potential-flow velocity field for advection of the baseline:
    //   v(r) = -Q/(r+s) r_hat + G/(r+s) theta_hat
    // With G ≈ 2Q the streamlines are ~63° spirals. VelocitySoftening
    // tames the 1/0 singularity at the drain centre.
    // ----------------------------------------------------------------
    private const float SinkStrength = 9.0f;
    private const float SwirlStrength = 18.0f;
    private const float VelocitySoftening = 2.5f;

    // ----------------------------------------------------------------
    // Activity tracking. _activity ramps to 1 whenever something is
    // happening (drain open OR basin not full) and decays to 0 when
    // everything settles. Used to gate wave-injection (chop / refill
    // impulses) and IsIdle.
    // ----------------------------------------------------------------
    private float _activity;
    private const float ActivityDecay = 0.04f;       // per frame when no activity

    private bool _quiescent = true;
    public bool IsIdle => _quiescent;

    // Status-bar accessors.
    public static float CurrentStrength { get; private set; } = 1.0f;
    public static bool DrainOpen { get; private set; }

    // xorshift32 RNG — small, fast, deterministic seed.
    private uint _rng = 0xDEADBEEFu;
    private uint NextRandom()
    {
        var x = _rng;
        x ^= x << 13; x ^= x >> 17; x ^= x << 5;
        _rng = x;
        return x;
    }
    private float NextFloat() => (NextRandom() & 0xFFFFFF) / (float)0x1000000;

    // Colour stops. Pre-built as byte triples so the per-cell lerp
    // doesn't allocate.
    private static readonly (byte R, byte G, byte B) SandColour    = (235, 215, 175);
    private static readonly (byte R, byte G, byte B) ShallowColour = (160, 220, 245);
    private static readonly (byte R, byte G, byte B) MidColour     = (40, 130, 200);
    private static readonly (byte R, byte G, byte B) DeepColour    = (8, 40, 100);

    public Hex1bWidget Build(StressContext sc)
    {
        // Surface alone doesn't route mouse events; Interactable does.
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
                    _activity = 1f;
                    _quiescent = false;
                });
                bindings.Mouse(MouseButton.Right).Action(_ =>
                {
                    _drainActive = false;
                    // Don't flip _quiescent — refill still has work to do.
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

        return _quiescent ? widget : widget.RedrawAfter(sc.RedrawIntervalMs);
    }

    private void EnsureField(int w, int h)
    {
        if (_surfaceWidth == w && _surfaceHeight == h && _depth.Length > 0)
            return;
        _surfaceWidth = w;
        _surfaceHeight = h;
        _dotWidth = w;
        _dotHeight = h * 2;
        var n = _dotWidth * _dotHeight;
        _depth = new float[n];
        _delta = new float[n];
        _wave = new float[n];
        _wavePrev = new float[n];
        Array.Fill(_depth, MaxDepth);
        _activity = 0f;
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

        // ---- Pass 0: advection of baseline when drain is open. Same
        //      analytic potential-flow field that drives the spiral
        //      look in the original whirlpool draft.
        if (_drainActive)
        {
            AdvectField(depth, delta, dw, dh);
            (depth, delta) = (delta, depth);
            _depth = depth;
            _delta = delta;
        }

        Array.Clear(delta, 0, delta.Length);

        // ---- Pass 1: gentle diffusion of baseline.
        var anyFlow = false;
        for (var y = 0; y < dh; y++)
        {
            var row = y * dw;
            for (var x = 0; x < dw; x++)
            {
                var i = row + x;
                var di = depth[i];
                if (x + 1 < dw)
                {
                    var j = i + 1;
                    var f = (di - depth[j]) * DiffusionK;
                    if (f != 0f) anyFlow = true;
                    delta[i] -= f;
                    delta[j] += f;
                }
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

        var changed = anyFlow;
        for (var i = 0; i < depth.Length; i++)
        {
            var d = depth[i] + delta[i];
            if (d < 0f) d = 0f;
            else if (d > MaxDepth) d = MaxDepth;
            depth[i] = d;
        }

        // ---- Pass 2: drain (consume baseline at cursor) or refill
        //      (pull baseline back up toward MaxDepth from edges).
        var basinFull = true;
        if (_drainActive)
        {
            changed |= ApplyDrain(depth, dw, dh);
            basinFull = false; // doesn't matter, _drainActive forces activity
        }
        else
        {
            changed |= ApplyRefill(depth, dw, dh, out basinFull);
        }

        // ---- Pass 3: activity tracking. Wave injection, chop, and
        //      idleness all key off this single scalar.
        if (_drainActive || !basinFull)
        {
            _activity = 1f;
        }
        else
        {
            _activity = MathF.Max(0f, _activity - ActivityDecay);
        }
        var active = _activity > 0.05f;

        // ---- Pass 4: inject impulses into the wave field, then run
        //      the wave-equation step. Wave runs every frame so
        //      energy injected last frame propagates immediately.
        if (active)
        {
            InjectWaveImpulses(dw, dh);
        }
        var waveAlive = WaveStep(dw, dh);

        _quiescent = !changed && !active && !waveAlive;
    }

    private bool ApplyDrain(float[] depth, int dw, int dh)
    {
        var cx = _drainX;
        var cy = _drainY * 2 + 0.5f;
        var rate = DrainPerFrameBase * _strength;
        var r2 = DrainRadius * DrainRadius;
        var x0 = Math.Max(0, (int)(cx - DrainRadius));
        var x1 = Math.Min(dw - 1, (int)(cx + DrainRadius + 0.5f));
        var y0 = Math.Max(0, (int)(cy - DrainRadius * 2));
        var y1 = Math.Min(dh - 1, (int)(cy + DrainRadius * 2 + 0.5f));
        var changed = false;
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var dx = x - cx;
                var dy = (y - cy) * 0.5f;
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
    /// Pulls the baseline back toward <see cref="MaxDepth"/> at a
    /// rate that decays from the screen edge toward the interior.
    /// Edge cells refill in roughly a second; interior cells rely on
    /// diffusion (and a tiny base rate) to top up. <paramref name="basinFull"/>
    /// is set true if every cell ended up within a small epsilon of
    /// MaxDepth this frame — used by the activity tracker.
    /// </summary>
    private bool ApplyRefill(float[] depth, int dw, int dh, out bool basinFull)
    {
        var changed = false;
        basinFull = true;
        var reach = RefillReachCells;
        var edgeBoost = RefillEdgeRate - RefillBaseRate;
        var basMax = MaxDepth - 0.003f;
        for (var y = 0; y < dh; y++)
        {
            var rowBaseY = y * dw;
            var ed_y = Math.Min(y, dh - 1 - y);
            for (var x = 0; x < dw; x++)
            {
                var i = rowBaseY + x;
                var d = depth[i];
                if (d < basMax) basinFull = false;
                var ed_x = Math.Min(x, dw - 1 - x);
                var edgeDist = Math.Min(ed_x, ed_y);
                var edgeFactor = edgeDist >= reach ? 0f : 1f - edgeDist / (float)reach;
                var rate = RefillBaseRate + edgeBoost * edgeFactor;
                var nv = d + (MaxDepth - d) * rate;
                if (nv > MaxDepth) nv = MaxDepth;
                if (nv != d) { depth[i] = nv; changed = true; }
            }
        }
        return changed;
    }

    /// <summary>
    /// Injects energy into the wave field. Three sources:
    /// (1) a continuous negative impulse at the drain centre while
    /// the drain is open (the suction is felt as turbulence);
    /// (2) continuous positive impulses scattered along the screen
    /// edges during refill (water rushing in splashes); (3) a few
    /// small random impulses anywhere on the field while anything is
    /// active, giving the ocean its own background chop.
    /// </summary>
    private void InjectWaveImpulses(int dw, int dh)
    {
        var wave = _wave;
        var amp = _activity;

        if (_drainActive)
        {
            var cx = (int)_drainX;
            var cy = (int)(_drainY * 2 + 0.5f);
            var imp = DrainImpulse * _strength * amp;
            Splat(wave, dw, dh, cx, cy, imp);
        }
        else
        {
            // Refill — drop a few positive splashes along whichever
            // edge the RNG picks each frame. Read as bubbling springs
            // running inward.
            for (var k = 0; k < 3; k++)
            {
                var edge = NextRandom() & 3;
                int ex, ey;
                switch (edge)
                {
                    case 0: ex = (int)(NextFloat() * dw); ey = 0; break;
                    case 1: ex = (int)(NextFloat() * dw); ey = dh - 1; break;
                    case 2: ex = 0; ey = (int)(NextFloat() * dh); break;
                    default: ex = dw - 1; ey = (int)(NextFloat() * dh); break;
                }
                Splat(wave, dw, dh, ex, ey, RefillImpulse * amp);
            }
        }

        // Background ocean chop — small scattered impulses.
        for (var k = 0; k < ChopPerFrame; k++)
        {
            var cx = (int)(NextFloat() * dw);
            var cy = (int)(NextFloat() * dh);
            var sign = (NextRandom() & 1) == 0 ? 1f : -1f;
            var i = cy * dw + cx;
            if ((uint)i < (uint)wave.Length)
                wave[i] += ChopAmplitude * amp * sign;
        }
    }

    /// <summary>3×3 soft splat of an impulse into the wave field.</summary>
    private static void Splat(float[] wave, int dw, int dh, int cx, int cy, float amp)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            var y = cy + dy;
            if ((uint)y >= (uint)dh) continue;
            for (var dx = -1; dx <= 1; dx++)
            {
                var x = cx + dx;
                if ((uint)x >= (uint)dw) continue;
                var f = (dx == 0 && dy == 0) ? 1f : ((dx == 0 || dy == 0) ? 0.5f : 0.25f);
                wave[y * dw + x] += amp * f;
            }
        }
    }

    /// <summary>
    /// Pond-style discretised 2D wave-equation step:
    /// <c>next = (sum of 4 neighbours)/2 - prev; next *= damping</c>.
    /// Ping-pongs <see cref="_wave"/> and <see cref="_wavePrev"/>.
    /// Returns true if any cell carries energy above the floor.
    /// </summary>
    private bool WaveStep(int dw, int dh)
    {
        var cur = _wave;
        var prv = _wavePrev;
        var anyAlive = false;
        for (var y = 1; y < dh - 1; y++)
        {
            var row = y * dw;
            for (var x = 1; x < dw - 1; x++)
            {
                var i = row + x;
                var nb = cur[i - 1] + cur[i + 1] + cur[i - dw] + cur[i + dw];
                var next = (nb * 0.5f) - prv[i];
                next *= WaveDamping;
                if (next > -WaveFloor && next < WaveFloor) next = 0f;
                else anyAlive = true;
                prv[i] = next;
            }
        }
        (_wave, _wavePrev) = (prv, cur);
        return anyAlive;
    }

    private static void VelocityAt(float x, float y, float cx, float cy,
        float Q, float G, float soft, out float vx, out float vy)
    {
        var dx = x - cx;
        var dy = y - cy;
        var r = MathF.Sqrt(dx * dx + dy * dy);
        var inv = 1f / (r + 1e-4f);
        var mag = 1f / (r + soft);
        var rx = dx * inv;
        var ry = dy * inv;
        vx = -Q * mag * rx + G * mag * (-ry);
        vy = -Q * mag * ry + G * mag *  rx;
    }

    /// <summary>
    /// Semi-Lagrangian advection of <paramref name="src"/> into
    /// <paramref name="dst"/>. Out-of-grid samples return 0 so the
    /// basin actually drains while the drain is open instead of
    /// being topped up by a fictitious infinite ocean.
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
                dst[row + x] = SampleBilinearOrZero(src, dw, dh, sx, sy);
            }
        }
    }

    private static float SampleBilinearOrZero(float[] field, int dw, int dh, float x, float y)
    {
        if (x < 0f || x > dw - 1f || y < 0f || y > dh - 1f) return 0f;
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

    private void DrawWater(Surface surface)
    {
        var w = surface.Width;
        var h = surface.Height;
        var dw = _dotWidth;
        var depth = _depth;
        var wave = _wave;

        for (var cy = 0; cy < h; cy++)
        {
            var topRow = (cy * 2) * dw;
            var botRow = (cy * 2 + 1) * dw;
            for (var cx = 0; cx < w; cx++)
            {
                var iTop = topRow + cx;
                var iBot = botRow + cx;
                var topV = depth[iTop] + WaveDisplayScale * wave[iTop];
                var botV = depth[iBot] + WaveDisplayScale * wave[iBot];
                if (topV < 0f) topV = 0f; else if (topV > 1f) topV = 1f;
                if (botV < 0f) botV = 0f; else if (botV > 1f) botV = 1f;
                surface[cx, cy] = new SurfaceCell("▀",
                    DepthColour(topV), DepthColour(botV));
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

    private static Hex1bColor DepthColour(float d)
    {
        if (d <= 0f) return Hex1bColor.FromRgb(SandColour.R, SandColour.G, SandColour.B);
        if (d >= 1f) return Hex1bColor.FromRgb(DeepColour.R, DeepColour.G, DeepColour.B);
        if (d < 0.2f) return Lerp(SandColour, ShallowColour, d / 0.2f);
        if (d < 0.6f) return Lerp(ShallowColour, MidColour, (d - 0.2f) / 0.4f);
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
