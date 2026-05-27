using Hex1b;
using Hex1b.Input;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace PerfStressDemo;

/// <summary>
/// Page 3 — "Whirlpool".
///
/// A fluid-like field of one character per cell. By default everything sits
/// still in a uniform grid. <b>Left click</b> opens a gravity well at the
/// cursor: characters are pulled in (with a tangential swirl giving the
/// spiral), and the void created at the centre propagates outward as a
/// pressure-gradient — overlapping/compressed particles push toward
/// lower-density neighbours, which keeps the field flowing toward the hole.
/// Particles that fall in are respawned at a random edge. <b>Right click</b>
/// turns the well off and the field settles wherever it landed (damping
/// kills residual velocity in a few seconds).
///
/// Controls:
///   * Left click   — place/move the well (activates it).
///   * Right click  — remove the well; field stabilises.
///   * Scroll wheel — adjust well strength.
///
/// Stress profile: ~w·h particles updated every frame, density binning
/// over a w·h grid each frame, plus one sparse cell write per particle.
/// Exercises scattered writes + a tight integer-accumulation pass over a
/// surface-sized buffer.
/// </summary>
internal sealed class WhirlpoolPage : IStressPage
{
    public string Name => "Whirlpool";

    public string Description =>
        "Pressure-driven character field. Left click to open a gravity well, "
        + "right click to remove it. Scroll adjusts strength.";

    // ----------------------------------------------------------------
    // Particle storage. Struct-of-Arrays — the hot loop touches each
    // field independently, so packed parallel arrays are kinder to the
    // cache than an array of structs.
    // ----------------------------------------------------------------
    private float[] _px = Array.Empty<float>();
    private float[] _py = Array.Empty<float>();
    private float[] _vx = Array.Empty<float>();
    private float[] _vy = Array.Empty<float>();
    private byte[] _glyphIdx = Array.Empty<byte>();
    private int _count;
    private int _surfaceWidth;
    private int _surfaceHeight;

    // Density grid: how many particles currently occupy each cell.
    // Recomputed from scratch every frame; gradients drive the pressure
    // force that redistributes the fluid toward low-density regions.
    private int[] _density = Array.Empty<int>();

    // Well state. _wellActive gates the entire pull/spin/swallow path.
    // When inactive only the pressure + damping terms run, so the field
    // settles into whatever shape it had when the user right-clicked.
    private bool _wellActive;
    private float _wellX;
    private float _wellY;
    // User-adjustable strength multiplier (scroll wheel). Multiplicative
    // scrolling so the same number of notches has the same perceptual
    // effect at any starting value. Clamped to a usable range.
    private float _strength = 1.0f;
    private const float MinStrength = 0.2f;
    private const float MaxStrength = 6.0f;

    // ----------------------------------------------------------------
    // Physics constants. The whole point of this page is to be SLOW
    // enough that you can track individual characters, so MaxSpeed is
    // tiny and damping is aggressive — set these too high and the
    // visual reads as snow rather than fluid.
    // ----------------------------------------------------------------
    private const float MaxSpeed = 0.35f;        // cells per frame, hard clamp
    private const float Damping = 0.94f;         // per-frame velocity multiplier
    private const float SwallowRadius = 1.2f;    // particle this close to well -> respawn
    private const float MinPullDistance = 2.0f;  // floor on r in the 1/r pull law
    private const float SpinRatio = 0.85f;       // tangential / radial force ratio
    private const float PressureK = 0.04f;       // how hard density gradients push
    // sizeFactor coefficient — kept low so default strength feels right
    // even on big monitors. The displayed strength is normalised by this
    // so different screens behave consistently.
    private const float SizeFactor = 0.05f;

    // Glyph palette — kept narrow and mostly punctuation so motion reads
    // as discrete bits of debris. Excludes whitespace and wide chars.
    private static readonly string[] s_glyphs = BuildGlyphs();

    private static string[] BuildGlyphs()
    {
        const string source = ".,'`*+/\\|-_~:;!?o0aXxv^=<>#%&";
        var arr = new string[source.Length];
        for (var i = 0; i < source.Length; i++)
            arr[i] = source[i].ToString();
        return arr;
    }

    // xorshift32 RNG — allocation-free, deterministic seed for reproducible
    // initial scatter.
    private uint _rng = 0xCAFEBABEu;

    private uint NextRandom()
    {
        var x = _rng;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _rng = x;
        return x;
    }

    private float NextFloat() => (NextRandom() & 0xFFFFFF) / (float)0x1000000;

    public Hex1bWidget Build(StressContext sc)
    {
        return sc.Root
            .Surface(layer =>
            {
                EnsureField(layer.Width, layer.Height);
                Step();
                return new[] { layer.Layer(DrawParticles) };
            })
            .InputBindings(bindings =>
            {
                // Left click: position the well at the cursor and activate
                // it. Subsequent clicks just relocate.
                bindings.Mouse(MouseButton.Left).Action(ctx =>
                {
                    if (ctx.MouseX < 0 || ctx.MouseY < 0) return;
                    _wellX = ctx.MouseX;
                    _wellY = ctx.MouseY;
                    _wellActive = true;
                });

                // Right click: kill the well. Existing particles keep
                // their velocity and damping settles them; pressure
                // pushes residual clumps back toward uniformity.
                bindings.Mouse(MouseButton.Right).Action(_ =>
                {
                    _wellActive = false;
                });

                // Scroll wheel: strength. Multiplicative so a couple of
                // notches visibly changes feel.
                bindings.Mouse(MouseButton.ScrollUp).Action(_ =>
                {
                    _strength = Math.Min(MaxStrength, _strength * 1.2f);
                });
                bindings.Mouse(MouseButton.ScrollDown).Action(_ =>
                {
                    _strength = Math.Max(MinStrength, _strength * 0.83f);
                });
            })
            .RedrawAfter(sc.RedrawIntervalMs);
    }

    /// <summary>Status-bar label exposing current strength.</summary>
    public static float CurrentStrength { get; private set; } = 1.0f;

    /// <summary>Status-bar label: whether the well is currently active.</summary>
    public static bool WellActive { get; private set; }

    private void EnsureField(int w, int h)
    {
        if (_surfaceWidth == w && _surfaceHeight == h && _px.Length > 0)
            return;

        _surfaceWidth = w;
        _surfaceHeight = h;
        // One particle per cell — a fully populated grid is what makes the
        // pressure model sensible (gradients are well-defined) and gives
        // the user the "I can see the whole fluid" feel they asked for.
        _count = w * h;
        _px = new float[_count];
        _py = new float[_count];
        _vx = new float[_count];
        _vy = new float[_count];
        _glyphIdx = new byte[_count];
        _density = new int[w * h];

        // Initial scatter: one particle at the centre of each cell, with
        // zero velocity and a random glyph. With no well and zero motion
        // density is uniform 1 per cell, gradients are zero everywhere,
        // and nothing moves until the user clicks.
        var idx = 0;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                _px[idx] = x + 0.5f;
                _py[idx] = y + 0.5f;
                _vx[idx] = 0;
                _vy[idx] = 0;
                _glyphIdx[idx] = (byte)(NextRandom() % (uint)s_glyphs.Length);
                idx++;
            }
        }
    }

    private void Step()
    {
        if (_count == 0) return;

        var w = _surfaceWidth;
        var h = _surfaceHeight;

        // -------- 1. Rebuild density grid --------
        // Zero it (Array.Clear is JIT-optimised to memset) then bin each
        // particle into the cell it currently occupies.
        Array.Clear(_density, 0, _density.Length);
        for (var i = 0; i < _count; i++)
        {
            var cx = (int)_px[i];
            var cy = (int)_py[i];
            if ((uint)cx >= (uint)w || (uint)cy >= (uint)h) continue;
            _density[cy * w + cx]++;
        }

        // -------- 2. Precompute well-related scalars --------
        // sizeFactor scales the absolute pull magnitude with surface area
        // so the user's chosen strength feels equivalent on small and
        // large terminals.
        var sizeFactor = MathF.Sqrt(w * h) * SizeFactor;
        var pullScale = _strength * sizeFactor;
        var spinScale = pullScale * SpinRatio;
        var swallowSq = SwallowRadius * SwallowRadius;
        var wellActive = _wellActive;
        var wx = _wellX;
        var wy = _wellY;

        // Expose to status bar.
        CurrentStrength = _strength;
        WellActive = wellActive;

        // -------- 3. Per-particle update --------
        for (var i = 0; i < _count; i++)
        {
            var px = _px[i];
            var py = _py[i];
            var vx = _vx[i];
            var vy = _vy[i];

            // Pressure gradient at the particle's current cell. We use
            // central differences in the density grid: high density to
            // one side pushes the particle the other way. Clamp the
            // sample coords so the boundary doesn't sample out of bounds.
            var cx = (int)px;
            var cy = (int)py;
            if ((uint)cx < (uint)w && (uint)cy < (uint)h)
            {
                var xl = cx > 0 ? cx - 1 : cx;
                var xr = cx < w - 1 ? cx + 1 : cx;
                var yu = cy > 0 ? cy - 1 : cy;
                var yd = cy < h - 1 ? cy + 1 : cy;
                var gx = _density[cy * w + xr] - _density[cy * w + xl];
                var gy = _density[yd * w + cx] - _density[yu * w + cx];
                // Force points from high density toward low density.
                vx -= PressureK * gx;
                vy -= PressureK * gy;
            }

            if (wellActive)
            {
                var dx = wx - px;
                var dy = wy - py;
                var rSq = dx * dx + dy * dy;
                if (rSq < swallowSq)
                {
                    Respawn(i);
                    continue;
                }
                var r = MathF.Sqrt(rSq);
                var rEff = r < MinPullDistance ? MinPullDistance : r;
                var invR = 1f / rEff;
                var ux = dx * invR;
                var uy = dy * invR;
                // Tangential (CCW rotation) gives the spiral. Same 1/r
                // falloff so the outer field stays alive — physical 1/r²
                // goes inert far from the well and the wider field looks
                // dead.
                var tx = -uy;
                var ty = ux;
                var a = pullScale * invR;
                var at = spinScale * invR;
                vx += ux * a + tx * at;
                vy += uy * a + ty * at;
            }

            // Damping. With the well off this is the only velocity
            // change, so the field settles into stillness in a couple
            // of seconds. With the well on, damping bounds the slingshot
            // effect of close approaches.
            vx *= Damping;
            vy *= Damping;

            // Speed clamp. Tiny — the whole point of this page is that
            // the user can follow individual glyphs as they drift.
            var sp2 = vx * vx + vy * vy;
            if (sp2 > MaxSpeed * MaxSpeed)
            {
                var sc = MaxSpeed / MathF.Sqrt(sp2);
                vx *= sc;
                vy *= sc;
            }

            px += vx;
            py += vy;

            // Boundary handling depends on whether the well is on.
            // With the well active, OOB particles respawn at an edge so
            // the fluid keeps flowing in. Without the well there's no
            // sink so it'd be wrong to teleport: just clamp the position
            // and zero the corresponding velocity component so things
            // come to rest against the wall.
            if (wellActive)
            {
                if (px < 0 || px >= w || py < 0 || py >= h)
                {
                    Respawn(i);
                    continue;
                }
            }
            else
            {
                if (px < 0) { px = 0; vx = 0; }
                else if (px >= w) { px = w - 0.001f; vx = 0; }
                if (py < 0) { py = 0; vy = 0; }
                else if (py >= h) { py = h - 0.001f; vy = 0; }
            }

            _px[i] = px;
            _py[i] = py;
            _vx[i] = vx;
            _vy[i] = vy;
        }
    }

    private void Respawn(int i)
    {
        var w = _surfaceWidth;
        var h = _surfaceHeight;
        var edge = NextRandom() & 3;
        const float Seed = 0.25f; // small inward push so they're visibly
                                   // moving when they appear, but still
                                   // slow enough to track.
        float px, py, vx, vy;
        switch (edge)
        {
            case 0: // top
                px = NextFloat() * w; py = 0.1f;
                vx = (NextFloat() - 0.5f) * 0.1f; vy = Seed;
                break;
            case 1: // bottom
                px = NextFloat() * w; py = h - 0.1f;
                vx = (NextFloat() - 0.5f) * 0.1f; vy = -Seed;
                break;
            case 2: // left
                px = 0.1f; py = NextFloat() * h;
                vx = Seed; vy = (NextFloat() - 0.5f) * 0.1f;
                break;
            default: // right
                px = w - 0.1f; py = NextFloat() * h;
                vx = -Seed; vy = (NextFloat() - 0.5f) * 0.1f;
                break;
        }
        _px[i] = px;
        _py[i] = py;
        _vx[i] = vx;
        _vy[i] = vy;
        _glyphIdx[i] = (byte)(NextRandom() % (uint)s_glyphs.Length);
    }

    private void DrawParticles(Surface surface)
    {
        var w = surface.Width;
        var h = surface.Height;
        var black = Hex1bColor.Black;

        // Draw each particle. Multiple particles overlapping the same
        // cell just means the last write wins for the glyph; brightness
        // comes from the density grid we already computed in Step, so
        // compressed regions visibly brighten and depleted regions go
        // dim — even though only one glyph shows, the density readout
        // tells you a clump is here.
        for (var i = 0; i < _count; i++)
        {
            var x = (int)_px[i];
            var y = (int)_py[i];
            if ((uint)x >= (uint)w || (uint)y >= (uint)h) continue;

            var d = _density[y * w + x];
            // Uniform density is 1; treat that as baseline grey. Stack
            // up to ~6 deep before saturating to white.
            var t = (d - 1) / 5f;
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
            var level = (byte)(120 + (int)(135 * t));
            var fg = Hex1bColor.FromRgb(level, level, level);
            surface[x, y] = new SurfaceCell(s_glyphs[_glyphIdx[i]], fg, black);
        }

        // Well marker drawn last so it's always visible on top, but only
        // when active. A right-clicked-off well leaves no visible cursor.
        if (_wellActive)
        {
            var wxI = (int)_wellX;
            var wyI = (int)_wellY;
            if ((uint)wxI < (uint)w && (uint)wyI < (uint)h)
            {
                surface[wxI, wyI] = new SurfaceCell(
                    "@", Hex1bColor.FromRgb(255, 64, 64), black);
            }
        }
    }
}
