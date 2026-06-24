using AsciiEarth;
using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;
using Hex1b.Scene.Textures;
using Hex1b.Widgets;
using SceneClass = Hex1b.Scene.Core.Scene;

// Drag/roll input step sizes.
const float RollStep = 0.10f;
const float DragYawScale = 0.018f;
const float DragPitchScale = 0.022f;

// Detail overlay patch tuning.
const float PatchRadius = 1.004f; // sits just above the base globe so it wins the depth test
const int PatchSegments = 40;     // enough to follow the sphere across the wide (≤13-tile) block
var detailFadeDuration = TimeSpan.FromMilliseconds(140);

// --- OSM tile + texture infrastructure -------------------------------------------------
using var tileClient = new RasterTileClient();
using var earth = new EarthTextureBuilder(tileClient, width: 1024, height: 512, maxZoom: OrbitController.BaseGlobeZoom);
using var detail = new DetailTextureBuilder(tileClient);

// --- Scene: a textured sphere lit by ambient light plus a fixed "sun" ------------------
var scene = new SceneClass("AsciiEarth");

var sphereMaterial = new SceneTextureMaterial(earth.Texture)
{
    WrapMode = TextureWrapMode.Repeat,
    FilterMode = TextureFilterMode.Linear
};
var sphereMesh = new SceneMesh(
    SphereGeometry.Create(radius: 1.0f, longitudeSegments: 64, latitudeSegments: 40),
    sphereMaterial,
    "Globe");
scene.AddChild(sphereMesh);

// High-detail overlay patch (added/removed from the scene as the user zooms in/out).
var detailMaterial = new SceneTextureMaterial(detail.Texture)
{
    WrapMode = TextureWrapMode.Clamp,
    FilterMode = TextureFilterMode.Linear
};
var patchMesh = new SceneMesh(SphereGeometry.Create(1.0f, 3, 2), detailMaterial, "Detail");

var ambientLight = new SceneAmbientLight("Ambient")
{
    Color = Vector3.One,
    Intensity = 0.58f
};
scene.AddChild(ambientLight);

// Fixed sun from the upper-left-front so the sphere reads as 3D as it spins.
var sun = new SceneDirectionalLight("Sun")
{
    Color = Vector3.One,
    Intensity = 0.75f,
    Rotation = DirectionToRotation(new Vector3(0.45f, -0.55f, -1.0f))
};
scene.AddChild(sun);

var camera = new ScenePerspectiveCamera("Camera")
{
    Near = 0.01f, // allow the camera to dive close to the surface for deep zoom
    Far = 50f
};
var orbit = new OrbitController();
DetailTextureBuilder.PublishedDetail? displayedDetail = null;
DetailTextureBuilder.PublishedDetail? pendingDetail = null;
DateTime fadeStartUtc = default;
var fadeTexture = new SceneTexture2D(EarthView.TilesX * EarthView.TilePixels, EarthView.TilesY * EarthView.TilePixels);
var fadePixels = new uint[fadeTexture.Width * fadeTexture.Height];
uint[]? fadeFromPixels = null;
uint[]? fadeToPixels = null;

// Kick off the first texture build at the starting zoom.
earth.RequestZoom(Math.Min(orbit.OsmZoom, OrbitController.BaseGlobeZoom));

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(
        o => o.EnableMouse = true,
        app => ctx =>
        {
            // Sync scene transforms from the orbit controller each frame.
            sphereMesh.Rotation = orbit.GlobeRotation;
            camera.Position = orbit.CameraPosition;
            camera.Rotation = orbit.CameraRotation;

            var (lat, lon) = orbit.CenterLatLon;
            var zoom = orbit.OsmZoom;

            // Base globe texture follows zoom up to the low global cap.
            earth.RequestZoom(Math.Min(zoom, OrbitController.BaseGlobeZoom));

            // High-zoom detail overlay: request the bounded tile block facing the camera. The block
            // carries a margin ring of tiles around the visible area so the texture can slide under
            // the view while panning, only re-downloading when the centre crosses into new tiles.
            if (orbit.DetailActive)
                detail.Request(EarthView.ComputeWindow(lat, lon, zoom));

            // Rebuild the detail patch every frame, magnifying the published tile block about the
            // *live* facing point. Because the magnification is about the point the user is looking
            // at and the imagery is mapped by true geography, panning slides the texture smoothly
            // across the mesh and a freshly-downloaded block drops in seamlessly where the old one
            // was — no jump when the tiles refresh. Past the globe-diving range the (tiny) window is
            // magnified so the patch keeps filling the view: a magnifier that sharpens each level.
            var publishedDetail = detail.Published;
            if (publishedDetail is { } latest)
            {
                displayedDetail ??= latest;
                if (displayedDetail is { } shown && latest.Version != shown.Version)
                {
                    // For zoom changes, keep showing the previous detail while the next level loads,
                    // then fade old→new in the same target window instead of falling back to the
                    // coarse base globe. For same-zoom block updates (panning), swap immediately.
                    if (latest.Window.Zoom != shown.Window.Zoom)
                    {
                        if (pendingDetail is null || pendingDetail.Value.Version != latest.Version)
                        {
                            pendingDetail = latest;
                            fadeStartUtc = DateTime.UtcNow;
                            fadeFromPixels = shown.Texture.GetPixels();
                            fadeToPixels = latest.Texture.GetPixels();
                        }
                    }
                    else
                    {
                        displayedDetail = latest;
                        pendingDetail = null;
                        fadeFromPixels = null;
                        fadeToPixels = null;
                    }
                }
            }

            var activeDetail = displayedDetail;
            if (displayedDetail is { } oldBlock && pendingDetail is { } newBlock)
            {
                var elapsed = DateTime.UtcNow - fadeStartUtc;
                var t = detailFadeDuration <= TimeSpan.Zero
                    ? 1.0
                    : Math.Clamp(elapsed.TotalSeconds / detailFadeDuration.TotalSeconds, 0.0, 1.0);

                if (fadeFromPixels is not null && fadeToPixels is not null && fadeFromPixels.Length == fadeToPixels.Length)
                {
                    BlendPixelArrays(fadeFromPixels, fadeToPixels, t, fadePixels);
                    fadeTexture.SetPixels(fadePixels);
                    detailMaterial.Texture = fadeTexture;
                    activeDetail = new DetailTextureBuilder.PublishedDetail(newBlock.Window, fadeTexture, newBlock.Version);
                }
                else
                {
                    // If we couldn't prepare a fade buffer, fall back to an immediate atomic swap.
                    detailMaterial.Texture = newBlock.Texture;
                    activeDetail = newBlock;
                    t = 1.0;
                }

                if (t >= 1.0)
                {
                    displayedDetail = newBlock;
                    pendingDetail = null;
                    fadeFromPixels = null;
                    fadeToPixels = null;
                }
            }
            else if (displayedDetail is { } stable)
            {
                detailMaterial.Texture = stable.Texture;
            }

            var showPatch = orbit.DetailActive
                && activeDetail is { } block
                && WindowCoversCenter(block.Window, lat, lon);
            if (showPatch && activeDetail is { } shownBlock)
            {
                var realRadius = shownBlock.Window.AngularRadiusRad;
                var mag = realRadius > 1e-9
                    ? Math.Max(1.0, OrbitController.MagnifierAngularRadius / realRadius)
                    : 1.0;
                patchMesh.Geometry =
                    DetailPatchGeometry.Create(shownBlock.Window, lat, lon, mag, PatchRadius, PatchSegments);
                patchMesh.Rotation = orbit.GlobeRotation;
                if (patchMesh.Parent is null)
                    scene.AddChild(patchMesh);
            }
            else if (patchMesh.Parent is not null)
            {
                scene.RemoveChild(patchMesh);
            }

            var status = detail.IsBuilding
                ? "loading detail…"
                : earth.IsBuilding ? "loading globe…" : "ready";
            var header =
                $" AsciiEarth   center {Format(lat, 'N', 'S')}, {Format(lon, 'E', 'W')}   " +
                $"OSM z{zoom}/{OrbitController.MaxZoom}   {status} ";

            return ctx.Interactable(ic =>
                ic.Grid(g =>
                {
                    g.Columns.Add(SizeHint.Fill);
                    g.Rows.Add(SizeHint.Fixed(1));
                    g.Rows.Add(SizeHint.Fill);
                    g.Rows.Add(SizeHint.Fixed(1));

                    return
                    [
                        g.Cell(c => c.Text(header)).Row(0).Column(0),

                        g.Cell(c => c.Border(b => [b.Scene(scene, camera)])
                            .Title("© OpenStreetMap contributors"))
                            .Row(1).Column(0),

                        g.Cell(c => c.Text(
                            " drag rotate · wheel zoom · WASD pan · ↑/↓ zoom · ←/→ roll · Esc quit "))
                            .Row(2).Column(0)
                    ];
                }))
                .InputBindings(bindings =>
                {
                    bindings.Drag(MouseButton.Left).Action((_, _) =>
                    {
                        var lastDx = 0;
                        var lastDy = 0;
                        return new DragHandler((_, dx, dy) =>
                        {
                            var stepDx = dx - lastDx;
                            var stepDy = dy - lastDy;
                            lastDx = dx;
                            lastDy = dy;
                            if (stepDx == 0 && stepDy == 0)
                                return;
                            var scale = orbit.SurfaceScale;
                            orbit.RotateScreen(stepDx * DragYawScale * scale, stepDy * DragPitchScale * scale);
                            app.Invalidate();
                        });
                    }, "Rotate the globe by dragging");

                    bindings.Mouse(MouseButton.ScrollUp).Action(_ =>
                    {
                        orbit.ZoomIn();
                        app.Invalidate();
                    }, "Zoom in");

                    bindings.Mouse(MouseButton.ScrollDown).Action(_ =>
                    {
                        orbit.ZoomOut();
                        app.Invalidate();
                    }, "Zoom out");

                    bindings.Key(Hex1bKey.W).Global().Action(_ =>
                    {
                        orbit.PanNorth();
                        app.Invalidate();
                    }, "Pan north");

                    bindings.Key(Hex1bKey.S).Global().Action(_ =>
                    {
                        orbit.PanSouth();
                        app.Invalidate();
                    }, "Pan south");

                    bindings.Key(Hex1bKey.A).Global().Action(_ =>
                    {
                        orbit.PanWest();
                        app.Invalidate();
                    }, "Pan west");

                    bindings.Key(Hex1bKey.D).Global().Action(_ =>
                    {
                        orbit.PanEast();
                        app.Invalidate();
                    }, "Pan east");

                    bindings.Key(Hex1bKey.UpArrow).Global().Action(_ =>
                    {
                        orbit.ZoomIn();
                        app.Invalidate();
                    }, "Zoom in");

                    bindings.Key(Hex1bKey.DownArrow).Global().Action(_ =>
                    {
                        orbit.ZoomOut();
                        app.Invalidate();
                    }, "Zoom out");

                    bindings.Key(Hex1bKey.LeftArrow).Global().Action(_ =>
                    {
                        orbit.Roll(RollStep);
                        app.Invalidate();
                    }, "Roll left");

                    bindings.Key(Hex1bKey.RightArrow).Global().Action(_ =>
                    {
                        orbit.Roll(-RollStep);
                        app.Invalidate();
                    }, "Roll right");
                })
                // Keep redrawing so async tile loads appear and held keys feel responsive.
                .RedrawAfter(120);
        })
    .Build();

await terminal.RunAsync();

// Builds a rotation that maps the default light forward (-Z) onto the given direction.
static Quaternion DirectionToRotation(Vector3 direction)
{
    var from = new Vector3(0, 0, -1);
    var to = direction.Normalized;
    var dot = Vector3.Dot(from, to);

    if (dot > 0.9999f)
        return Quaternion.Identity;
    if (dot < -0.9999f)
        return Quaternion.FromAxisAngle(Vector3.UnitY, MathF.PI);

    var axis = Vector3.Cross(from, to).Normalized;
    var angle = MathF.Acos(Math.Clamp(dot, -1f, 1f));
    return Quaternion.FromAxisAngle(axis, angle);
}

static void BlendPixelArrays(uint[] from, uint[] to, double t, uint[] output)
{
    t = Math.Clamp(t, 0.0, 1.0);
    var len = Math.Min(output.Length, Math.Min(from.Length, to.Length));
    for (var i = 0; i < len; i++)
        output[i] = LerpRgba(from[i], to[i], t);
}

static uint LerpRgba(uint from, uint to, double t)
{
    var fr = (byte)(from >> 24);
    var fg = (byte)(from >> 16);
    var fb = (byte)(from >> 8);
    var fa = (byte)from;

    var tr = (byte)(to >> 24);
    var tg = (byte)(to >> 16);
    var tb = (byte)(to >> 8);
    var ta = (byte)to;

    static byte Blend(byte a, byte b, double w)
        => (byte)Math.Clamp((int)Math.Round(a + ((b - a) * w)), 0, 255);

    var r = Blend(fr, tr, t);
    var g = Blend(fg, tg, t);
    var b = Blend(fb, tb, t);
    var a = Blend(fa, ta, t);
    return ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | a;
}

static string Format(double degrees, char positive, char negative)
{
    var hemisphere = degrees >= 0 ? positive : negative;
    return $"{Math.Abs(degrees):0.0}°{hemisphere}";
}

// True when the published detail window encloses (within one window of over-pan) the current center
// lat/lon. The margin keeps the patch visible while panning a little past the block's edge before
// the next block loads, while still rejecting a far-away stale window after a large jump. Longitude
// bounds are continuous (may run past ±180 near the antimeridian), so test ±360 offsets too.
static bool WindowCoversCenter(EarthView.Window window, double lat, double lon)
{
    var latMargin = window.North - window.South;
    if (lat > window.North + latMargin || lat < window.South - latMargin)
        return false;
    var lonMargin = window.East - window.West;
    for (var k = -1; k <= 1; k++)
    {
        var shifted = lon + k * 360.0;
        if (shifted >= window.West - lonMargin && shifted <= window.East + lonMargin)
            return true;
    }
    return false;
}
