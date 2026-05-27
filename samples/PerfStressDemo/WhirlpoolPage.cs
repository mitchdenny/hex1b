using System.Numerics;
using Hex1b;
using Hex1b.Input;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace PerfStressDemo;

/// <summary>
/// Page 3 — "Whirlpool".
///
/// A voxel-column fluid simulation, viewed top-down.
///
/// The "tank" is the interior of the screen: a 1-cell-thick solid
/// border surrounds a rectangular pool whose floor is at the bottom
/// of every interior column. Each interior sub-cell stores a
/// <c>uint</c> bitmap of up to <see cref="D"/> stacked water voxels,
/// gravity-compacted to the low bits (the tank floor). The top-down
/// renderer colours each sub-cell purely by its column's bit-count
/// (popcount), so a deeper column is darker blue and an empty one
/// shows the sandy floor.
///
/// Physics is three cellular-automaton rules over the column grid:
/// <list type="number">
///   <item><b>Drain</b> — when the user opens the hole, voxels are
///         removed from random columns within a small disc each
///         frame. The disc grows from a dimple to its full width
///         over a few seconds so the whirlpool forms locally before
///         pulling water from further out.</item>
///   <item><b>Tangential rotation</b> — within the drain's reach,
///         each column probabilistically donates a single voxel to
///         the neighbour in its tangential direction (perpendicular
///         to the radial vector from the drain), with probability
///         falling off as a Gaussian of the radius. This is what
///         spins the water into a visible whirlpool.</item>
///   <item><b>Lateral pressure</b> — between every adjacent pair of
///         columns, if the height difference exceeds 1 voxel, move
///         a single voxel from the taller to the shorter. Done in
///         two phases per axis (even/odd parity) for race-free
///         transfer. This is the slow, granular "flow" you see
///         carrying outer water toward the developing whirlpool.</item>
/// </list>
///
/// When the drain is closed, a roaming inlet drips voxels into a
/// random interior column, refilling the tank. When every column is
/// full and nothing is moving, the page reports IsIdle so the
/// framework can sleep.
///
/// Controls: left click opens the drain at the cursor (and resets
/// the reach ramp), right click closes it, scroll up/down adjusts
/// drain strength.
/// </summary>
internal sealed class WhirlpoolPage : IStressPage
{
    public string Name => "Whirlpool";

    public string Description =>
        "Voxel-column fluid: each interior sub-cell stores up to "
        + "16 stacked water voxels in a uint bitmap. Drain, tangential "
        + "rotation, and lateral pressure all run as cellular rules.";

    // ----------------------------------------------------------------
    // Voxel column geometry. D = max voxels per column, so a full
    // column is FullColumn = (1u << D) - 1. Storing as uint keeps the
    // entire grid SIMD-friendly for later (Vector<uint>.PopCount on
    // .NET 8+).
    // ----------------------------------------------------------------
    private const int D = 16;
    private const uint FullColumn = (1u << D) - 1u;

    private uint[] _columns = Array.Empty<uint>();
    private int _screenW;     // surface width  (cells, including walls)
    private int _screenH;     // surface height (cells, including walls)
    private int _dw;          // interior width  in sub-cells (columns array X)
    private int _dh;          // interior height in sub-cells (columns array Y)

    // Wall thickness is 1 cell on each side of the screen — the
    // "tank" pool is the inner rectangle. Sub-cell offsets place the
    // interior columns into the right physical region of the screen.
    private const int WallCells = 1;

    // ----------------------------------------------------------------
    // Drain state.
    // ----------------------------------------------------------------
    private bool _drainActive;
    private int _drainCx;          // drain column (interior sub-cell coords)
    private int _drainCy;
    private float _strength = 1.0f;
    private int _drainOpenFrames;
    private const float MinStrength = 0.3f;
    private const float MaxStrength = 4.0f;

    private const float DrainReachInitial = 2.0f;   // initial Gaussian sigma + drain disc radius
    private const float DrainReachMax     = 18f;
    private const int   DrainReachRampFrames = 360;
    private const float DrainDiscRadiusMax = 11f;
    private const float DrainDensity      = 0.5f;    // voxels removed per disc-area unit per frame
    private const float TangentialBiasBase = 0.7f;   // peak probability of a tangential donation per column per frame

    // ----------------------------------------------------------------
    // Lateral / pressure equalisation. Move at most 1 voxel per pair
    // per frame whenever |dh| > LateralThreshold — keeps the flow
    // visibly granular.
    // ----------------------------------------------------------------
    private const int LateralThreshold = 1;

    // ----------------------------------------------------------------
    // Refill — roaming inlet that drips voxels into one interior
    // column at a time. No jet/wave splash here; the discrete voxel
    // accumulation IS the visible motion.
    // ----------------------------------------------------------------
    private float _inletX;
    private float _inletY;
    private int _inletAge;
    private int _inletGap;
    private const int InletLifetime = 80;
    private const int InletGapMin = 0;
    private const int InletGapMax = 10;
    private const int InletVoxelsPerFrame = 8;
    private const float InletRadius = 3f;

    // ----------------------------------------------------------------
    // Activity / idleness.
    // ----------------------------------------------------------------
    private bool _quiescent = true;
    public bool IsIdle => _quiescent;

    public static float CurrentStrength { get; private set; } = 1.0f;
    public static bool DrainOpen { get; private set; }

    // ----------------------------------------------------------------
    // Fast deterministic RNG (xorshift32).
    // ----------------------------------------------------------------
    private uint _rng = 0xDEADBEEFu;
    private uint NextRandom()
    {
        var x = _rng;
        x ^= x << 13; x ^= x >> 17; x ^= x << 5;
        _rng = x;
        return x;
    }
    private float NextFloat() => (NextRandom() & 0xFFFFFF) / (float)0x1000000;

    // ----------------------------------------------------------------
    // Colour stops.
    // ----------------------------------------------------------------
    private static readonly (byte R, byte G, byte B) SandColour    = (235, 215, 175);
    private static readonly (byte R, byte G, byte B) ShallowColour = (160, 220, 245);
    private static readonly (byte R, byte G, byte B) MidColour     = (40, 130, 200);
    private static readonly (byte R, byte G, byte B) DeepColour    = (8, 40, 100);
    private static readonly Hex1bColor WallColour = Hex1bColor.FromRgb(70, 55, 40);
    private static readonly Hex1bColor WallTrim   = Hex1bColor.FromRgb(40, 30, 22);
    private static readonly Hex1bColor HoleColour = Hex1bColor.FromRgb(8, 8, 12);

    public Hex1bWidget Build(StressContext sc)
    {
        // Surface alone doesn't route mouse; Interactable does.
        var widget = sc.Root.Interactable(ic =>
            ic.Surface(layer =>
            {
                EnsureField(layer.Width, layer.Height);
                Step();
                return new[] { layer.Layer(DrawTank) };
            }))
            .InputBindings(bindings =>
            {
                bindings.Mouse(MouseButton.Left).Action(ctx =>
                {
                    if (!TryMouseToInteriorColumn(ctx.MouseX, ctx.MouseY, out var icx, out var icy))
                        return;
                    _drainCx = icx;
                    _drainCy = icy;
                    if (!_drainActive) _drainOpenFrames = 0;
                    _drainActive = true;
                    _quiescent = false;
                });
                bindings.Mouse(MouseButton.Right).Action(_ =>
                {
                    _drainActive = false;
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

    private bool TryMouseToInteriorColumn(int mouseX, int mouseY, out int icx, out int icy)
    {
        icx = mouseX - WallCells;
        // Use the bottom sub-cell of the clicked cell as the drain
        // centre so the hole's vertical position matches what the
        // user sees their cursor over.
        icy = (mouseY - WallCells) * 2 + 1;
        if (mouseX < WallCells || mouseX >= _screenW - WallCells) return false;
        if (mouseY < WallCells || mouseY >= _screenH - WallCells) return false;
        if (icx < 0 || icx >= _dw || icy < 0 || icy >= _dh) return false;
        return true;
    }

    private void EnsureField(int w, int h)
    {
        if (_screenW == w && _screenH == h && _columns.Length > 0) return;
        _screenW = w;
        _screenH = h;
        var interiorW = Math.Max(0, w - 2 * WallCells);
        var interiorH = Math.Max(0, h - 2 * WallCells);
        _dw = interiorW;
        _dh = interiorH * 2;
        _columns = new uint[_dw * _dh];
        Array.Fill(_columns, FullColumn);
        _quiescent = true;
        _drainActive = false;
        _drainOpenFrames = 0;
        _inletAge = 0;
        _inletGap = 0;
    }

    private void Step()
    {
        if (_columns.Length == 0) return;
        CurrentStrength = _strength;
        DrainOpen = _drainActive;

        var changed = false;

        if (_drainActive)
        {
            _drainOpenFrames++;
            changed |= ApplyDrain();
            changed |= ApplyTangential();
        }

        changed |= ApplyLateralX();
        changed |= ApplyLateralY();

        var basinFull = IsBasinFull();
        if (!_drainActive && !basinFull)
        {
            changed |= ApplyRefill();
        }
        else
        {
            _inletAge = 0;
            _inletGap = 0;
        }

        _quiescent = !changed && !_drainActive && basinFull;
    }

    // ----------------------------------------------------------------
    // Column helpers — keep the bitmap representation consistent.
    // Voxels stack from bit 0 (floor) upward, so a column of height h
    // is (1 << h) - 1. PopCount gives the height in O(1) hardware op.
    // ----------------------------------------------------------------
    private static int Height(uint col) => BitOperations.PopCount(col);
    private static uint MakeCol(int height)
    {
        if (height <= 0) return 0u;
        if (height >= D) return FullColumn;
        return (1u << height) - 1u;
    }

    private float CurrentDrainReach()
    {
        var t = MathF.Min(1f, _drainOpenFrames / (float)DrainReachRampFrames);
        var s = t * t * (3f - 2f * t);
        return DrainReachInitial + (DrainReachMax - DrainReachInitial) * s;
    }

    // ----------------------------------------------------------------
    // Drain — remove the top voxel from a random sample of columns
    // within a disc around the drain centre. The disc starts as a
    // dimple and grows with the drain's reach.
    // ----------------------------------------------------------------
    private bool ApplyDrain()
    {
        var reach = CurrentDrainReach();
        var discR = MathF.Min(DrainDiscRadiusMax, reach * 0.7f + 0.5f);
        if (discR < 0.5f) discR = 0.5f;
        var discR2 = discR * discR;
        var discArea = MathF.PI * discR2;
        var k = (int)MathF.Max(1f, discArea * DrainDensity * _strength);
        var x0 = Math.Max(0, (int)(_drainCx - discR));
        var x1 = Math.Min(_dw - 1, (int)(_drainCx + discR + 0.5f));
        var y0 = Math.Max(0, (int)(_drainCy - discR));
        var y1 = Math.Min(_dh - 1, (int)(_drainCy + discR + 0.5f));
        var spanW = x1 - x0 + 1;
        var spanH = y1 - y0 + 1;
        if (spanW <= 0 || spanH <= 0) return false;

        var changed = false;
        for (var n = 0; n < k; n++)
        {
            var rx = x0 + (int)(NextFloat() * spanW);
            var ry = y0 + (int)(NextFloat() * spanH);
            var dx = rx - _drainCx;
            var dy = ry - _drainCy;
            if (dx * dx + dy * dy > discR2) continue;
            var i = ry * _dw + rx;
            var col = _columns[i];
            if (col == 0u) continue;
            // Remove the top voxel (column height shrinks by one).
            _columns[i] = MakeCol(Height(col) - 1);
            changed = true;
        }
        return changed;
    }

    // ----------------------------------------------------------------
    // Tangential rotation — for columns near the drain, with
    // Gaussian-falloff probability donate one voxel to the neighbour
    // in the tangential (perpendicular-to-radial) direction. This is
    // what makes the water visibly spin into the hole.
    // ----------------------------------------------------------------
    private bool ApplyTangential()
    {
        var cx = (float)_drainCx;
        var cy = (float)_drainCy;
        var reach = CurrentDrainReach();
        var twoReach2 = 2f * reach * reach;
        var skip2 = (3f * reach) * (3f * reach);
        var biasPeak = TangentialBiasBase * _strength;
        var changed = false;

        for (var y = 0; y < _dh; y++)
        {
            var dyc = y - cy;
            var row = y * _dw;
            for (var x = 0; x < _dw; x++)
            {
                var dxc = x - cx;
                var d2 = dxc * dxc + dyc * dyc;
                if (d2 > skip2 || d2 < 0.5f) continue;
                var prob = biasPeak * MathF.Exp(-d2 / twoReach2);
                if (NextFloat() > prob) continue;

                var r = MathF.Sqrt(d2);
                // Tangential = perpendicular to radial (CCW): rotate
                // (dxc/r, dyc/r) by 90°.
                var tx = -dyc / r;
                var ty = dxc / r;
                var nx = x + (tx > 0.4f ? 1 : tx < -0.4f ? -1 : 0);
                var ny = y + (ty > 0.4f ? 1 : ty < -0.4f ? -1 : 0);
                if (nx == x && ny == y) continue;
                if ((uint)nx >= (uint)_dw || (uint)ny >= (uint)_dh) continue;

                var srcIdx = row + x;
                var dstIdx = ny * _dw + nx;
                var srcH = Height(_columns[srcIdx]);
                var dstH = Height(_columns[dstIdx]);
                if (srcH == 0 || dstH >= D) continue;
                _columns[srcIdx] = MakeCol(srcH - 1);
                _columns[dstIdx] = MakeCol(dstH + 1);
                changed = true;
            }
        }
        return changed;
    }

    // ----------------------------------------------------------------
    // Lateral pressure on the X axis. Two phases: process pairs
    // starting at even x, then pairs starting at odd x. Race-free
    // because no column appears in two pairs in the same phase.
    // ----------------------------------------------------------------
    private bool ApplyLateralX()
    {
        var changed = false;
        for (var phase = 0; phase < 2; phase++)
        {
            for (var y = 0; y < _dh; y++)
            {
                var row = y * _dw;
                for (var x = phase; x + 1 < _dw; x += 2)
                {
                    var i = row + x;
                    var j = i + 1;
                    var ha = Height(_columns[i]);
                    var hb = Height(_columns[j]);
                    var diff = ha - hb;
                    if (diff > LateralThreshold)
                    {
                        _columns[i] = MakeCol(ha - 1);
                        _columns[j] = MakeCol(hb + 1);
                        changed = true;
                    }
                    else if (diff < -LateralThreshold)
                    {
                        _columns[i] = MakeCol(ha + 1);
                        _columns[j] = MakeCol(hb - 1);
                        changed = true;
                    }
                }
            }
        }
        return changed;
    }

    private bool ApplyLateralY()
    {
        var changed = false;
        for (var phase = 0; phase < 2; phase++)
        {
            for (var y = phase; y + 1 < _dh; y += 2)
            {
                var row = y * _dw;
                var nextRow = row + _dw;
                for (var x = 0; x < _dw; x++)
                {
                    var i = row + x;
                    var j = nextRow + x;
                    var ha = Height(_columns[i]);
                    var hb = Height(_columns[j]);
                    var diff = ha - hb;
                    if (diff > LateralThreshold)
                    {
                        _columns[i] = MakeCol(ha - 1);
                        _columns[j] = MakeCol(hb + 1);
                        changed = true;
                    }
                    else if (diff < -LateralThreshold)
                    {
                        _columns[i] = MakeCol(ha + 1);
                        _columns[j] = MakeCol(hb - 1);
                        changed = true;
                    }
                }
            }
        }
        return changed;
    }

    // ----------------------------------------------------------------
    // Refill — drip InletVoxelsPerFrame voxels into a small disc
    // around a roaming inlet point, picking a fresh point every
    // InletLifetime frames with a short cooldown between.
    // ----------------------------------------------------------------
    private bool ApplyRefill()
    {
        if (_inletAge == 0)
        {
            if (_inletGap > 0) { _inletGap--; return false; }
            // New inlet anywhere in the interior — gives a "rain
            // dropping into the tank" feel rather than only edges.
            _inletX = NextFloat() * _dw;
            _inletY = NextFloat() * _dh;
        }
        _inletAge++;
        if (_inletAge >= InletLifetime)
        {
            _inletAge = 0;
            _inletGap = InletGapMin + (int)(NextFloat() * (InletGapMax - InletGapMin + 1));
            return false;
        }

        var r = InletRadius;
        var r2 = r * r;
        var x0 = Math.Max(0, (int)(_inletX - r));
        var x1 = Math.Min(_dw - 1, (int)(_inletX + r + 0.5f));
        var y0 = Math.Max(0, (int)(_inletY - r));
        var y1 = Math.Min(_dh - 1, (int)(_inletY + r + 0.5f));
        var spanW = x1 - x0 + 1;
        var spanH = y1 - y0 + 1;
        if (spanW <= 0 || spanH <= 0) return false;

        var changed = false;
        for (var n = 0; n < InletVoxelsPerFrame; n++)
        {
            var rx = x0 + (int)(NextFloat() * spanW);
            var ry = y0 + (int)(NextFloat() * spanH);
            var dx = rx - _inletX;
            var dy = ry - _inletY;
            if (dx * dx + dy * dy > r2) continue;
            var i = ry * _dw + rx;
            var col = _columns[i];
            var h = Height(col);
            if (h >= D) continue;
            _columns[i] = MakeCol(h + 1);
            changed = true;
        }
        return changed;
    }

    private bool IsBasinFull()
    {
        var cols = _columns;
        for (var i = 0; i < cols.Length; i++)
        {
            if (cols[i] != FullColumn) return false;
        }
        return true;
    }

    // ----------------------------------------------------------------
    // Rendering — top-down. Walls drawn as a solid border, interior
    // sub-cells coloured by column height. Open drain hole rendered
    // as a dark dot on the floor at the drain cell.
    // ----------------------------------------------------------------
    private void DrawTank(Surface surface)
    {
        var w = surface.Width;
        var h = surface.Height;

        // Walls — top, bottom, left, right.
        for (var x = 0; x < w; x++)
        {
            surface[x, 0] = new SurfaceCell("▄", WallColour, WallTrim);
            surface[x, h - 1] = new SurfaceCell("▀", WallColour, WallTrim);
        }
        for (var y = 1; y < h - 1; y++)
        {
            surface[0, y] = new SurfaceCell("█", WallColour, WallColour);
            surface[w - 1, y] = new SurfaceCell("█", WallColour, WallColour);
        }

        // Interior — half-block per cell from two stacked sub-cells.
        var dw = _dw;
        var cols = _columns;
        for (var cy = WallCells; cy < h - WallCells; cy++)
        {
            var iy = cy - WallCells;
            var topRow = (iy * 2) * dw;
            var botRow = (iy * 2 + 1) * dw;
            for (var cx = WallCells; cx < w - WallCells; cx++)
            {
                var ix = cx - WallCells;
                var hTop = Height(cols[topRow + ix]);
                var hBot = Height(cols[botRow + ix]);
                surface[cx, cy] = new SurfaceCell("▀",
                    DepthColour(hTop), DepthColour(hBot));
            }
        }

        // Drain hole — a dark dot on the tank floor at the drain
        // location, visible when the drain is open. Sits on top of
        // the half-block water so you can see it through whatever
        // water depth remains.
        if (_drainActive)
        {
            var holeCx = _drainCx + WallCells;
            var holeCy = _drainCy / 2 + WallCells;
            if (holeCx >= WallCells && holeCx < w - WallCells
                && holeCy >= WallCells && holeCy < h - WallCells)
            {
                var iy = holeCy - WallCells;
                var topRow = (iy * 2) * dw;
                var botRow = (iy * 2 + 1) * dw;
                var ix = holeCx - WallCells;
                var hTop = Height(cols[topRow + ix]);
                var hBot = Height(cols[botRow + ix]);
                surface[holeCx, holeCy] = new SurfaceCell("◉",
                    HoleColour, DepthColour(hBot == 0 ? 0 : hBot));
                // Suppress: keep DepthColour(hTop) ignored for the
                // foreground because the glyph is the hole indicator.
                _ = hTop;
            }
        }
    }

    private static Hex1bColor DepthColour(int h)
    {
        if (h <= 0) return Hex1bColor.FromRgb(SandColour.R, SandColour.G, SandColour.B);
        if (h >= D) return Hex1bColor.FromRgb(DeepColour.R, DeepColour.G, DeepColour.B);
        var t = h / (float)D;
        if (t < 0.25f) return Lerp(SandColour, ShallowColour, t / 0.25f);
        if (t < 0.65f) return Lerp(ShallowColour, MidColour, (t - 0.25f) / 0.4f);
        return Lerp(MidColour, DeepColour, (t - 0.65f) / 0.35f);
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
