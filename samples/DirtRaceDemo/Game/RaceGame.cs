namespace DirtRaceDemo.Game;

using DirtRaceDemo.Physics;
using DirtRaceDemo.Rendering;
using DirtRaceDemo.Scenery;
using DirtRaceDemo.Track;
using Hex1b;
using Hex1b.Layout;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;
using Hex1b.Widgets;
using SceneClass = Hex1b.Scene.Core.Scene;

/// <summary>
/// Top-level orchestrator for the racer. Owns the scene graph, track, physics world, vehicle
/// and camera, advances the simulation each frame and composes the on-screen layout.
/// </summary>
public sealed class RaceGame
{
    private readonly SceneClass _scene = new("Dirt Race");
    private readonly FigureEightTrack _track;
    private readonly PhysicsWorld _world;
    private readonly ArcadeVehicle _vehicle;
    private readonly VehicleModel _vehicleModel = new();
    private readonly GhostModel _ghostModel = new();
    private readonly LapRecorder _recorder = new();
    private readonly CameraRig _cameraRig = new();
    private readonly Hex1b.Scene.Core.SceneObject _ground = TrackMeshBuilder.BuildGround();
    private readonly float _groundLocalY = -0.02f;

    private float _previousProgress;
    private float _lapTime;
    private LapRecording? _bestLap;
    private bool _ghostVisible;
    private float _rideHeight;
    private Vector3 _cameraFocus;
    private float _cameraYaw;
    private float _cameraYawVelocity;
    private float _cameraSize = BaseCameraSize;
    private Vector2 _lastOnTrackPosition;
    private float _lastOnTrackHeading;

    private const float CameraFollowSpeed = 5.0f;

    // Speed-driven zoom: the orthographic size grows with the truck's actual speed so the camera
    // pulls back the faster it travels. Because dirt caps the top speed well below the track, the
    // camera naturally sits closer when scrabbling around off-track.
    private const float BaseCameraSize = 11.0f;
    private const float CameraZoomPerSpeed = 0.5f;
    private const float MaxCameraSize = 26.0f;
    private const float CameraZoomFollowSpeed = 3.0f;

    // How far past the track edge the truck may stray before it is dropped back onto the track at
    // the point where it last left it.
    private const float RespawnDistance = 14.0f;

    // Spring constants for the chase camera's yaw. A soft, critically damped spring makes the
    // camera swing in behind the truck gently rather than snapping to its heading.
    private const float CameraYawStiffness = 6.0f;
    private static readonly float CameraYawDamping = 2.0f * MathF.Sqrt(CameraYawStiffness);

    public InputState Input { get; } = new();
    public int Lap { get; private set; } = 1;
    public float BestLapTime { get; private set; }

    public RaceGame()
    {
        _track = new FigureEightTrack();

        var tuning = new VehicleTuning();
        _vehicle = new ArcadeVehicle(tuning, _track.StartPosition, _track.StartHeading);
        _rideHeight = tuning.RideHeight;

        var ramps = BuildRamps(_track);
        var obstacles = BuildObstacles(_track);
        _world = new PhysicsWorld(_track, ramps, obstacles);

        BuildScene(ramps, obstacles);

        _cameraFocus = _vehicle.WorldPosition;
        _cameraYaw = _vehicle.Heading;
        _cameraRig.SetYaw(_cameraYaw);
        ApplyCameraFollow();

        _lastOnTrackPosition = _vehicle.PositionXZ;
        _lastOnTrackHeading = _vehicle.Heading;
        _previousProgress = _track.Progress(_vehicle.PositionXZ);
    }

    public float SpeedDisplay => MathF.Abs(_vehicle.ForwardSpeed) * 5.0f;
    public int ThrottlePercent => (int)MathF.Round(Input.Cruise * 100.0f);
    public bool Airborne => !_vehicle.Grounded;
    public float AirTime => _vehicle.AirTime;
    public bool OnTrack => _world.IsOnTrack(_vehicle.PositionXZ.X, _vehicle.PositionXZ.Y);
    public bool GhostActive => _bestLap is not null;

    public void Update(float dt)
    {
        if (Input.ResetRequested)
        {
            _vehicle.Reset(_track.StartPosition, _track.StartHeading);
            Input.ResetCruise();
            _cameraFocus = _vehicle.WorldPosition;
            _cameraYaw = _vehicle.Heading;
            _cameraYawVelocity = 0.0f;
            _cameraSize = BaseCameraSize;
            _cameraRig.SetSize(_cameraSize);
            _cameraRig.SetYaw(_cameraYaw);
            ApplyCameraFollow();
            _lastOnTrackPosition = _vehicle.PositionXZ;
            _lastOnTrackHeading = _vehicle.Heading;
            _lapTime = 0.0f;
            _recorder.Reset();
            _previousProgress = _track.Progress(_vehicle.PositionXZ);
            Input.ClearResetRequest();
        }

        var input = Input.ToVehicleInput();
        Input.Decayed(dt);

        _vehicle.Step(dt, input, _world);
        EnforceTrackBounds();

        _vehicleModel.SyncTo(_vehicle);
        _vehicleModel.SetSteer(input.Steer * 0.5f);

        var t = 1.0f - MathF.Exp(-CameraFollowSpeed * dt);
        _cameraFocus = Vector3.Lerp(_cameraFocus, _vehicle.WorldPosition, t);
        ApplyCameraFollow();
        UpdateCameraYaw(dt);
        UpdateCameraZoom(dt);

        TrackLaps(dt);
        UpdateGhost();
    }

    /// <summary>
    /// Replays the best lap by positioning the ghost truck at the pose the player held at the same
    /// elapsed lap time. Hidden until a best lap exists; once shown it follows the recorded line.
    /// </summary>
    private void UpdateGhost()
    {
        if (_bestLap is null)
        {
            return;
        }

        var sample = _bestLap.Sample(_lapTime);
        var world = new Vector3(sample.Position.X, sample.Height + _rideHeight, sample.Position.Y);
        _ghostModel.SetTransform(world, sample.Heading);
    }

    /// <summary>
    /// Remembers the last point the truck was on the track and, if it strays too far onto the
    /// surrounding dirt, drops it back onto the track exactly where it left. Keeps the action on
    /// the circuit without an explicit reset.
    /// </summary>
    private void EnforceTrackBounds()
    {
        var position = _vehicle.PositionXZ;
        var nearest = _track.NearestSample(position, out _);
        var offTrackDistance = (nearest.Position - position).Magnitude;

        if (offTrackDistance <= _track.HalfWidth)
        {
            _lastOnTrackPosition = position;
            _lastOnTrackHeading = _vehicle.Heading;
            return;
        }

        if (offTrackDistance > RespawnDistance)
        {
            _vehicle.Reset(_lastOnTrackPosition, _lastOnTrackHeading);
        }
    }

    /// <summary>
    /// Eases the orthographic camera size toward a target derived from the truck's actual speed so
    /// the view zooms out as it accelerates and pulls back in as it slows (or bogs down on dirt).
    /// </summary>
    private void UpdateCameraZoom(float dt)
    {
        var target = MathF.Min(MaxCameraSize, BaseCameraSize + _vehicle.Speed * CameraZoomPerSpeed);
        var t = 1.0f - MathF.Exp(-CameraZoomFollowSpeed * dt);
        _cameraSize = _cameraSize + (target - _cameraSize) * t;
        _cameraRig.SetSize(_cameraSize);
    }

    /// <summary>
    /// Advances the spring that swings the camera in behind the truck. The spring tracks the
    /// truck's heading via the shortest angular path so the camera follows gently through turns
    /// instead of snapping around.
    /// </summary>
    private void UpdateCameraYaw(float dt)
    {
        var error = WrapAngle(_vehicle.Heading - _cameraYaw);
        _cameraYawVelocity += error * CameraYawStiffness * dt;
        _cameraYawVelocity -= _cameraYawVelocity * CameraYawDamping * dt;
        _cameraYaw += _cameraYawVelocity * dt;
        _cameraRig.SetYaw(_cameraYaw);
    }

    private static float WrapAngle(float angle)
    {
        while (angle > MathF.PI)
        {
            angle -= MathF.Tau;
        }

        while (angle < -MathF.PI)
        {
            angle += MathF.Tau;
        }

        return angle;
    }

    /// <summary>
    /// Translates the whole scene so the smoothed camera focus sits at the world origin. The chase
    /// camera always looks at the origin, so this keeps the truck centred while the track extends
    /// well beyond the viewport.
    /// </summary>
    private void ApplyCameraFollow()
    {
        _scene.Position = new Vector3(-_cameraFocus.X, 0.0f, -_cameraFocus.Z);

        // Keep the featureless dirt ground centred under the truck (world origin). Its single large
        // quad would otherwise project to extreme coordinates once the truck drives far from the
        // scene origin and drop out of the rasterizer; recentring it keeps the plane filling frame.
        _ground.Position = new Vector3(_cameraFocus.X, _groundLocalY, _cameraFocus.Z);
    }

    public SceneClass Scene => _scene;

    public SceneCamera Camera => _cameraRig.Camera;

    public Hex1bWidget BuildView(WidgetContext<InteractableWidget> context)
    {
        return context.Grid(g =>
        {
            g.Columns.Add(SizeHint.Fill);
            g.Rows.Add(SizeHint.Fixed(1));
            g.Rows.Add(SizeHint.Fill);
            g.Rows.Add(SizeHint.Fixed(1));

            return
            [
                g.Cell(c => c.Text(RaceHud.StatusLine(this))).Row(0).Column(0),
                g.Cell(c => c.Border(b => [b.Scene(_scene, _cameraRig.Camera)]).Title(" Dirt Race "))
                    .Row(1).Column(0),
                g.Cell(c => c.Text(RaceHud.ControlsLine())).Row(2).Column(0),
            ];
        })
        .RedrawAfter(33);
    }

    private void BuildScene(IReadOnlyList<Ramp> ramps, IReadOnlyList<Obstacle> obstacles)
    {
        _scene.AddChild(_ground);
        _scene.AddChild(TrackMeshBuilder.BuildTrackRibbon(_track));

        foreach (var ramp in ramps)
        {
            _scene.AddChild(RampModel.Build(ramp));
        }

        foreach (var obstacle in obstacles)
        {
            _scene.AddChild(PropFactory.BuildObstacle(obstacle));
        }

        _scene.AddChild(PropFactory.BuildStartMarker(_track.StartPosition, _track.StartHeading, _track.HalfWidth * 2.0f));
        _scene.AddChild(_vehicleModel.Root);
        _vehicleModel.SyncTo(_vehicle);

        var ambient = new SceneAmbientLight("Ambient")
        {
            Color = new Vector3(1.0f, 0.96f, 0.88f),
            Intensity = 0.45f,
        };
        var sun = new SceneDirectionalLight("Sun")
        {
            Color = new Vector3(1.0f, 0.95f, 0.82f),
            Intensity = 1.1f,
            Rotation = Quaternion.FromEulerAngles(-0.95f, 0.6f, 0.0f),
        };
        _scene.AddChild(ambient);
        _scene.AddChild(sun);
    }

    private static List<Ramp> BuildRamps(FigureEightTrack track)
    {
        var ramps = new List<Ramp>();
        foreach (var progress in new[] { 0.18f, 0.68f })
        {
            var sample = SampleAtProgress(track, progress);
            var heading = sample.Tangent;
            const float length = 9.0f;
            var front = sample.Position - heading * (length * 0.5f);
            ramps.Add(new Ramp(front, heading, length, track.HalfWidth * 2.0f, 1.3f));
        }

        return ramps;
    }

    private static List<Obstacle> BuildObstacles(FigureEightTrack track)
    {
        // A couple of boulders sitting in the infield, off the racing line.
        return new List<Obstacle>
        {
            new(new Vector2(track.Scale * 0.5f, 0.0f), 1.0f, 1.0f, 1.4f),
            new(new Vector2(-track.Scale * 0.5f, 0.0f), 1.2f, 0.8f, 1.2f),
        };
    }

    private static TrackSample SampleAtProgress(FigureEightTrack track, float progress)
    {
        var samples = track.Samples;
        var index = (int)(progress * samples.Count) % samples.Count;
        return samples[index];
    }

    private void TrackLaps(float dt)
    {
        _lapTime += dt;
        _recorder.Record(_lapTime, _vehicle);
        var progress = _track.Progress(_vehicle.PositionXZ);

        // Detect a forward crossing of the start line (progress wraps high -> low).
        if (_previousProgress > 0.8f && progress < 0.2f)
        {
            Lap++;
            if (BestLapTime <= 0.0f || _lapTime < BestLapTime)
            {
                BestLapTime = _lapTime;

                // New best lap: freeze the recorded line as the ghost to chase next lap.
                if (_recorder.HasSamples)
                {
                    _bestLap = _recorder.Build();
                    ShowGhost();
                }
            }

            _lapTime = 0.0f;
            _recorder.Reset();
        }

        _previousProgress = progress;
    }

    private void ShowGhost()
    {
        if (_ghostVisible)
        {
            return;
        }

        _scene.AddChild(_ghostModel.Root);
        _ghostVisible = true;
    }
}
