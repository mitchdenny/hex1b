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
    private readonly CameraRig _cameraRig;

    private float _previousProgress;
    private float _lapTime;

    public InputState Input { get; } = new();
    public int Lap { get; private set; } = 1;
    public float BestLapTime { get; private set; }

    public RaceGame()
    {
        _track = new FigureEightTrack();

        var tuning = new VehicleTuning();
        _vehicle = new ArcadeVehicle(tuning, _track.StartPosition, _track.StartHeading);

        var ramps = BuildRamps(_track);
        var obstacles = BuildObstacles(_track);
        _world = new PhysicsWorld(_track, ramps, obstacles);

        BuildScene(ramps, obstacles);

        _cameraRig = new CameraRig(_vehicle.WorldPosition);
        _cameraRig.Snap(_vehicle.WorldPosition);

        _previousProgress = _track.Progress(_vehicle.PositionXZ);
    }

    public float SpeedDisplay => MathF.Abs(_vehicle.ForwardSpeed) * 5.0f;
    public bool Airborne => !_vehicle.Grounded;
    public float AirTime => _vehicle.AirTime;
    public bool OnTrack => _world.IsOnTrack(_vehicle.PositionXZ.X, _vehicle.PositionXZ.Y);

    public void Update(float dt)
    {
        if (Input.ResetRequested)
        {
            _vehicle.Reset(_track.StartPosition, _track.StartHeading);
            _cameraRig.Snap(_vehicle.WorldPosition);
            _lapTime = 0.0f;
            _previousProgress = _track.Progress(_vehicle.PositionXZ);
            Input.ClearResetRequest();
        }

        var input = Input.ToVehicleInput();
        Input.Decayed(dt);

        _vehicle.Step(dt, input, _world);

        _vehicleModel.SyncTo(_vehicle);
        _vehicleModel.SetSteer(input.Steer * 0.5f);

        _cameraRig.Update(_vehicle.WorldPosition, dt);

        TrackLaps(dt);
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
        _scene.AddChild(TrackMeshBuilder.BuildGround());
        _scene.AddChild(TrackMeshBuilder.BuildTrackRibbon(_track));

        foreach (var ramp in ramps)
        {
            _scene.AddChild(RampModel.Build(ramp));
        }

        foreach (var obstacle in obstacles)
        {
            _scene.AddChild(PropFactory.BuildObstacle(obstacle));
        }

        _scene.AddChild(PropFactory.BuildStartMarker(_track.StartPosition, _track.StartHeading));
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
            const float length = 4.5f;
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
        var progress = _track.Progress(_vehicle.PositionXZ);

        // Detect a forward crossing of the start line (progress wraps high -> low).
        if (_previousProgress > 0.8f && progress < 0.2f)
        {
            Lap++;
            if (BestLapTime <= 0.0f || _lapTime < BestLapTime)
            {
                BestLapTime = _lapTime;
            }

            _lapTime = 0.0f;
        }

        _previousProgress = progress;
    }
}
