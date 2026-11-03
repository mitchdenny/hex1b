using System.Diagnostics;
using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;
using Hex1b.Widgets;
using Scene = Hex1b.Scene.Core.Scene;

namespace AnimatedHumanoidDemo;

internal sealed class HumanoidDemo
{
    private static readonly (string Name, string Label, bool Loop)[] Clips =
    [
        ("Idle", "1 Idle", true),
        ("Walking", "2 Walk", true),
        ("Running", "3 Run", true),
        ("Dance", "4 Dance", true),
        ("Wave", "5 Wave", false),
        ("Jump", "6 Jump", false)
    ];

    private readonly AnimatedHumanoid _model;
    private readonly Scene _scene = new("Animated humanoid");
    private readonly ScenePerspectiveCamera _camera = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastFrame;
    private double _animationTime;
    private double _poseMilliseconds;
    private float _yaw = 0.25f;
    private float _distance = 7;

    public string AnimationName { get; private set; } = "Idle";
    public bool Paused { get; private set; }
    public bool Wireframe { get; private set; }

    public HumanoidDemo(AnimatedHumanoid model)
    {
        _model = model;
        foreach (var clip in Clips)
        {
            if (!_model.Animations.Any(a => a.Name == clip.Name))
                throw new InvalidDataException($"The robot is missing animation '{clip.Name}'.");
        }

        _scene.AddChild(model.Root);
        _scene.AddChild(new SceneAmbientLight { Intensity = 0.45f });
        _scene.AddChild(new SceneDirectionalLight
        {
            Rotation = Quaternion.FromEulerAngles(-0.6f, 0.6f, 0),
            Intensity = 0.9f
        });
        Play("Idle");
    }

    public void Play(string name)
    {
        var clip = Clips.First(c => c.Name == name);
        _model.SetAnimationFrame(name, 0, clip.Loop);
        AnimationName = name;
        _animationTime = 0;
        Paused = false;
        _lastFrame = _clock.Elapsed.TotalSeconds;
    }

    public Hex1bWidget Build(RootContext context)
    {
        var now = _clock.Elapsed.TotalSeconds;
        var elapsed = now - _lastFrame;
        _lastFrame = now;
        var clip = Clips.First(c => c.Name == AnimationName);
        var duration = _model.Animations.First(a => a.Name == AnimationName).Duration;
        if (!Paused)
        {
            _animationTime += elapsed;
            _animationTime = clip.Loop ? _animationTime % duration : Math.Min(_animationTime, duration);
        }

        var start = Stopwatch.GetTimestamp();
        _model.SetAnimationFrame(AnimationName, (float)_animationTime, clip.Loop);
        _poseMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        _camera.Position = new Vector3(MathF.Sin(_yaw) * _distance, 1.8f, MathF.Cos(_yaw) * _distance);
        _camera.Rotation = Quaternion.FromEulerAngles(0, _yaw, 0);
        var state = Paused ? "paused" : !clip.Loop && _animationTime >= duration ? "finished" : "playing";
        var mode = Wireframe ? "wire" : "solid";

        return context.Grid(g =>
        {
            g.Columns.Add(SizeHint.Fill);
            g.Rows.Add(SizeHint.Fixed(1));
            g.Rows.Add(SizeHint.Fill);
            g.Rows.Add(SizeHint.Fixed(1));
            g.Rows.Add(SizeHint.Fixed(1));
            g.Rows.Add(SizeHint.Fixed(1));
            return
            [
                g.Cell(c => c.Text(
                    $" {AnimationName} | {state} | {mode} | {_model.TriangleCount:N0} tris | pose {_poseMilliseconds:0.0}ms | frame {elapsed * 1000:0.0}ms"))
                    .Row(0),
                g.Cell(c => c.Border(b => [b.Scene(_scene, _camera)]).Title(" Robot Expressive "))
                    .Row(1),
                g.Cell(c => c.HStack(h => Clips.Select(item =>
                    h.Button(item.Label).OnClick(_ => Play(item.Name))).ToArray()))
                    .Row(2),
                g.Cell(c => c.HStack(h =>
                [
                    h.Button(Paused ? "Resume" : "Pause").OnClick(_ => Paused = !Paused),
                    h.Button("Restart").OnClick(_ => Play(AnimationName)),
                    h.Button(Wireframe ? "Solid" : "Wireframe").OnClick(_ => ToggleWireframe()),
                    h.Button("Quit").OnClick(e => e.Context.RequestStop())
                ])).Row(3),
                g.Cell(c => c.Text(" 1-6 animate | Space pause | W wire | Left/Right orbit | Up/Down zoom | Tab/Enter buttons"))
                    .Row(4)
            ];
        })
        .RedrawAfter(33)
        .InputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.D1).Global().Action(_ => Play("Idle"), "Idle");
            bindings.Key(Hex1bKey.D2).Global().Action(_ => Play("Walking"), "Walk");
            bindings.Key(Hex1bKey.D3).Global().Action(_ => Play("Running"), "Run");
            bindings.Key(Hex1bKey.D4).Global().Action(_ => Play("Dance"), "Dance");
            bindings.Key(Hex1bKey.D5).Global().Action(_ => Play("Wave"), "Wave");
            bindings.Key(Hex1bKey.D6).Global().Action(_ => Play("Jump"), "Jump");
            bindings.Key(Hex1bKey.Spacebar).Global().Action(_ => Paused = !Paused, "Pause/resume");
            bindings.Key(Hex1bKey.W).Global().Action(_ => ToggleWireframe(), "Toggle wireframe");
            bindings.Key(Hex1bKey.LeftArrow).Global().Action(_ => _yaw -= 0.15f, "Orbit left");
            bindings.Key(Hex1bKey.RightArrow).Global().Action(_ => _yaw += 0.15f, "Orbit right");
            bindings.Key(Hex1bKey.UpArrow).Global().Action(_ => _distance = Math.Max(4, _distance - 0.5f), "Zoom in");
            bindings.Key(Hex1bKey.DownArrow).Global().Action(_ => _distance = Math.Min(14, _distance + 0.5f), "Zoom out");
        });
    }

    private void ToggleWireframe()
    {
        Wireframe = !Wireframe;
        foreach (var mesh in _model.Meshes)
        {
            mesh.Material = Wireframe
                ? new SceneLineBasicMaterial(mesh.Material.Color)
                : new SceneMeshMaterial(mesh.Material.Color);
        }
    }
}
