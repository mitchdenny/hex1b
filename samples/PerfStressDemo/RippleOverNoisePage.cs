using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace PerfStressDemo;

/// <summary>
/// Page 1 — "Ripple over noise".
///
/// Full-screen field of random printable ASCII characters in white, with an
/// <see cref="EffectPanelWidget"/> wrapped around it that paints a continuously
/// expanding circular ripple wave. The wave touches every cell each frame, so
/// the EffectPanel allocates and composites a full-screen surface every frame
/// — this was the original "tanks the machine" scenario that prompted the
/// perf investigation.
/// </summary>
internal sealed class RippleOverNoisePage : IStressPage
{
    public string Name => "Ripple over noise";

    public string Description =>
        "Full-screen white random ASCII + continuous ripple EffectPanel. "
        + "The original 'tanks the machine' workload.";

    // Cached noise surface. Regenerated only when the terminal is resized.
    // Without this, generating a fresh 160×50 random char field every frame
    // would allocate 8000 strings/frame (240k/sec at 30 fps) which would
    // dwarf any allocation savings from the SurfacePool and make the
    // ripple animation hitch.
    private Surface? _noiseSurface;

    // Pre-computed single-character strings for printable ASCII so that
    // building the noise surface is allocation-free. char.ToString() is not
    // cached by the BCL — for hot fill paths like this you must do it
    // yourself.
    private static readonly string[] s_asciiChars = BuildAsciiChars();

    private const int RangeStart = 0x21; // '!'
    private const int RangeEnd = 0x7E;   // '~'
    private const int RangeSize = RangeEnd - RangeStart + 1;

    private static string[] BuildAsciiChars()
    {
        var arr = new string[RangeSize];
        for (var i = 0; i < RangeSize; i++)
            arr[i] = ((char)(RangeStart + i)).ToString();
        return arr;
    }

    public Hex1bWidget Build(StressContext sc)
    {
        // The noise itself is static. We only rebuild it on a resize.
        var noise = sc.Root.Surface(layer => GetOrBuildNoiseLayer(layer));

        // Wrap the noise in an EffectPanel that overlays a circular ripple
        // expanding from the center, animated by elapsed time.
        return sc.Root
            .EffectPanel(noise, surface => Ripple(surface, sc.ElapsedSeconds))
            .RedrawAfter(sc.RedrawIntervalMs);
    }

    private IEnumerable<SurfaceLayer> GetOrBuildNoiseLayer(SurfaceLayerContext layer)
    {
        var w = layer.Width;
        var h = layer.Height;
        if (_noiseSurface is null || _noiseSurface.Width != w || _noiseSurface.Height != h)
        {
            _noiseSurface = new Surface(w, h);
            FillNoise(_noiseSurface);
        }
        return new[] { layer.Layer(_noiseSurface) };
    }

    private static void FillNoise(Surface surface)
    {
        // Deterministic seed so the static background is stable across resizes
        // for a given size. The visible variety is plenty.
        var rng = new Random(0xC0FFEE);
        var white = Hex1bColor.White;
        var black = Hex1bColor.Black;

        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var ch = s_asciiChars[rng.Next(RangeSize)];
                surface[x, y] = new SurfaceCell(ch, white, black);
            }
        }
    }

    private static void Ripple(Surface surface, double seconds)
    {
        // Center of the screen, with x scaled so the ripple appears round
        // despite cells being roughly twice as tall as wide.
        var cx = surface.Width / 2.0;
        var cy = surface.Height / 2.0;
        const double aspect = 2.0;

        // Three concentric waves at different speeds keep the surface
        // visually busy and ensure the colour at every cell changes
        // frequently (so the diff stays large).
        var wave1 = seconds * 6.0;
        var wave2 = seconds * 9.0;
        var wave3 = seconds * 4.0;

        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var dx = (x - cx) / aspect;
                var dy = y - cy;
                var r = Math.Sqrt(dx * dx + dy * dy);

                var s1 = Math.Sin(r * 0.45 - wave1);
                var s2 = Math.Sin(r * 0.25 - wave2);
                var s3 = Math.Sin(r * 0.85 - wave3);
                var combined = (s1 + s2 + s3) / 3.0; // -1..+1
                var brightness = 0.5 + 0.5 * combined; // 0..1

                // Smooth hue rotation around the wave so the ripple is
                // unmistakable and exercises the full RGB colour output path.
                var hue = (r * 0.04 - seconds * 0.25) % 1.0;
                if (hue < 0) hue += 1.0;
                var colour = HsvToColor(hue, 0.85, 0.35 + 0.65 * brightness);

                var cell = surface[x, y];
                surface[x, y] = cell with { Foreground = colour };
            }
        }
    }

    internal static Hex1bColor HsvToColor(double h, double s, double v)
    {
        var i = (int)Math.Floor(h * 6.0) % 6;
        var f = h * 6.0 - Math.Floor(h * 6.0);
        var p = v * (1.0 - s);
        var q = v * (1.0 - f * s);
        var t = v * (1.0 - (1.0 - f) * s);
        var (r, g, b) = i switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
        return Hex1bColor.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}

