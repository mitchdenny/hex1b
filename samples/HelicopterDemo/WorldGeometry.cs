using Hex1b.Scene.Core;
using Hex1b.Scene.Geometry;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;

namespace HelicopterDemo;

internal static class WorldGeometry
{
    public static SceneMesh Box(Vector3 center, Vector3 size, Vector3 color)
    {
        var x = size.X / 2;
        var y = size.Y / 2;
        var z = size.Z / 2;
        return Mesh(center, color,
        [
            -x, -y, -z, x, -y, -z, x, -y, z, -x, -y, z,
            -x, y, -z, x, y, -z, x, y, z, -x, y, z
        ],
        [
            0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6,
            0, 5, 1, 0, 4, 5, 3, 2, 6, 3, 6, 7,
            1, 5, 6, 1, 6, 2, 0, 3, 7, 0, 7, 4
        ]);
    }

    public static SceneMesh Plane(Vector3 center, float width, float depth, Vector3 color)
    {
        var x = width / 2;
        var z = depth / 2;
        return Mesh(center, color, [-x, 0, -z, x, 0, -z, x, 0, z, -x, 0, z], [0, 2, 1, 0, 3, 2]);
    }

    public static SceneMesh Disc(Vector3 center, float radiusX, float radiusZ, Vector3 color)
    {
        const int segments = 24;
        var positions = new float[(segments + 1) * 3];
        var indices = new uint[segments * 3];
        for (var i = 0; i < segments; i++)
        {
            var angle = i * MathF.Tau / segments;
            positions[(i + 1) * 3] = MathF.Cos(angle) * radiusX;
            positions[(i + 1) * 3 + 2] = MathF.Sin(angle) * radiusZ;
            indices[i * 3] = 0;
            indices[i * 3 + 1] = (uint)((i + 1) % segments + 1);
            indices[i * 3 + 2] = (uint)(i + 1);
        }
        return Mesh(center, color, positions, indices);
    }

    public static void Populate(SceneObject scene)
    {
        scene.AddChild(Plane(new Vector3(0, -0.02f, 0), 200, 200, new Vector3(0.12f, 0.22f, 0.14f)));
        for (var z = -4; z <= 4; z++)
        {
            for (var x = -4; x <= 4; x++)
            {
                var shade = (x + z) % 2 == 0 ? 0.02f : 0;
                scene.AddChild(Plane(new Vector3(x * 12, 0, z * 12), 12, 12,
                    new Vector3(0.14f + shade, 0.25f + shade, 0.16f + shade)));
            }
        }
        var road = new Vector3(0.24f, 0.25f, 0.22f);
        scene.AddChild(Plane(new Vector3(10, 0.02f, 0), 2.6f, 108, road));
        scene.AddChild(Plane(new Vector3(0, 0.03f, 10), 108, 2.6f, road));
        for (var i = -10; i <= 10; i++)
        {
            scene.AddChild(Plane(new Vector3(10, 0.04f, i * 5), 0.12f, 1.8f, new Vector3(0.65f, 0.62f, 0.4f)));
            scene.AddChild(Plane(new Vector3(i * 5, 0.05f, 10), 1.8f, 0.12f, new Vector3(0.65f, 0.62f, 0.4f)));
        }

        scene.AddChild(Disc(new Vector3(0, 0.06f, 0), 4, 4, new Vector3(0.7f, 0.66f, 0.27f)));
        scene.AddChild(Disc(new Vector3(0, 0.07f, 0), 3.6f, 3.6f, new Vector3(0.18f, 0.21f, 0.2f)));
        var marking = new Vector3(0.9f, 0.87f, 0.65f);
        scene.AddChild(Plane(new Vector3(-1, 0.08f, 0), 0.35f, 2.6f, marking));
        scene.AddChild(Plane(new Vector3(1, 0.08f, 0), 0.35f, 2.6f, marking));
        scene.AddChild(Plane(new Vector3(0, 0.08f, 0), 2, 0.35f, marking));

        foreach (var (x, z) in new (float, float)[] { (16, -7), (16, -15), (23, -15), (-14, 16), (-22, 16), (16, 21) })
        {
            scene.AddChild(Box(new Vector3(x, 0.9f, z), new Vector3(4, 1.8f, 5), new Vector3(0.58f, 0.52f, 0.36f)));
            scene.AddChild(Box(new Vector3(x, 1.9f, z), new Vector3(4.4f, 0.2f, 5.4f), new Vector3(0.25f, 0.3f, 0.34f)));
        }

        var random = new Random(42);
        for (var i = 0; i < 60; i++)
        {
            var x = random.Next(-35, 36);
            var z = random.Next(-35, 36);
            if (Math.Abs(x) < 6 && Math.Abs(z) < 6 || Math.Abs(x - 10) < 3 || Math.Abs(z - 10) < 3)
                continue;
            scene.AddChild(Box(new Vector3(x, 0.7f, z), new Vector3(0.35f, 1.4f, 0.35f), new Vector3(0.27f, 0.19f, 0.1f)));
            scene.AddChild(Mesh(new Vector3(x, 1, z), new Vector3(0.12f, 0.38f, 0.2f),
                [-1.1f, 0, -1.1f, 1.1f, 0, -1.1f, 1.1f, 0, 1.1f, -1.1f, 0, 1.1f, 0, 2, 0],
                [0, 4, 1, 1, 4, 2, 2, 4, 3, 3, 4, 0]));
        }
    }

    private static SceneMesh Mesh(Vector3 center, Vector3 color, float[] positions, uint[] indices)
    {
        var geometry = new SceneBufferGeometry();
        geometry.SetAttribute("position", new SceneBufferAttribute("position", positions, 3));
        geometry.SetIndices(indices);
        return new SceneMesh(geometry, new SceneMeshMaterial(color)) { Position = center };
    }
}
