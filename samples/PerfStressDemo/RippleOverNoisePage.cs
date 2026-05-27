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

    /// <summary>
    /// Number of distinct greyscale shades the ripple modulates the foreground
    /// across. 256 = true-color smooth gradient (worst case — every cell tends
    /// to a unique colour, so SGR run-collapsing in the surface comparer can't
    /// help and the per-frame byte count to the host terminal is maximised).
    /// Lower values quantise the gradient into bands so neighbouring cells
    /// often share the same SGR — fewer bytes/frame, easier for slow terminal
    /// emulators to keep up. 0 is a sentinel meaning "no quantisation".
    /// </summary>
    public static int Levels { get; set; } = 256;

    /// <summary>
    /// Human-readable label for the current <see cref="Levels"/> value, used
    /// by the status bar.
    /// </summary>
    public static string LevelsLabel =>
        Levels >= 256 ? "smooth (256)" : Levels.ToString();

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
        // The noise itself is static. We only rebuild it on a resize, and we mark
        // the SurfaceWidget as cacheable so the framework doesn't reallocate a
        // ~64-bytes-per-cell child surface every reconcile (any reasonably sized
        // terminal puts that surface on the LOH → straight to Gen2).
        var noise = sc.Root
            .Surface(layer => GetOrBuildNoiseLayer(layer))
            .Cached(static _ => true);

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
        // visually busy and ensure the brightness at every cell changes
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

                // Optionally quantise into N bands so neighbouring cells share
                // a value, which lets the SurfaceComparer's SGR state tracking
                // collapse long runs of cells into a single SGR. Massive
                // bytes/frame reduction on slow host terminals.
                var levels = Levels;
                if (levels > 0 && levels < 256)
                {
                    var bucket = (int)(brightness * levels);
                    if (bucket >= levels) bucket = levels - 1;
                    brightness = bucket / (double)(levels - 1);
                }

                // Stay in greyscale: just modulate the white intensity so
                // the ripple reads as the original characters pulsing
                // between dim and bright rather than cycling hue.
                var level = (byte)(40 + 215 * brightness); // 40..255
                var colour = Hex1bColor.FromRgb(level, level, level);

                var cell = surface[x, y];
                surface[x, y] = cell with { Foreground = colour };
            }
        }
    }
}

