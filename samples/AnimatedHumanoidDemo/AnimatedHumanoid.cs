using Hex1b.Scene.Core;
using Hex1b.Scene.Geometry;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Objects;
using SharpGLTF.Runtime;
using SharpGLTF.Schema2;
using Vector3 = System.Numerics.Vector3;
using SceneVector3 = Hex1b.Scene.Math.Vector3;

namespace AnimatedHumanoidDemo;

internal sealed class AnimatedHumanoid
{
    private readonly SceneInstance _instance;
    private readonly Dictionary<IDrawableTemplate, Part[]> _parts = new();
    private readonly Dictionary<string, int> _animationIndices;
    private readonly Vector3 _origin;
    private readonly float _scale;

    public SceneObjectGroup Root { get; } = new("Animated humanoid");
    public IReadOnlyList<Animation> Animations { get; }
    public IReadOnlyList<SceneMesh> Meshes { get; }
    public int VertexCount { get; }
    public int TriangleCount { get; }

    public static string AssetPath => Path.Combine(AppContext.BaseDirectory, "Assets", "RobotExpressive.glb");

    public AnimatedHumanoid(string path)
    {
        var model = ModelRoot.Load(path);
        var scene = model.DefaultScene ?? throw new InvalidDataException("The model has no default scene.");
        _instance = SceneTemplate.Create(scene).CreateInstance();
        _instance.Armature.SetPoseTransforms();
        Animations = model.LogicalAnimations.ToArray();
        _animationIndices = Animations.ToDictionary(a => a.Name, a => a.LogicalIndex, StringComparer.Ordinal);

        var decoded = model.LogicalMeshes.Decode();
        var meshes = new List<SceneMesh>();
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);

        foreach (var drawable in _instance)
        {
            var parts = new List<Part>();
            foreach (var primitive in decoded[drawable.Template.LogicalMeshIndex].Primitives)
            {
                var indices = primitive.TriangleIndices
                    .SelectMany(t => new[] { (uint)t.A, (uint)t.B, (uint)t.C }).ToArray();
                if (indices.Length == 0)
                    throw new NotSupportedException("This spike requires triangle meshes.");

                var positions = new float[primitive.VertexCount * 3];
                var geometry = new SceneBufferGeometry();
                geometry.SetAttribute("position", new SceneBufferAttribute("position", positions, 3));
                geometry.SetIndices(indices);
                var color = primitive.Material?.FindChannel("BaseColor")?.Color ?? System.Numerics.Vector4.One;
                var mesh = new SceneMesh(geometry,
                    new SceneMeshMaterial(new SceneVector3(color.X, color.Y, color.Z)),
                    drawable.Template.NodeName);
                Root.AddChild(mesh);
                meshes.Add(mesh);
                parts.Add(new Part(primitive, positions));
                VertexCount += primitive.VertexCount;
                TriangleCount += indices.Length / 3;

                for (var i = 0; i < primitive.VertexCount; i++)
                {
                    var position = primitive.GetPosition(i, drawable.Transform);
                    min = Vector3.Min(min, position);
                    max = Vector3.Max(max, position);
                }
            }
            _parts.Add(drawable.Template, parts.ToArray());
        }

        var height = max.Y - min.Y;
        if (!float.IsFinite(height) || height <= 0)
            throw new InvalidDataException("The model must contain geometry with a nonzero height.");

        _origin = new Vector3((min.X + max.X) / 2, min.Y, (min.Z + max.Z) / 2);
        _scale = 3 / height;
        Meshes = meshes;
        UpdateGeometry();
    }

    public void SetAnimationFrame(string name, float seconds, bool loop)
    {
        if (!float.IsFinite(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!_animationIndices.TryGetValue(name, out var index))
            throw new ArgumentException($"Unknown animation '{name}'.", nameof(name));

        // Reset channels not keyed by the new clip instead of inheriting the previous pose.
        _instance.Armature.SetPoseTransforms();
        _instance.Armature.SetAnimationFrame(index, seconds, loop);
        UpdateGeometry();
    }

    private void UpdateGeometry()
    {
        foreach (var drawable in _instance)
        {
            foreach (var part in _parts[drawable.Template])
            {
                for (var i = 0; i < part.Primitive.VertexCount; i++)
                {
                    var position = (part.Primitive.GetPosition(i, drawable.Transform) - _origin) * _scale;
                    part.Positions[i * 3] = position.X;
                    part.Positions[i * 3 + 1] = position.Y;
                    part.Positions[i * 3 + 2] = position.Z;
                }
            }
        }
    }

    private sealed record Part(IMeshPrimitiveDecoder<Material> Primitive, float[] Positions);
}
