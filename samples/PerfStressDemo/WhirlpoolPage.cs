using Hex1b;
using Hex1b.Input;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace PerfStressDemo;

/// <summary>
/// Page 3 — "Whirlpool".
///
/// A particle system in which ~3000 single-glyph particles drift across the
/// screen and are pulled toward a user-controllable gravity well. As they
/// approach the well a tangential force gives them an orbital spin, so the
/// late-stage motion looks like matter spiralling into a black hole. When a
/// particle crosses the swallow radius (or sails off-screen) it respawns at
/// a random edge with a small inward velocity and a fresh glyph.
///
/// Controls:
///   * Left click — relocate the well to the cursor.
///   * Scroll wheel — adjust well strength (up = stronger pull, down = weaker).
///
/// Stress profile: every particle is recomputed every frame and every
/// particle writes one cell, so the surface is touched ~3000 times/frame
/// at scattered positions. This exercises the renderer's sparse-update path
/// rather than the bulk-fill path the other pages hit.
/// </summary>
internal sealed class WhirlpoolPage : IStressPage
{
    public string Name => "Whirlpool";

    public string Description =>
        "Particle system pulled toward a mouse-controlled gravity well. "
        + "Click to reposition, scroll to adjust strength.";

    // --------------------------------------------------------------
    // Particle storage (Struct-of-Arrays for cache friendliness in
    // the hot per-frame loop — we touch every particle's px/py/vx/vy
    // sequentially each step).
    // --------------------------------------------------------------
    private float[] _px = Array.Empty<float>();
    private float[] _py = Array.Empty<float>();
    private float[] _vx = Array.Empty<float>();
    private float[] _vy = Array.Empty<float>();
    private byte[] _glyphIdx = Array.Empty<byte>();
    private int _count;
    private int _surfaceWidth;
    private int _surfaceHeight;

    // Well state. Mutated by mouse bindings; read by the sim each frame.
    // Negative coords sentinel = not yet positioned (centred on first frame).
    private float _wellX = -1;
    private float _wellY = -1;
    // Per-frame strength multiplier. The radial pull is roughly
    // strength * size² / r, where strength scales the magnitude.
    // 1.0 is a noticeable pull at typical screen sizes; the user can tune
    // via scroll. Clamped to a sensible range.
    private float _strength = 1.0f;
    private const float MinStrength = 0.2f;
    private const float MaxStrength = 8.0f;

    // Physics constants.
    private const float Damping = 0.985f;            // mild — we *want* fast in-fall
    private const float MaxSpeed = 8.0f;             // cells per frame, hard clamp
    private const float SwallowRadius = 1.5f;        // particles closer than this respawn
    private const float MinPullDistance = 2.5f;      // floor for r in pull calc — avoids /0 spikes
    private const float SpinRatio = 0.85f;           // tangential / radial; 0 = pure radial, 1 = circular
    private const float ParticlesPerCell = 0.5f;     // density (~3200 for 80×40)

    // Glyph palette. Mix of small, dense, and "streak"-looking ASCII so
    // moving particles read as bits of matter, not just dots. Excludes
    // wide chars and whitespace.
    private static readonly string[] s_glyphs = BuildGlyphs();

    private static string[] BuildGlyphs()
    {
        // Pre-cached so particle draws are allocation-free.
        const string source = ".,'`*+/\\|-_~:;!?o0aXxv^=<>#%&";
        var arr = new string[source.Length];
        for (var i = 0; i < source.Length; i++)
            arr[i] = source[i].ToString();
        return arr;
    }

    // Deterministic-ish RNG state for respawns. Not security-critical;
    // xorshift32 keeps the hot path allocation-free.
    private uint _rng = 0xCAFEBABEu;

    private uint NextRandom()
    {
        // xorshift32 — small, fast, good enough for visual jitter.
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
                // First-frame centring: drop the well in the middle until
                // the user clicks somewhere.
                if (_wellX < 0)
                {
                    _wellX = layer.Width * 0.5f;
                    _wellY = layer.Height * 0.5f;
                }
                Step();
                return new[] { layer.Layer(DrawParticles) };
            })
            .InputBindings(bindings =>
            {
                bindings.Mouse(MouseButton.Left).Action(ctx =>
                {
                    if (ctx.MouseX < 0 || ctx.MouseY < 0) return;
                    _wellX = ctx.MouseX;
                    _wellY = ctx.MouseY;
                });
                // Scroll up = stronger pull, scroll down = weaker.
                // Multiplicative so it feels exponential — a few notches
                // visibly change behaviour without bottoming out.
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

    private void EnsureField(int w, int h)
    {
        if (_surfaceWidth == w && _surfaceHeight == h && _px.Length > 0)
            return;

        _surfaceWidth = w;
        _surfaceHeight = h;
        _count = Math.Max(64, (int)(w * h * ParticlesPerCell));

        // Reallocate arrays to fit the new count. We don't try to pool
        // these — resizes are rare (resize-only) and the simulation
        // resumes from a fresh scatter, which is fine since the previous
        // arrangement is meaningless at a different resolution anyway.
        _px = new float[_count];
        _py = new float[_count];
        _vx = new float[_count];
        _vy = new float[_count];
        _glyphIdx = new byte[_count];

        for (var i = 0; i < _count; i++)
        {
            _px[i] = NextFloat() * w;
            _py[i] = NextFloat() * h;
            // Small initial drift so things move on frame 1.
            _vx[i] = (NextFloat() - 0.5f) * 0.4f;
            _vy[i] = (NextFloat() - 0.5f) * 0.4f;
            _glyphIdx[i] = (byte)(NextRandom() % (uint)s_glyphs.Length);
        }
    }

    private void Step()
    {
        if (_count == 0) return;
        CurrentStrength = _strength;

        var w = _surfaceWidth;
        var h = _surfaceHeight;
        var wx = _wellX;
        var wy = _wellY;
        var s = _strength;
        // Radial pull magnitude = s * sizeFactor / max(r, MinPullDistance).
        // sizeFactor scales with surface area so the same `strength` value
        // feels equivalent across a small laptop terminal and a giant
        // external monitor — without it, large screens make the well
        // feel weak because particles are mostly far away.
        var sizeFactor = MathF.Sqrt(w * h) * 0.25f;
        var pullScale = s * sizeFactor;
        var spinScale = pullScale * SpinRatio;
        var swallowSq = SwallowRadius * SwallowRadius;

        for (var i = 0; i < _count; i++)
        {
            var dx = wx - _px[i];
            var dy = wy - _py[i];
            var rSq = dx * dx + dy * dy;
            if (rSq < swallowSq)
            {
                Respawn(i);
                continue;
            }

            var r = MathF.Sqrt(rSq);
            var rEff = r < MinPullDistance ? MinPullDistance : r;
            var invR = 1f / rEff;
            // Unit vector toward the well.
            var ux = dx * invR;
            var uy = dy * invR;
            // Tangential unit vector — rotate (ux,uy) 90° CCW so the spiral
            // always winds the same direction (visually consistent).
            var tx = -uy;
            var ty = ux;

            // Acceleration: radial pull + tangential swirl. Both fall off
            // as 1/r so the inner orbit accelerates dramatically. The pure
            // 1/r law isn't physical for gravity (which is 1/r²) but reads
            // much better visually — the outer field stays alive instead
            // of going inert.
            var a = pullScale * invR;
            var at = spinScale * invR;
            _vx[i] += (ux * a + tx * at);
            _vy[i] += (uy * a + ty * at);

            // Damping prevents runaway escapes from the slingshot effect
            // of the inner orbit.
            _vx[i] *= Damping;
            _vy[i] *= Damping;

            // Clamp speed so a particle that nearly grazes the well doesn't
            // teleport across the screen in one frame.
            var sp2 = _vx[i] * _vx[i] + _vy[i] * _vy[i];
            if (sp2 > MaxSpeed * MaxSpeed)
            {
                var sc = MaxSpeed / MathF.Sqrt(sp2);
                _vx[i] *= sc;
                _vy[i] *= sc;
            }

            _px[i] += _vx[i];
            _py[i] += _vy[i];

            // Out-of-bounds → respawn at the edge.
            if (_px[i] < 0 || _px[i] >= w || _py[i] < 0 || _py[i] >= h)
                Respawn(i);
        }
    }

    private void Respawn(int i)
    {
        // Pick a random edge: 0=top, 1=bottom, 2=left, 3=right.
        var edge = NextRandom() & 3;
        var w = _surfaceWidth;
        var h = _surfaceHeight;
        float px, py, vx, vy;
        // Small inward velocity so they don't immediately satisfy the
        // out-of-bounds check again on a frame where their position rounds
        // wrong, and so the first few frames of motion are visible.
        const float Seed = 0.6f;

        switch (edge)
        {
            case 0: // top
                px = NextFloat() * w;
                py = 0;
                vx = (NextFloat() - 0.5f) * 0.3f;
                vy = Seed;
                break;
            case 1: // bottom
                px = NextFloat() * w;
                py = h - 1;
                vx = (NextFloat() - 0.5f) * 0.3f;
                vy = -Seed;
                break;
            case 2: // left
                px = 0;
                py = NextFloat() * h;
                vx = Seed;
                vy = (NextFloat() - 0.5f) * 0.3f;
                break;
            default: // right
                px = w - 1;
                py = NextFloat() * h;
                vx = -Seed;
                vy = (NextFloat() - 0.5f) * 0.3f;
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

        // Surfaces come out of the pool zeroed/blank, so we don't need to
        // explicitly clear — but we *do* want a hint of texture (the well
        // and a faint halo) before the particles go down. Keep this O(1):
        // just stamp the well marker; the rest stays default.

        for (var i = 0; i < _count; i++)
        {
            var x = (int)_px[i];
            var y = (int)_py[i];
            if ((uint)x >= (uint)w || (uint)y >= (uint)h) continue;

            // Brightness rises with speed — fast inner-orbit streaks
            // pop out from the slower outer drift.
            var sp = MathF.Sqrt(_vx[i] * _vx[i] + _vy[i] * _vy[i]);
            var t = sp / MaxSpeed;
            if (t > 1f) t = 1f;
            var level = (byte)(80 + (int)(175 * t));
            var fg = Hex1bColor.FromRgb(level, level, level);
            surface[x, y] = new SurfaceCell(s_glyphs[_glyphIdx[i]], fg, black);
        }

        // Well marker — bright red '@' on top of everything. Use integer
        // floor to align with particle positions.
        var wxI = (int)_wellX;
        var wyI = (int)_wellY;
        if ((uint)wxI < (uint)w && (uint)wyI < (uint)h)
        {
            surface[wxI, wyI] = new SurfaceCell(
                "@", Hex1bColor.FromRgb(255, 64, 64), black);
        }
    }
}
