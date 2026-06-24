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

// Auto motion / tour tuning.
const float AutoRotateYawRate = -0.20f; // radians/sec, camera-relative yaw
const float TourSpinYawRate = -0.07f;   // slow constant spin while tracking cities
const float TourSteerGain = 0.090f;     // proportional steering gain toward target city
const float TourMaxStep = 0.030f;       // clamp per-frame steer step
const int TourMinZoom = 4;
const int TourMaxZoom = 12;
var tourZoomPeriod = TimeSpan.FromSeconds(18);
var tourZoomStepPeriod = TimeSpan.FromMilliseconds(320);
var tourCityDuration = TimeSpan.FromSeconds(14);

var tourCities = new (string Name, double Lat, double Lon)[]
{
    ("Canberra", -35.2809, 149.1300),
    ("Tokyo", 35.6764, 139.6500),
    ("New Delhi", 28.6139, 77.2090),
    ("Cairo", 30.0444, 31.2357),
    ("Paris", 48.8566, 2.3522),
    ("Brasilia", -15.7939, -47.8828),
    ("Washington", 38.9072, -77.0369),
    ("Ottawa", 45.4215, -75.6972),
    ("London", 51.5074, -0.1278),
    ("Cape Town", -33.9249, 18.4241),
    ("Wellington", -41.2865, 174.7762)
};

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
var autoRotateEnabled = false;
var tourModeEnabled = false;
var lastMotionTickUtc = DateTime.UtcNow;
var tourStartedUtc = DateTime.UtcNow;
var nextTourCitySwitchUtc = DateTime.UtcNow + tourCityDuration;
var nextTourZoomStepUtc = DateTime.UtcNow;
var tourCityIndex = 0;
DetailTextureBuilder.PublishedDetail? displayedDetail = null;
DetailTextureBuilder.PublishedDetail? proxyDetail = null;
var proxySourceVersion = -1;
EarthView.Window? proxyRequestedWindow = null;
var proxyRequestedSourceVersion = -1;
var proxyGeneration = 0;
var proxyTask = Task.CompletedTask;
var proxyGate = new object();
var proxyIsBuilding = false;

void EnsureProxyForTarget(DetailTextureBuilder.PublishedDetail source, EarthView.Window target)
{
    lock (proxyGate)
    {
        var hasUsableProxy = proxyDetail is { } existing
            && existing.Window.Zoom == target.Zoom
            && existing.Window.MinTileX == target.MinTileX
            && existing.Window.MinTileY == target.MinTileY
            && proxySourceVersion == source.Version;
        var sameRequestInFlight = proxyIsBuilding
            && proxyRequestedWindow is { } requested
            && requested.Zoom == target.Zoom
            && requested.MinTileX == target.MinTileX
            && requested.MinTileY == target.MinTileY
            && proxyRequestedSourceVersion == source.Version;
        if (hasUsableProxy || sameRequestInFlight)
            return;

        proxyIsBuilding = true;
        proxyRequestedWindow = target;
        proxyRequestedSourceVersion = source.Version;
        var generation = ++proxyGeneration;
        proxyTask = Task.Run(() =>
        {
            try
            {
                var texture = BuildProxyTextureFromSource(source, target);
                lock (proxyGate)
                {
                    if (generation != proxyGeneration)
                        return;
                    proxyDetail = new DetailTextureBuilder.PublishedDetail(target, texture, source.Version);
                    proxySourceVersion = source.Version;
                    proxyIsBuilding = false;
                    proxyRequestedWindow = null;
                    proxyRequestedSourceVersion = -1;
                }
            }
            catch
            {
                lock (proxyGate)
                {
                    if (generation == proxyGeneration)
                    {
                        proxyIsBuilding = false;
                        proxyRequestedWindow = null;
                        proxyRequestedSourceVersion = -1;
                    }
                }
            }
        });
    }
}

// Kick off the first texture build at the starting zoom.
earth.RequestZoom(Math.Min(orbit.OsmZoom, OrbitController.BaseGlobeZoom));

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(
        o => o.EnableMouse = true,
        app => ctx =>
        {
            var nowUtc = DateTime.UtcNow;
            var dt = (float)Math.Clamp((nowUtc - lastMotionTickUtc).TotalSeconds, 0.0, 0.25);
            lastMotionTickUtc = nowUtc;

            if (tourModeEnabled)
            {
                while (nowUtc >= nextTourCitySwitchUtc)
                {
                    tourCityIndex = (tourCityIndex + 1) % tourCities.Length;
                    nextTourCitySwitchUtc += tourCityDuration;
                }

                var targetCity = tourCities[tourCityIndex];
                var (currLat, currLon) = orbit.CenterLatLon;
                var deltaLat = targetCity.Lat - currLat;
                var deltaLon = NormalizeLonDeltaDegrees(targetCity.Lon - currLon);

                var steerYaw = Math.Clamp((float)(-deltaLon * TourSteerGain * dt), -TourMaxStep, TourMaxStep);
                var steerPitch = Math.Clamp((float)(deltaLat * TourSteerGain * dt), -TourMaxStep, TourMaxStep);
                orbit.RotateScreen(steerYaw + (TourSpinYawRate * dt), steerPitch);

                if (nowUtc >= nextTourZoomStepUtc)
                {
                    var phase = (nowUtc - tourStartedUtc).TotalSeconds / tourZoomPeriod.TotalSeconds;
                    var wave = 0.5 + (0.5 * Math.Sin(phase * Math.PI * 2.0));
                    var targetZoom = (int)Math.Round(TourMinZoom + ((TourMaxZoom - TourMinZoom) * wave));
                    if (orbit.OsmZoom < targetZoom)
                        orbit.ZoomIn();
                    else if (orbit.OsmZoom > targetZoom)
                        orbit.ZoomOut();
                    nextTourZoomStepUtc = nowUtc + tourZoomStepPeriod;
                }
            }
            else if (autoRotateEnabled)
            {
                orbit.RotateScreen(AutoRotateYawRate * dt * Math.Max(orbit.SurfaceScale, 0.03f), 0f);
            }

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
            EarthView.Window? targetWindow = null;
            if (orbit.DetailActive)
            {
                targetWindow = EarthView.ComputeWindow(lat, lon, zoom);
                detail.Request(targetWindow.Value);
            }

            // Keep the most recent real published snapshot around. For same-zoom updates (panning),
            // apply immediately. For zoom transitions, we'll either use a proxy in the target window
            // or keep showing the previous detail while the proxy/new LOD is pending.
            var publishedDetail = detail.Published;
            if (publishedDetail is { } latest)
            {
                displayedDetail ??= latest;

                if (latest.Window.Zoom == zoom)
                {
                    displayedDetail = latest;
                    lock (proxyGate)
                    {
                        proxyDetail = null;
                        proxySourceVersion = -1;
                        proxyGeneration++;
                        proxyIsBuilding = false;
                        proxyRequestedWindow = null;
                        proxyRequestedSourceVersion = -1;
                    }
                }
            }

            DetailTextureBuilder.PublishedDetail? activeDetail = null;
            if (orbit.DetailActive && targetWindow is { } target)
            {
                if (displayedDetail is { } realAtZoom && realAtZoom.Window.Zoom == zoom)
                {
                    activeDetail = realAtZoom;
                    detailMaterial.Texture = realAtZoom.Texture;
                }
                else
                {
                    DetailTextureBuilder.PublishedDetail? proxyNow = null;
                    lock (proxyGate)
                    {
                        if (proxyDetail is { } p
                            && p.Window.Zoom == target.Zoom
                            && p.Window.MinTileX == target.MinTileX
                            && p.Window.MinTileY == target.MinTileY)
                            proxyNow = p;
                    }

                    if (proxyNow is { } proxyAtZoom)
                    {
                        activeDetail = proxyAtZoom;
                        detailMaterial.Texture = proxyAtZoom.Texture;
                    }
                    else if (displayedDetail is { } previousReal)
                    {
                        activeDetail = previousReal;
                        detailMaterial.Texture = previousReal.Texture;
                        EnsureProxyForTarget(previousReal, target);
                    }
                }
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

            bool isProxyBuilding;
            lock (proxyGate)
                isProxyBuilding = proxyIsBuilding;
            var status = (detail.IsBuilding || isProxyBuilding)
                ? "loading detail…"
                : earth.IsBuilding ? "loading globe…" : "ready";
            var motion = tourModeEnabled
                ? $"tour: {tourCities[tourCityIndex].Name}"
                : autoRotateEnabled ? "auto-rotate" : "manual";
            var header =
                $" AsciiEarth   center {Format(lat, 'N', 'S')}, {Format(lon, 'E', 'W')}   " +
                $"OSM z{zoom}/{OrbitController.MaxZoom}   {motion}   {status} ";

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
                            " drag rotate · wheel zoom · WASD pan · ↑/↓ zoom · ←/→ roll · R auto · T tour · Esc quit "))
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

                    bindings.Key(Hex1bKey.R).Global().Action(_ =>
                    {
                        if (tourModeEnabled)
                            tourModeEnabled = false;
                        autoRotateEnabled = !autoRotateEnabled;
                        app.Invalidate();
                    }, "Toggle auto-rotate");

                    bindings.Key(Hex1bKey.T).Global().Action(_ =>
                    {
                        tourModeEnabled = !tourModeEnabled;
                        if (tourModeEnabled)
                        {
                            autoRotateEnabled = false;
                            tourStartedUtc = DateTime.UtcNow;
                            nextTourCitySwitchUtc = tourStartedUtc + tourCityDuration;
                            nextTourZoomStepUtc = tourStartedUtc;
                            var (_, currLon) = orbit.CenterLatLon;
                            tourCityIndex = NearestCityIndexByLongitude(currLon, tourCities);
                        }
                        app.Invalidate();
                    }, "Toggle world-capitals tour");
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

static int NearestCityIndexByLongitude(double lon, (string Name, double Lat, double Lon)[] cities)
{
    var best = 0;
    var bestDelta = double.MaxValue;
    for (var i = 0; i < cities.Length; i++)
    {
        var d = Math.Abs(NormalizeLonDeltaDegrees(cities[i].Lon - lon));
        if (d < bestDelta)
        {
            bestDelta = d;
            best = i;
        }
    }
    return best;
}

static double NormalizeLonDeltaDegrees(double delta)
{
    while (delta > 180.0) delta -= 360.0;
    while (delta < -180.0) delta += 360.0;
    return delta;
}

static SceneTexture2D BuildProxyTextureFromSource(
    DetailTextureBuilder.PublishedDetail source,
    EarthView.Window target)
{
    var width = EarthView.TilesX * EarthView.TilePixels;
    var height = EarthView.TilesY * EarthView.TilePixels;
    var pixels = new uint[width * height];
    var denomX = Math.Max(1, width - 1);
    var denomY = Math.Max(1, height - 1);

    for (var y = 0; y < height; y++)
    {
        var fy = (double)y / denomY;
        var tileY = target.MinTileY + fy * EarthView.TilesY;
        var lat = TileCoordinates.TileToLatLon(0, tileY, target.Zoom).Lat;

        for (var x = 0; x < width; x++)
        {
            var fx = (double)x / denomX;
            var tileX = target.MinTileX + fx * EarthView.TilesX;
            var lon = TileCoordinates.TileToLatLon(tileX, 0, target.Zoom).Lon;
            pixels[(y * width) + x] = SampleDetailAtLatLon(source, lat, lon);
        }
    }

    var texture = new SceneTexture2D(width, height);
    texture.SetPixels(pixels);
    return texture;
}

static uint SampleDetailAtLatLon(DetailTextureBuilder.PublishedDetail snapshot, double lat, double lon)
{
    var window = snapshot.Window;
    var (tileX, tileY) = TileCoordinates.LatLonToTile(lat, lon, window.Zoom);
    var n = 1 << window.Zoom;

    var dx = WrapTileDelta(tileX - window.MinTileX, n);
    var dy = tileY - window.MinTileY;
    var u = (float)(dx / EarthView.TilesX);
    var v = (float)(dy / EarthView.TilesY);
    return snapshot.Texture.SampleBilinear(u, v, TextureWrapMode.Clamp);
}

static double WrapTileDelta(double delta, int n)
{
    if (n <= 0)
        return delta;
    while (delta < -n * 0.5) delta += n;
    while (delta > n * 0.5) delta -= n;
    return delta;
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
