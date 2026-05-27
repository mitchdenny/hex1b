using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace PerfStressDemo;

/// <summary>
/// Page 2 — "Pond ripple".
///
/// Same noise-character background as page 1, but instead of a fixed
/// concentric ripple driven by elapsed time, the wave field is driven
/// interactively by the mouse cursor. Move the mouse over the page and
/// the cells underneath light up; the disturbance propagates outward as
/// a real shallow-water wave (height-field with 2D wave-equation
/// integration), bounces off the edges, interferes with itself, and
/// dampens to stillness once you stop moving.
///
/// Render cost characteristics are similar to page 1's ripple — every
/// cell typically changes some amount each frame while waves are alive,
/// so the SurfaceComparer diff stays large. The key difference is that
/// the wave field actually decays to zero, so you can see the
/// renderer's "idle" path engage when the pond settles (the fast-path
/// CellsEqual short-circuit kicks in and the per-frame byte count
/// collapses).
/// </summary>
internal sealed class PondRipplePage : IStressPage
{
    public string Name => "Pond ripple (mouse)";

    public string Description =>
        "Move the mouse over the page — waves propagate from the cursor "
        + "across the pond and dampen to stillness.";

    // --- Noise background ----------------------------------------------------

    private const int RangeStart = 0x21; // '!'
    private const int RangeEnd = 0x7E;   // '~'
    private const int RangeSize = RangeEnd - RangeStart + 1;

    private static readonly string[] s_asciiChars = BuildAsciiChars();

    private static string[] BuildAsciiChars()
    {
        var arr = new string[RangeSize];
        for (var i = 0; i < RangeSize; i++)
            arr[i] = ((char)(RangeStart + i)).ToString();
        return arr;
    }

    // The character at each cell; stable across resizes for a given size.
    private string[]? _noise;

    // --- Wave physics --------------------------------------------------------
    //
    // Classic discretised 2D wave equation on a heightfield:
    //     next[x,y] = (prev[x-1,y] + prev[x+1,y]
    //                + prev[x,y-1] + prev[x,y+1]) / 2
    //                - current[x,y];
    //     next[x,y] *= damping;
    // We ping-pong between two buffers (current ↔ previous) and treat
    // out-of-bounds neighbours as 0 (Dirichlet) so the pond has solid edges.

    private float[]? _current;
    private float[]? _previous;
    private int _fieldWidth;
    private int _fieldHeight;
    // Last MouseX/Y values *observed* from the layer context, used purely
    // to detect whether the mouse actually moved between frames. Distinct
    // from the splash trail head below: the observed position persists
    // across stationary frames, the trail head does not.
    private int _observedMouseX = int.MinValue;
    private int _observedMouseY = int.MinValue;
    // Position of the last splash (head of the current drag trail), or -1
    // when the trail is "broken" (mouse outside the pond, or stationary
    // for a frame). Reset to -1 ends the trail so the next movement
    // starts a fresh single-point splash instead of drawing a Bresenham
    // line from a stale position.
    private int _trailX = -1;
    private int _trailY = -1;

    /// <summary>
    /// One viscosity preset — combines damping, step rate, and splash
    /// amplitude so each preset reads as a coherent fluid feel rather
    /// than independent knobs.
    /// </summary>
    /// <param name="Name">Short label shown in the status bar.</param>
    /// <param name="Damping">
    /// Per-step amplitude multiplier. 1.0 = lossless; values below 1
    /// dissipate energy over time. Lower = more viscous (waves die
    /// faster).
    /// </param>
    /// <param name="StepEveryNFrames">
    /// Run a physics step every Nth frame. Higher = slower wavefront
    /// propagation (acts as time dilation on the fluid). 1 = fast water,
    /// 3+ = slow glob.
    /// </param>
    /// <param name="SplashAmplitude">
    /// Magnitude of the splash impulse, scaled to keep single moves
    /// visible against the chosen damping.
    /// </param>
    internal readonly record struct ViscosityPreset(
        string Name,
        float Damping,
        int StepEveryNFrames,
        float SplashAmplitude);

    /// <summary>
    /// Cyclable viscosity presets, ordered thin → thick. Press V to
    /// advance through them.
    /// </summary>
    internal static readonly ViscosityPreset[] Presets = new[]
    {
        new ViscosityPreset("water",  0.995f, 1, -2.5f),
        new ViscosityPreset("light",  0.98f,  1, -3.5f),
        new ViscosityPreset("medium", 0.96f,  1, -4.5f),
        new ViscosityPreset("thick",  0.94f,  2, -5.5f),
        new ViscosityPreset("glob",   0.92f,  3, -6.5f),
    };

    /// <summary>
    /// Index into <see cref="Presets"/> for the current fluid feel.
    /// Mutated by the V key binding in Program.cs.
    /// </summary>
    public static int PresetIndex { get; set; } = 1; // "light" by default

    /// <summary>Human-readable preset name for the status bar.</summary>
    public static string PresetLabel => Presets[PresetIndex].Name;

    /// <summary>Advances to the next preset (wrapping).</summary>
    public static void CyclePreset()
        => PresetIndex = (PresetIndex + 1) % Presets.Length;

    private int _stepCounter;

    public Hex1bWidget Build(StressContext sc)
    {
        return sc.Root
            .Surface(layer =>
            {
                EnsureFieldSize(layer.Width, layer.Height);
                EnsureNoise(layer.Width, layer.Height);

                // Mouse coords are relative to the surface widget; -1 when
                // the pointer isn't over us. Treat any frame where the
                // pointer is present as a splash impulse — this gives the
                // "dragging a finger" feel without needing a click.
                var mx = layer.MouseX;
                var my = layer.MouseY;
                var inPond = mx >= 0 && my >= 0 && mx < layer.Width && my < layer.Height;
                // "Moved this frame" = the layer's mouse coords differ from
                // what we observed last frame. layer.MouseX/Y holds the
                // last reported value, so a stationary mouse reports the
                // same coords frame after frame.
                var movedThisFrame = inPond
                    && (mx != _observedMouseX || my != _observedMouseY);
                _observedMouseX = mx;
                _observedMouseY = my;

                if (movedThisFrame)
                {
                    // If the trail head is valid, draw a Bresenham splash
                    // line from it to the new position (continuous drag).
                    // If the trail was broken (pause or just entered the
                    // pond), splash only at the new position so we don't
                    // yank a wave across the screen.
                    SplashLine(_trailX, _trailY, mx, my);
                    _trailX = mx;
                    _trailY = my;
                }
                else
                {
                    // No movement this frame (or mouse not in pond): break
                    // the trail so the next movement starts fresh.
                    _trailX = -1;
                    _trailY = -1;
                }

                // Sub-step: only advance the simulation every Nth frame so
                // wavefronts propagate at a fraction of cell-per-frame
                // speed. Combined with damping this gives the chosen
                // viscosity preset its characteristic fluid feel.
                if (++_stepCounter >= Presets[PresetIndex].StepEveryNFrames)
                {
                    _stepCounter = 0;
                    Step();
                }

                return new[] { layer.Layer(DrawPond) };
            })
            // Keep stepping the simulation every frame even with no input
            // so waves dampen naturally to stillness.
            .RedrawAfter(sc.RedrawIntervalMs);
    }

    private void EnsureNoise(int w, int h)
    {
        if (_noise is { } existing && existing.Length == w * h)
            return;

        var rng = new Random(0xC0FFEE);
        var arr = new string[w * h];
        for (var i = 0; i < arr.Length; i++)
            arr[i] = s_asciiChars[rng.Next(RangeSize)];
        _noise = arr;
    }

    private void EnsureFieldSize(int w, int h)
    {
        if (_current is not null && _fieldWidth == w && _fieldHeight == h)
            return;

        _fieldWidth = w;
        _fieldHeight = h;
        _current = new float[w * h];
        _previous = new float[w * h];
        _observedMouseX = int.MinValue;
        _observedMouseY = int.MinValue;
        _trailX = -1;
        _trailY = -1;
    }

    /// <summary>
    /// Adds a splash impulse along the segment from (x0,y0) to (x1,y1).
    /// When the cursor moves several cells in a single frame, splashing
    /// only at the endpoint gives a "skipping" feel — splashing along
    /// the line gives the continuous "dragging a finger" feel.
    /// </summary>
    private void SplashLine(int x0, int y0, int x1, int y1)
    {
        if (x0 < 0 || y0 < 0)
        {
            Splash(x1, y1);
            return;
        }

        // Bresenham line, splashing at every step.
        var dx = Math.Abs(x1 - x0);
        var dy = -Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;

        while (true)
        {
            Splash(x0, y0);
            if (x0 == x1 && y0 == y1) break;
            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private void Splash(int cx, int cy)
    {
        if (_current is null) return;
        if ((uint)cx >= (uint)_fieldWidth || (uint)cy >= (uint)_fieldHeight) return;

        // 3x3 splat with a softer falloff at the corners so the disturbance
        // reads as a "fingertip" rather than a square.
        var w = _fieldWidth;
        var h = _fieldHeight;
        for (var dy = -1; dy <= 1; dy++)
        {
            var y = cy + dy;
            if ((uint)y >= (uint)h) continue;
            for (var dx = -1; dx <= 1; dx++)
            {
                var x = cx + dx;
                if ((uint)x >= (uint)w) continue;
                var falloff = (dx == 0 && dy == 0) ? 1.0f : ((dx == 0 || dy == 0) ? 0.5f : 0.25f);
                _current[y * w + x] += Presets[PresetIndex].SplashAmplitude * falloff;
            }
        }
    }

    /// <summary>
    /// Advances the wave simulation by one step. Reads from
    /// <see cref="_previous"/>, writes into a new buffer, then swaps
    /// so the new state becomes "current" for the next frame.
    /// </summary>
    private void Step()
    {
        if (_current is null || _previous is null) return;

        var w = _fieldWidth;
        var h = _fieldHeight;
        var cur = _current;
        var prv = _previous;
        var damping = Presets[PresetIndex].Damping;

        // Update interior cells. Edge cells stay at 0 (solid boundary).
        for (var y = 1; y < h - 1; y++)
        {
            var row = y * w;
            for (var x = 1; x < w - 1; x++)
            {
                var i = row + x;
                var neighbours = cur[i - 1] + cur[i + 1] + cur[i - w] + cur[i + w];
                var next = (neighbours * 0.5f) - prv[i];
                next *= damping;
                // Floor extremely small values to 0 so settled pond doesn't
                // keep emitting micro-differences forever (lets the renderer
                // fast-path engage).
                if (next > -0.001f && next < 0.001f) next = 0f;
                prv[i] = next;
            }
        }

        // Swap buffers: prv now holds the next state.
        (_current, _previous) = (prv, cur);
    }

    private void DrawPond(Surface surface)
    {
        if (_current is null || _noise is null) return;

        var w = surface.Width;
        var h = surface.Height;
        var black = Hex1bColor.Black;
        var field = _current;

        // Map signed displacement to brightness with a linear ramp clamped
        // at ±maxDisplay. Peaks brighten, troughs ALSO brighten (we display
        // |displacement|) so a wave reads as a band of brightness; what
        // distinguishes constructive vs destructive interference is the
        // *amplitude* of the resulting peak/trough — keep it linear here so
        // those amplitude differences are visible instead of saturating.
        const float MaxDisplay = 6.0f;
        for (var y = 0; y < h; y++)
        {
            var row = y * w;
            for (var x = 0; x < w; x++)
            {
                var i = row + x;
                var d = field[i];
                var mag = d < 0 ? -d : d;
                var t = mag / MaxDisplay;
                if (t > 1.0f) t = 1.0f;
                var level = (byte)(40 + (215 * t));
                var colour = Hex1bColor.FromRgb(level, level, level);
                surface[x, y] = new SurfaceCell(_noise[i], colour, black);
            }
        }
    }
}
