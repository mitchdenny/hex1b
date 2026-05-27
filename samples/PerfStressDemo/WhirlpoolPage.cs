using Hex1b;
using Hex1b.Input;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace PerfStressDemo;

/// <summary>
/// Page 3 — "Whirlpool".
///
/// A shallow-water-equations fluid in a top-down tank. Each interior
/// sub-cell holds an integer column height (a stack of unit voxels)
/// plus a 2D velocity vector. Velocities evolve via:
/// <list type="bullet">
///   <item><b>Pressure gradient</b> — water accelerates away from
///         high-water cells toward low-water cells (<c>∂v/∂t ∝ -∇h</c>).</item>
///   <item><b>Drain attraction</b> — when the drain is open, each
///         cell within reach receives a radially-inward acceleration
///         weighted by a Gaussian on distance to the drain centre.
///         The vortex shape emerges naturally from this radial pull
///         plus the pressure gradient and the obstacle field, with
///         no artificial tangential swirl.</item>
///   <item><b>Damping</b> — multiplicative per-frame velocity decay
///         plus a hard cap; the system loses energy over time so it
///         eventually settles.</item>
/// </list>
/// Heights evolve via integer voxel transfers. For each edge between
/// adjacent cells, the volumetric flux is
/// <c>avg(velocity) × upwind(height)</c>; we accumulate it on a
/// per-edge float and whenever it crosses ±1 we transfer one voxel
/// across the edge. So even single-voxel motion is conserved and
/// driven by the velocity field.
///
/// The combination gives genuinely elastic flow: open the drain and
/// water spirals into the hole; close the drain and momentum carries
/// the surrounding water inward past the equilibrium level, the
/// pressure-gradient force then pushes it back out, and the basin
/// sloshes for several seconds before settling.
///
/// Voxels are still individuals — they're stored as the height count
/// in their column — but their motion is governed by the per-cell
/// velocity field. All hot arrays (heights, vx, vy, flow accumulators)
/// are dense float/short data with no branches in the inner loops
/// of damping or advection, so a future SIMD pass over Vector&lt;float&gt;
/// is straightforward.
///
/// The tank is the interior of the screen: a 1-cell-thick solid
/// border is drawn around the perimeter, with a scatter of random
/// solid disc obstacles inside that the water has to flow around.
/// The drain hole appears as a dark dot at the cursor while it's open.
///
/// Controls: left click places the outlet (drain) at the cursor;
/// right click places the inlet (refill source); ctrl+left or
/// ctrl+right click clears the outlet or inlet respectively;
/// R clears both and empties the basin; scroll up/down adjusts
/// strength (drain consumption and inlet flow).
/// </summary>
internal sealed class WhirlpoolPage : IStressPage
{
    public string Name => "Whirlpool";

    public string Description =>
        "Shallow-water fluid: per-cell continuous height + 2D velocity, "
        + "pressure-gradient acceleration, radial drain attraction, "
        + "fractional volumetric flux transfers. Water sloshes back "
        + "elastically when the drain closes.";

    // ----------------------------------------------------------------
    // Geometry — heights are continuous, columns top out at D units.
    // The integer ceiling exists only for rendering buckets; the
    // underlying physics is fractional so gravity can level a surface
    // to its true equilibrium instead of getting stuck on a slope-1
    // staircase that a pure integer field would consider "settled".
    // ----------------------------------------------------------------
    private const float D = 16f;

    private float[] _height = Array.Empty<float>();
    private float[] _vx = Array.Empty<float>();
    private float[] _vy = Array.Empty<float>();
    private bool[] _solid = Array.Empty<bool>();

    private int _screenW, _screenH;
    private int _dw, _dh;
    private const int WallCells = 1;

    // ----------------------------------------------------------------
    // Drain state.
    // ----------------------------------------------------------------
    private bool _drainActive;
    private int _drainCx;
    private int _drainCy;
    private float _strength = 1.0f;
    private int _drainOpenFrames;
    private const float MinStrength = 0.2f;
    private const float MaxStrength = 8.0f;

    private const float DrainReachInitial   = 2.0f;
    private const float DrainReachMax       = 22f;
    private const int   DrainReachRampFrames = 90;
    private const float DrainDiscRadiusMax  = 3.0f;
    private const float DrainDensity        = 4.0f;
    private const float PullStrength        = 0.15f; // peak inward accel per frame at drain centre

    // ----------------------------------------------------------------
    // Shallow-water dynamics. Tune for visible momentum / sloshing
    // without runaway oscillation.
    // ----------------------------------------------------------------
    private const float PressureGain = 0.06f;
    private const float Damping      = 0.92f;
    private const float MaxSpeed     = 0.6f;
    private const float VelocityFloor = 0.01f;
    // Fraction of the per-edge volumetric flux applied per frame.
    // Lower = more viscous / stable; higher = sloshier.
    private const float AdvectionRate = 0.5f;

    // ----------------------------------------------------------------
    // Surface tension / film thickness.
    //
    // Real fluids have cohesion: a thin film of water sits put against
    // a surface until enough volume accumulates above it to overflow.
    // We model that with a single threshold — water at or below
    // FilmThickness voxels is considered a "skin" with no internal
    // pressure gradient and no advective flux. Only the excess above
    // the threshold drives flow. Drain forces likewise only attract
    // cells that have water depth above the skin.
    // ----------------------------------------------------------------
    private const float FilmThickness = 1.5f;

    // ----------------------------------------------------------------
    // Refill — fixed inlet placed by right click, drips voxels into a
    // small interior disc until the user clears or relocates it.
    // ----------------------------------------------------------------
    private bool _inletActive;
    private int _inletCx;
    private int _inletCy;
    private const float InletFlowPerFrame = 120f;
    private const float InletRadius  = 5f;

    private bool _quiescent = true;
    public bool IsIdle => _quiescent;

    public static float CurrentStrength { get; private set; } = 1f;
    public static bool DrainOpen { get; private set; }
    public static bool InletOpen { get; private set; }

    // xorshift32 RNG
    private uint _rng = 0xDEADBEEFu;
    private uint NextRandom()
    {
        var x = _rng; x ^= x << 13; x ^= x >> 17; x ^= x << 5;
        _rng = x; return x;
    }
    private float NextFloat() => (NextRandom() & 0xFFFFFF) / (float)0x1000000;

    private static readonly (byte R, byte G, byte B) SandColour    = (235, 215, 175);
    private static readonly (byte R, byte G, byte B) ShallowColour = (160, 220, 245);
    private static readonly (byte R, byte G, byte B) MidColour     = (40, 130, 200);
    private static readonly (byte R, byte G, byte B) DeepColour    = (8, 40, 100);
    private static readonly Hex1bColor WallColour = Hex1bColor.FromRgb(70, 55, 40);
    private static readonly Hex1bColor WallTrim   = Hex1bColor.FromRgb(40, 30, 22);
    private static readonly Hex1bColor HoleColour = Hex1bColor.FromRgb(8, 8, 12);
    private static readonly Hex1bColor InletColour = Hex1bColor.FromRgb(240, 240, 255);
    private static readonly Hex1bColor RockColour = Hex1bColor.FromRgb(95, 85, 75);

    public Hex1bWidget Build(StressContext sc)
    {
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
                bindings.Mouse(MouseButton.Right).Action(ctx =>
                {
                    if (!TryMouseToInteriorColumn(ctx.MouseX, ctx.MouseY, out var icx, out var icy))
                        return;
                    _inletCx = icx;
                    _inletCy = icy;
                    _inletActive = true;
                    _quiescent = false;
                });
                bindings.Mouse(MouseButton.ScrollUp).Action(_ =>
                {
                    _strength = MathF.Min(MaxStrength, _strength * 1.4f);
                });
                bindings.Mouse(MouseButton.ScrollDown).Action(_ =>
                {
                    _strength = MathF.Max(MinStrength, _strength * 0.71f);
                });
                bindings.Mouse(MouseButton.Left).Ctrl().Action(_ =>
                {
                    _drainActive = false;
                    _quiescent = false;
                });
                bindings.Mouse(MouseButton.Right).Ctrl().Action(_ =>
                {
                    _inletActive = false;
                    _quiescent = false;
                });
                bindings.Key(Hex1bKey.R).Action(_ => ResetBasin(), "Reset");
            });

        return _quiescent ? widget : widget.RedrawAfter(sc.RedrawIntervalMs);
    }

    private bool TryMouseToInteriorColumn(int mouseX, int mouseY, out int icx, out int icy)
    {
        icx = mouseX - WallCells;
        icy = (mouseY - WallCells) * 2 + 1;
        if (mouseX < WallCells || mouseX >= _screenW - WallCells) return false;
        if (mouseY < WallCells || mouseY >= _screenH - WallCells) return false;
        if (icx < 0 || icx >= _dw || icy < 0 || icy >= _dh) return false;
        return true;
    }

    private void ResetBasin()
    {
        var n = _height.Length;
        for (var i = 0; i < n; i++)
        {
            _height[i] = 0f;
            _vx[i] = 0f;
            _vy[i] = 0f;
        }
        _drainActive = false;
        _drainOpenFrames = 0;
        _inletActive = false;
        _strength = 1f;
        _quiescent = false;
    }

    private void EnsureField(int w, int h)
    {
        if (_screenW == w && _screenH == h && _height.Length > 0) return;
        _screenW = w;
        _screenH = h;
        _dw = Math.Max(0, w - 2 * WallCells);
        _dh = Math.Max(0, h - 2 * WallCells) * 2;
        var n = _dw * _dh;
        _height = new float[n];
        _vx = new float[n];
        _vy = new float[n];
        _solid = new bool[n];
        GenerateObstacles();
        for (var i = 0; i < n; i++) _height[i] = 0f;
        _quiescent = true;
        _drainActive = false;
        _drainOpenFrames = 0;
        _inletActive = false;
    }

    /// <summary>
    /// Scatters a handful of random solid disc obstacles inside the
    /// tank interior, leaving a margin from the perimeter walls so
    /// every cell on the border remains traversable.
    /// </summary>
    private void GenerateObstacles()
    {
        var n = _solid.Length;
        for (var i = 0; i < n; i++) _solid[i] = false;
        if (_dw < 8 || _dh < 8) return;

        var area = _dw * _dh;
        var count = Math.Max(4, area / 600); // ~ a couple of dozen on a typical screen
        var margin = 2;

        for (var k = 0; k < count; k++)
        {
            var cx = margin + (int)(NextFloat() * (_dw - 2 * margin));
            var cy = margin + (int)(NextFloat() * (_dh - 2 * margin));
            var r = 1.5f + NextFloat() * 3.5f;
            var r2 = r * r;
            var x0 = Math.Max(0, (int)(cx - r));
            var x1 = Math.Min(_dw - 1, (int)(cx + r + 0.5f));
            var y0 = Math.Max(0, (int)(cy - r));
            var y1 = Math.Min(_dh - 1, (int)(cy + r + 0.5f));
            for (var y = y0; y <= y1; y++)
            {
                var row = y * _dw;
                for (var x = x0; x <= x1; x++)
                {
                    var dx = x - cx;
                    var dy = y - cy;
                    if (dx * dx + dy * dy <= r2) _solid[row + x] = true;
                }
            }
        }
    }

    private void Step()
    {
        if (_height.Length == 0) return;
        CurrentStrength = _strength;
        DrainOpen = _drainActive;
        InletOpen = _inletActive;

        ApplyForces();
        ApplyDamping();
        var moved = ApplyAdvection();

        var consumed = false;
        if (_drainActive)
        {
            _drainOpenFrames++;
            consumed = ApplyDrain();
        }

        var refilled = false;
        if (_inletActive)
        {
            refilled = ApplyRefill();
        }

        _quiescent = !_drainActive
                     && !_inletActive
                     && !moved
                     && !consumed
                     && !refilled
                     && AllVelocitiesQuiescent();
    }

    private bool AllVelocitiesQuiescent()
    {
        var vx = _vx; var vy = _vy;
        for (var i = 0; i < vx.Length; i++)
        {
            if (vx[i] > VelocityFloor || vx[i] < -VelocityFloor) return false;
            if (vy[i] > VelocityFloor || vy[i] < -VelocityFloor) return false;
        }
        return true;
    }

    private float CurrentDrainReach()
    {
        var t = MathF.Min(1f, _drainOpenFrames / (float)DrainReachRampFrames);
        var s = t * t * (3f - 2f * t);
        return DrainReachInitial + (DrainReachMax - DrainReachInitial) * s;
    }

    /// <summary>
    /// Per-cell acceleration: pressure gradient pushes velocity from
    /// high water toward low water; while the drain is open each
    /// cell within reach also receives a radial-inward
    /// acceleration weighted by a Gaussian on its distance to the
    /// drain centre. Boundaries reflect by mirroring the centre
    /// height into the off-grid neighbour — so the wall pushes back
    /// on water trying to flow into it.
    /// </summary>
    private void ApplyForces()
    {
        var dw = _dw; var dh = _dh;
        var h = _height; var vx = _vx; var vy = _vy;
        var solid = _solid;
        var drainOn = _drainActive;
        var cx = (float)_drainCx;
        var cy = (float)_drainCy;
        var reach = CurrentDrainReach();
        var twoR2 = 2f * reach * reach;
        var skip2 = (3f * reach) * (3f * reach);
        var pull = PullStrength * _strength;

        for (var y = 0; y < dh; y++)
        {
            var row = y * dw;
            for (var x = 0; x < dw; x++)
            {
                var i = row + x;
                if (solid[i]) { vx[i] = 0f; vy[i] = 0f; continue; }
                var hi = h[i];
                var hL = (x > 0     && !solid[i - 1])  ? h[i - 1]  : hi;
                var hR = (x + 1 < dw && !solid[i + 1])  ? h[i + 1]  : hi;
                var hU = (y > 0     && !solid[i - dw]) ? h[i - dw] : hi;
                var hD = (y + 1 < dh && !solid[i + dw]) ? h[i + dw] : hi;
                // Surface tension: only the depth above FilmThickness contributes
                // to the pressure gradient. A thin film has no net force on it.
                var eL = MathF.Max(0f, hL - FilmThickness);
                var eR = MathF.Max(0f, hR - FilmThickness);
                var eU = MathF.Max(0f, hU - FilmThickness);
                var eD = MathF.Max(0f, hD - FilmThickness);
                var gradX = (eR - eL) * 0.5f;
                var gradY = (eD - eU) * 0.5f;
                vx[i] -= gradX * PressureGain;
                vy[i] -= gradY * PressureGain;

                // Drain only attracts cells that actually hold water above the
                // film threshold — a dry cell on the other side of the screen
                // doesn't feel the drain until water reaches it. The
                // attraction is also scaled down as a cell approaches full
                // depth, so water can't "climb" into a mound around the
                // drain that's deeper than the surrounding basin.
                if (drainOn && hi > FilmThickness)
                {
                    var dxc = x - cx;
                    var dyc = y - cy;
                    var d2 = dxc * dxc + dyc * dyc;
                    if (d2 > skip2 || d2 < 0.1f) continue;
                    var falloff = MathF.Exp(-d2 / twoR2);
                    var capacity = MathF.Max(0f, 1f - hi / (float)D);
                    capacity *= capacity; // sharper rolloff as the cell nears full
                    var w = falloff * capacity;
                    if (w <= 0f) continue;
                    var invR = 1f / MathF.Sqrt(d2);
                    var inX = -dxc * invR;
                    var inY = -dyc * invR;
                    vx[i] += pull * inX * w;
                    vy[i] += pull * inY * w;
                }
            }
        }
    }

    /// <summary>
    /// Multiplicative velocity damping with a hard cap and a small
    /// floor that zeroes near-quiescent noise. SIMD-friendly tight
    /// loop with no branches inside the cap; on .NET 8+ this could
    /// be vectorised over Vector&lt;float&gt;.
    /// </summary>
    private void ApplyDamping()
    {
        var vx = _vx; var vy = _vy;
        var n = vx.Length;
        for (var i = 0; i < n; i++)
        {
            var x = vx[i] * Damping;
            var y = vy[i] * Damping;
            if (x >  MaxSpeed) x =  MaxSpeed;
            if (x < -MaxSpeed) x = -MaxSpeed;
            if (y >  MaxSpeed) y =  MaxSpeed;
            if (y < -MaxSpeed) y = -MaxSpeed;
            if (x > -VelocityFloor && x < VelocityFloor) x = 0f;
            if (y > -VelocityFloor && y < VelocityFloor) y = 0f;
            vx[i] = x;
            vy[i] = y;
        }
    }

    /// <summary>
    /// Continuous-flux advection. For each X edge (between cells
    /// i and i+1 in the same row) and each Y edge (between cells i
    /// and i+_dw in the same column) we compute volumetric flux as
    /// <c>avg(velocity) × upwind(effective height)</c> and apply a
    /// fraction (<see cref="AdvectionRate"/>) of it directly to the
    /// height field — no integer accumulator, no per-edge state.
    ///
    /// With float heights, gravity can drive water to its true
    /// equilibrium (flat surface) instead of getting stuck on a
    /// slope-1 integer staircase, and the pressure gradient never
    /// re-pumps the velocity to swap a voxel back the way it came.
    /// Mass is conserved exactly (clamped flux is symmetric across
    /// the edge), and the only remaining source of perpetual motion
    /// is the small floor on velocity, which damping kills quickly.
    /// </summary>
    private bool ApplyAdvection()
    {
        var dw = _dw; var dh = _dh;
        var h = _height; var vx = _vx; var vy = _vy;
        var solid = _solid;
        var moved = false;

        // X edges within each row.
        for (var y = 0; y < dh; y++)
        {
            var row = y * dw;
            for (var x = 0; x + 1 < dw; x++)
            {
                var i = row + x;
                var j = i + 1;
                if (solid[i] || solid[j]) continue;
                var v = (vx[i] + vx[j]) * 0.5f;
                if (v == 0f) continue;
                var hUp = v >= 0f ? h[i] : h[j];
                var eUp = hUp - FilmThickness;
                if (eUp <= 0f) continue;
                var flux = v * eUp * AdvectionRate;
                // Cap by available water at the source and free space
                // at the sink so the field stays within [0, D].
                if (flux > 0f)
                {
                    var room = D - h[j];
                    if (flux > h[i]) flux = h[i];
                    if (flux > room) flux = room;
                    if (flux <= 0f) continue;
                }
                else
                {
                    var avail = h[j];
                    var room = D - h[i];
                    var neg = -flux;
                    if (neg > avail) neg = avail;
                    if (neg > room)  neg = room;
                    if (neg <= 0f) continue;
                    flux = -neg;
                }
                h[i] -= flux;
                h[j] += flux;
                moved = true;
            }
        }

        // Y edges within each column.
        for (var y = 0; y + 1 < dh; y++)
        {
            var row = y * dw;
            for (var x = 0; x < dw; x++)
            {
                var i = row + x;
                var j = i + dw;
                if (solid[i] || solid[j]) continue;
                var v = (vy[i] + vy[j]) * 0.5f;
                if (v == 0f) continue;
                var hUp = v >= 0f ? h[i] : h[j];
                var eUp = hUp - FilmThickness;
                if (eUp <= 0f) continue;
                var flux = v * eUp * AdvectionRate;
                if (flux > 0f)
                {
                    var room = D - h[j];
                    if (flux > h[i]) flux = h[i];
                    if (flux > room) flux = room;
                    if (flux <= 0f) continue;
                }
                else
                {
                    var avail = h[j];
                    var room = D - h[i];
                    var neg = -flux;
                    if (neg > avail) neg = avail;
                    if (neg > room)  neg = room;
                    if (neg <= 0f) continue;
                    flux = -neg;
                }
                h[i] -= flux;
                h[j] += flux;
                moved = true;
            }
        }
        return moved;
    }

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
            if (_solid[i]) continue;
            if (_height[i] > 0f)
            {
                var take = MathF.Min(0.5f, _height[i]);
                _height[i] -= take;
                changed = true;
            }
        }
        return changed;
    }

    private bool ApplyRefill()
    {
        var r = InletRadius;
        var r2 = r * r;
        var x0 = Math.Max(0, (int)(_inletCx - r));
        var x1 = Math.Min(_dw - 1, (int)(_inletCx + r + 0.5f));
        var y0 = Math.Max(0, (int)(_inletCy - r));
        var y1 = Math.Min(_dh - 1, (int)(_inletCy + r + 0.5f));
        var spanW = x1 - x0 + 1;
        var spanH = y1 - y0 + 1;
        if (spanW <= 0 || spanH <= 0) return false;

        var changed = false;
        var per = (int)MathF.Max(1f, InletFlowPerFrame * _strength);
        for (var n = 0; n < per; n++)
        {
            var rx = x0 + (int)(NextFloat() * spanW);
            var ry = y0 + (int)(NextFloat() * spanH);
            var dx = rx - _inletCx;
            var dy = ry - _inletCy;
            if (dx * dx + dy * dy > r2) continue;
            var i = ry * _dw + rx;
            if (_solid[i]) continue;
            if (_height[i] < D)
            {
                var room = D - _height[i];
                var add = MathF.Min(0.5f, room);
                _height[i] += add;
                changed = true;
            }
        }
        return changed;
    }

    private void DrawTank(Surface surface)
    {
        var w = surface.Width;
        var hsz = surface.Height;

        for (var x = 0; x < w; x++)
        {
            surface[x, 0] = new SurfaceCell("▄", WallColour, WallTrim);
            surface[x, hsz - 1] = new SurfaceCell("▀", WallColour, WallTrim);
        }
        for (var y = 1; y < hsz - 1; y++)
        {
            surface[0, y] = new SurfaceCell("█", WallColour, WallColour);
            surface[w - 1, y] = new SurfaceCell("█", WallColour, WallColour);
        }

        var dw = _dw;
        var heights = _height;
        var solid = _solid;
        for (var cy = WallCells; cy < hsz - WallCells; cy++)
        {
            var iy = cy - WallCells;
            var topRow = (iy * 2) * dw;
            var botRow = (iy * 2 + 1) * dw;
            for (var cx = WallCells; cx < w - WallCells; cx++)
            {
                var ix = cx - WallCells;
                var topI = topRow + ix;
                var botI = botRow + ix;
                var topCol = solid[topI] ? RockColour : DepthColour(heights[topI]);
                var botCol = solid[botI] ? RockColour : DepthColour(heights[botI]);
                surface[cx, cy] = new SurfaceCell("▀", topCol, botCol);
            }
        }

        if (_inletActive)
        {
            var inCx = _inletCx + WallCells;
            var inCy = _inletCy / 2 + WallCells;
            if (inCx >= WallCells && inCx < w - WallCells
                && inCy >= WallCells && inCy < hsz - WallCells)
            {
                var iy = inCy - WallCells;
                var botRow = (iy * 2 + 1) * dw;
                var ix = inCx - WallCells;
                surface[inCx, inCy] = new SurfaceCell("◎",
                    InletColour, DepthColour(heights[botRow + ix]));
            }
        }

        if (_drainActive)
        {
            var holeCx = _drainCx + WallCells;
            var holeCy = _drainCy / 2 + WallCells;
            if (holeCx >= WallCells && holeCx < w - WallCells
                && holeCy >= WallCells && holeCy < hsz - WallCells)
            {
                var iy = holeCy - WallCells;
                var botRow = (iy * 2 + 1) * dw;
                var ix = holeCx - WallCells;
                surface[holeCx, holeCy] = new SurfaceCell("◉",
                    HoleColour, DepthColour(heights[botRow + ix]));
            }
        }
    }

    private static Hex1bColor DepthColour(float h)
    {
        if (h <= 0f) return Hex1bColor.FromRgb(SandColour.R, SandColour.G, SandColour.B);
        if (h >= D) return Hex1bColor.FromRgb(DeepColour.R, DeepColour.G, DeepColour.B);
        var t = h / D;
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
