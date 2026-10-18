using System.Diagnostics;
using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;
using Hex1b.Widgets;
using Scene = Hex1b.Scene.Core.Scene;
using FlightVector = System.Numerics.Vector2;

namespace HelicopterDemo;

internal sealed class HelicopterDemoApp
{
    private const float CameraAltitude = 40;
    private readonly Scene _scene = new("Top-down gunship");
    private readonly TopDownCamera _camera = new() { Height = 22 };
    private readonly HelicopterModel _helicopter;
    private readonly SceneMesh _shadow = WorldGeometry.Disc(Vector3.Zero, 0.8f, 2.5f, new Vector3(0.035f, 0.05f, 0.035f));
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastFrame;
    private (int X, int Y, int Width, int Height)? _lastPointer;

    public FlightState Flight { get; } = new();
    public string LastCommand { get; private set; } = "Ready";
    public float ViewHeight => _camera.Height;

    public HelicopterDemoApp()
    {
        _helicopter = new HelicopterModel(HelicopterModel.AssetPath);
        WorldGeometry.Populate(_scene);
        _scene.AddChild(_shadow);
        _scene.AddChild(_helicopter.Root);
        _scene.AddChild(new SceneAmbientLight { Intensity = 0.7f });
        _scene.AddChild(new SceneDirectionalLight
        {
            Rotation = Quaternion.FromEulerAngles(-0.8f, 0.4f, 0),
            Intensity = 0.65f
        });
        _camera.Rotation = Quaternion.FromEulerAngles(-MathF.PI / 2, 0, 0);
    }

    public Hex1bWidget Build(RootContext context)
    {
        var now = _clock.Elapsed.TotalSeconds;
        Flight.Step((float)(now - _lastFrame));
        _lastFrame = now;
        _shadow.Position = new Vector3(Flight.Position.X + Flight.Altitude * 0.2f, 0.09f,
            Flight.Position.Y + Flight.Altitude * 0.25f);
        _shadow.Scale = Vector3.One * (1 + Flight.Altitude * 0.03f);
        _shadow.Rotation = Quaternion.FromAxisAngle(Vector3.UnitY, Flight.Heading);
        _camera.Position = new Vector3(Flight.Position.X, CameraAltitude, Flight.Position.Y);
        var heading = (360 - Flight.Heading * 180 / MathF.PI) % 360;
        var mode = Flight.Paused ? "PAUSED" : Flight.IsGrounded ? "LANDED" : "FLYING";
        var rotorPercent = Flight.RotorSpeed / FlightState.DefaultRotorSpeed * 100;

        return context.Grid(g =>
        {
            g.Columns.Add(SizeHint.Fill);
            g.Rows.Add(SizeHint.Fixed(2));
            g.Rows.Add(SizeHint.Fill);
            g.Rows.Add(SizeHint.Fixed(1));
            g.Rows.Add(SizeHint.Fixed(1));
            return
            [
                g.Cell(c => c.VStack(v =>
                [
                    v.Text($" GUNSHIP | {mode} | X {Flight.Position.X:0.0} Z {Flight.Position.Y:0.0} | Speed {Flight.Velocity.Length():0.0} | {LastCommand}"),
                    v.Text($" Altitude {Flight.Altitude:0.0} | V/S {Flight.VerticalVelocity:+0.0;-0.0;0.0} | Rotor {rotorPercent:0}% | Hover 100% level | Hdg {heading:000}")
                ]))
                    .Row(0),
                g.Cell(c => c.Border(b =>
                [
                    b.Interactable(ic => ic.Surface(surface =>
                    {
                        var pointer = (surface.MouseX, surface.MouseY, surface.Width, surface.Height);
                        // A stationary off-center pointer must not undo Brake or Reset.
                        if (_lastPointer != pointer)
                        {
                            Flight.AimAt(surface.MouseX, surface.MouseY, surface.Width, surface.Height);
                            _lastPointer = pointer;
                        }
                        _helicopter.Update(Flight);
                        return [surface.WidgetLayer(ic.Scene(_scene, _camera))];
                    }))
                    .OnHoverChanged(e =>
                    {
                        if (!e.IsHovered)
                            Flight.ReleaseControls();
                    })
                    .InputBindings(bindings =>
                    {
                        bindings.Drag(MouseButton.Left).Action((_, _) => StartMouseTurn(1));
                        bindings.Drag(MouseButton.Right).Action((_, _) => StartMouseTurn(-1));
                        bindings.Mouse(MouseButton.ScrollUp).Action(_ => ChangeRotorSpeed(1));
                        bindings.Mouse(MouseButton.ScrollDown).Action(_ => ChangeRotorSpeed(-1));
                    })
                ]).Title(" Top-down flight | Helicopter by Zsky / CC BY 3.0 "))
                    .Row(1),
                g.Cell(c => c.HStack(h =>
                [
                    h.Button("Reset").OnClick(_ => Reset()),
                    h.Button("Brake").OnClick(_ => Brake()),
                    h.Button(Flight.Paused ? "Resume" : "Pause").OnClick(_ => TogglePause()),
                    h.Button("Zoom +").OnClick(_ => Zoom(-2)),
                    h.Button("Zoom -").OnClick(_ => Zoom(2)),
                    h.Button("Quit").OnClick(e => e.Context.RequestStop())
                ])).Row(2),
                g.Cell(c => c.Text(" Mouse tilt | Wheel lift | Hold L/R turn | Space brake | P pause | R reset"))
                    .Row(3)
            ];
        })
        .RedrawAfter(33)
        .InputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.W).Global().Action(_ => Fly(0, -1, "North"), "Fly north");
            bindings.Key(Hex1bKey.S).Global().Action(_ => Fly(0, 1, "South"), "Fly south");
            bindings.Key(Hex1bKey.A).Global().Action(_ => Fly(-1, 0, "West"), "Fly west");
            bindings.Key(Hex1bKey.D).Global().Action(_ => Fly(1, 0, "East"), "Fly east");
            bindings.Key(Hex1bKey.UpArrow).Global().Action(_ => Fly(0, -1, "North"), "Fly north");
            bindings.Key(Hex1bKey.DownArrow).Global().Action(_ => Fly(0, 1, "South"), "Fly south");
            bindings.Key(Hex1bKey.LeftArrow).Global().Action(_ => Fly(-1, 0, "West"), "Fly west");
            bindings.Key(Hex1bKey.RightArrow).Global().Action(_ => Fly(1, 0, "East"), "Fly east");
            bindings.Key(Hex1bKey.Q).Global().Action(_ => Turn(0.18f), "Turn left");
            bindings.Key(Hex1bKey.E).Global().Action(_ => Turn(-0.18f), "Turn right");
            bindings.Key(Hex1bKey.Spacebar).Global().Action(_ => Brake(), "Brake");
            bindings.Key(Hex1bKey.P).Global().Action(_ => TogglePause(), "Pause/resume");
            bindings.Key(Hex1bKey.R).Global().Action(_ => Reset(), "Reset");
            bindings.Key(Hex1bKey.Z).Global().Action(_ => Zoom(-2), "Zoom in");
            bindings.Key(Hex1bKey.X).Global().Action(_ => Zoom(2), "Zoom out");
        });
    }

    private void Fly(float x, float z, string command)
    {
        if (Flight.Paused)
            return;
        Flight.Nudge(new FlightVector(x, z));
        LastCommand = command;
    }

    private void Turn(float radians)
    {
        if (Flight.Paused)
            return;
        Flight.Turn(radians);
        LastCommand = radians > 0 ? "Turn left" : "Turn right";
    }

    private DragHandler StartMouseTurn(int direction)
    {
        if (Flight.Paused)
            return new DragHandler();
        Flight.SetRotationDirection(direction);
        LastCommand = direction > 0 ? "Turn left" : "Turn right";
        return DragHandler.Simple(onEnd: () => Flight.SetRotationDirection(0));
    }

    private void ChangeRotorSpeed(int steps)
    {
        if (Flight.Paused)
            return;
        Flight.ChangeRotorSpeed(steps);
        LastCommand = steps > 0 ? "Rotor faster" : "Rotor slower";
    }

    private void Brake()
    {
        Flight.Brake();
        LastCommand = "Brake";
    }

    private void Reset()
    {
        Flight.Reset();
        LastCommand = "Ready";
        _lastFrame = _clock.Elapsed.TotalSeconds;
    }

    private void TogglePause()
    {
        Flight.Paused = !Flight.Paused;
        _lastFrame = _clock.Elapsed.TotalSeconds;
    }

    private void Zoom(float delta) => _camera.Height = Math.Clamp(_camera.Height + delta, 12, 32);

    private sealed class TopDownCamera : ScenePerspectiveCamera
    {
        public float Height { get; set; }

        public override Matrix4 GetProjectionMatrix(int viewportWidth, int viewportHeight)
        {
            FieldOfView = 2 * MathF.Atan(Height / (2 * CameraAltitude));
            return base.GetProjectionMatrix(viewportWidth, viewportHeight);
        }
    }
}
