using Hex1b.Scene.Math;

namespace AsciiEarth;

/// <summary>
/// Holds globe orientation, camera roll, and a discrete OSM zoom level for AsciiEarth, and
/// translates user input into camera-relative rotations.
/// </summary>
/// <remarks>
/// The camera sits on +Z looking at the origin; the globe (a unit sphere) rotates beneath it.
/// Zoom is an integer OSM level: low levels frame the whole globe, higher levels derive a camera
/// distance (via <see cref="EarthView.FramingDistance"/>) that dives toward the surface so the
/// bounded detail tile block fills the view. Panning rotates the globe about the camera's current
/// right/up axes (so it always tracks what the viewer sees, including any roll), scaled by
/// <see cref="SurfaceScale"/> so it stays usable as you zoom in. Roll spins the camera about its
/// view axis.
/// </remarks>
internal sealed class OrbitController
{
    /// <summary>Lowest OSM zoom (whole globe).</summary>
    public const int MinZoom = 2;

    /// <summary>Highest OSM zoom (street-level detail).</summary>
    public const int MaxZoom = 19;

    /// <summary>At or below this zoom the whole globe is framed and the base texture is used.</summary>
    public const int BaseGlobeZoom = 3;

    private const float FieldOfView = MathF.PI / 4f; // must match the perspective camera
    private const float MinDistance = 1.05f;         // closest the camera dives toward the surface
    private const float MaxDistance = 5.0f;
    private const float BasePanStep = 0.12f;

    // Whole-globe framing distances for the base zoom levels (all ≥ ~2.61 so the full disc shows).
    private const float GlobeNearDistance = 2.7f; // at BaseGlobeZoom
    private const float GlobeFarDistance = 3.4f;  // at MinZoom

    private static readonly double ReferenceAngularRadius =
        EarthView.ComputeWindow(0.0, 0.0, MinZoom).AngularRadiusRad;

    private Quaternion _globeRotation = Quaternion.Identity;
    private float _roll;
    private int _osmZoom = MinZoom;

    /// <summary>Current discrete OSM zoom level.</summary>
    public int OsmZoom => _osmZoom;

    /// <summary>Rotation applied to the globe mesh (and the detail patch).</summary>
    public Quaternion GlobeRotation => _globeRotation;

    /// <summary>Camera roll about its view axis, as a rotation quaternion.</summary>
    public Quaternion CameraRotation => Quaternion.FromAxisAngle(Vector3.UnitZ, _roll);

    /// <summary>Camera position (always on +Z, distance derived from the zoom level).</summary>
    public Vector3 CameraPosition => new(0, 0, Distance);

    /// <summary>Camera distance from the globe centre for the current zoom and centre.</summary>
    public float Distance
    {
        get
        {
            if (_osmZoom <= BaseGlobeZoom)
            {
                var span = BaseGlobeZoom - MinZoom;
                var t = span <= 0 ? 1f : (float)(_osmZoom - MinZoom) / span;
                return GlobeFarDistance + (GlobeNearDistance - GlobeFarDistance) * t;
            }

            var (lat, lon) = CenterLatLon;
            var window = EarthView.ComputeWindow(lat, lon, _osmZoom);
            return EarthView.FramingDistance(window.AngularRadiusRad, FieldOfView, MinDistance, MaxDistance);
        }
    }

    /// <summary>
    /// Fraction of the globe currently in view (1 at the widest zoom, shrinking as you zoom in),
    /// used to scale pan/drag so movement feels consistent at every zoom level.
    /// </summary>
    public float SurfaceScale
    {
        get
        {
            var (lat, lon) = CenterLatLon;
            var window = EarthView.ComputeWindow(lat, lon, _osmZoom);
            var ratio = (float)(window.AngularRadiusRad / ReferenceAngularRadius);
            return Math.Clamp(ratio, 0.02f, 1f);
        }
    }

    /// <summary>Latitude/longitude of the point currently facing the camera.</summary>
    public (double Lat, double Lon) CenterLatLon
    {
        get
        {
            // The sphere point nearest the camera is world +Z; undo the globe rotation to find
            // its position in the sphere's own (texture) space, then convert to lat/lon using the
            // same convention as SphereGeometry (nx = cosLat·sinLon, ny = sinLat, nz = cosLat·cosLon).
            var local = _globeRotation.Inverse.RotateVector(Vector3.UnitZ).Normalized;
            var lat = Math.Asin(Math.Clamp(local.Y, -1.0, 1.0)) * 180.0 / Math.PI;
            var lon = Math.Atan2(local.X, local.Z) * 180.0 / Math.PI;
            return (lat, lon);
        }
    }

    /// <summary>
    /// Rotates the globe about the camera's up axis (yaw) and right axis (pitch).
    /// Positive yaw scrolls the surface to the right; positive pitch reveals the north.
    /// </summary>
    public void RotateScreen(float yaw, float pitch)
    {
        var cam = CameraRotation;
        var up = cam.RotateVector(Vector3.UnitY);
        var right = cam.RotateVector(Vector3.UnitX);

        var delta = Quaternion.FromAxisAngle(up, yaw) * Quaternion.FromAxisAngle(right, pitch);
        _globeRotation = (delta * _globeRotation).Normalized;
    }

    /// <summary>W/S pan north/south (camera-relative, zoom-scaled).</summary>
    public void PanNorth() => RotateScreen(0f, BasePanStep * SurfaceScale);

    public void PanSouth() => RotateScreen(0f, -BasePanStep * SurfaceScale);

    /// <summary>A/D pan west/east (camera-relative, zoom-scaled).</summary>
    public void PanWest() => RotateScreen(BasePanStep * SurfaceScale, 0f);

    public void PanEast() => RotateScreen(-BasePanStep * SurfaceScale, 0f);

    /// <summary>Rolls the camera about its view axis (←/→).</summary>
    public void Roll(float amount) => _roll += amount;

    /// <summary>Zooms in one OSM level (more detail).</summary>
    public void ZoomIn() => _osmZoom = Math.Min(MaxZoom, _osmZoom + 1);

    /// <summary>Zooms out one OSM level.</summary>
    public void ZoomOut() => _osmZoom = Math.Max(MinZoom, _osmZoom - 1);

    /// <summary>True when the high-detail overlay patch should be shown at this zoom.</summary>
    public bool DetailActive => _osmZoom > BaseGlobeZoom;
}
