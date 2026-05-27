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
    private int _lastMouseX = -1;
    private int _lastMouseY = -1;

    // Damping per step. 1.0 = lossless (waves never settle), 0.99 ≈ a few
    // seconds to settle, 0.95 = quick decay. Tuned for visual taste.
    private const float Damping = 0.985f;

    // Splash impulse amplitude. Negative = displaces water downward (the
    // "finger pushes the surface down" mental model). Magnitude is in the
    // same units as the field; the colour mapper rescales to brightness.
    private const float SplashAmplitude = -8.0f;

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
                if (mx >= 0 && my >= 0 && mx < layer.Width && my < layer.Height)
                {
                    // Splash on every "in-pond" frame so a steady hover
                    // still injects a little energy; a moving cursor
                    // injects across each cell of its path.
                    SplashLine(_lastMouseX, _lastMouseY, mx, my);
                    _lastMouseX = mx;
                    _lastMouseY = my;
                }
                else
                {
                    // Pointer left the pond — stop tracking but let the
                    // existing waves continue to evolve.
                    _lastMouseX = -1;
                    _lastMouseY = -1;
                }

                Step();

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
        _lastMouseX = -1;
        _lastMouseY = -1;
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
                _current[y * w + x] += SplashAmplitude * falloff;
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

        // Update interior cells. Edge cells stay at 0 (solid boundary).
        for (var y = 1; y < h - 1; y++)
        {
            var row = y * w;
            for (var x = 1; x < w - 1; x++)
            {
                var i = row + x;
                var neighbours = cur[i - 1] + cur[i + 1] + cur[i - w] + cur[i + w];
                var next = (neighbours * 0.5f) - prv[i];
                next *= Damping;
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

        // Map field displacement (~ -10..+10) to brightness 0..1 via a
        // soft compressor so big splashes don't clip immediately.
        // mag/(mag+k) is a smooth tanh-like saturator in [0,1).
        for (var y = 0; y < h; y++)
        {
            var row = y * w;
            for (var x = 0; x < w; x++)
            {
                var i = row + x;
                var d = field[i];
                var mag = d < 0 ? -d : d;
                var t = mag / (mag + 4.0f);
                var level = (byte)(40 + (215 * t));
                var colour = Hex1bColor.FromRgb(level, level, level);
                surface[x, y] = new SurfaceCell(_noise[i], colour, black);
            }
        }
    }
}
