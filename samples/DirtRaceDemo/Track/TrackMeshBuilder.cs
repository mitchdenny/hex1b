namespace DirtRaceDemo.Track;

using DirtRaceDemo.Scenery;
using Hex1b.Scene.Geometry;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;

/// <summary>
/// Builds the visible geometry for the track: the dirt ground plane and the ribbon that
/// follows the figure-eight centreline (which naturally arches into the crossover bridge as
/// its samples gain height near the centre).
/// </summary>
public static class TrackMeshBuilder
{
    private static readonly Vector3 GroundColor = new(0.46f, 0.34f, 0.22f);
    private static readonly Vector3 TrackColor = new(0.30f, 0.27f, 0.24f);

    public static SceneMesh BuildGround(float size = 70.0f)
    {
        var material = new SceneMeshMaterial(GroundColor) { ShadingMode = SceneMeshShadingMode.Lit };
        return new SceneMesh(GeometryFactory.Plane(size, size), material, "Ground")
        {
            Position = new Vector3(0.0f, -0.02f, 0.0f),
        };
    }

    public static SceneMesh BuildTrackRibbon(FigureEightTrack track)
    {
        var samples = track.Samples;
        var count = samples.Count;
        var positions = new float[count * 2 * 3];
        var uvs = new float[count * 2 * 2];

        for (var i = 0; i < count; i++)
        {
            var sample = samples[i];
            var right = new Vector2(sample.Tangent.Y, -sample.Tangent.X);
            var left = sample.Position - right * track.HalfWidth;
            var rightEdge = sample.Position + right * track.HalfWidth;
            var y = sample.Height + 0.03f;

            var li = i * 6;
            positions[li + 0] = left.X;
            positions[li + 1] = y;
            positions[li + 2] = left.Y;
            positions[li + 3] = rightEdge.X;
            positions[li + 4] = y;
            positions[li + 5] = rightEdge.Y;

            var ui = i * 4;
            var v = i / (float)count;
            uvs[ui + 0] = 0.0f;
            uvs[ui + 1] = v;
            uvs[ui + 2] = 1.0f;
            uvs[ui + 3] = v;
        }

        var indices = new uint[count * 6];
        for (var i = 0; i < count; i++)
        {
            var next = (i + 1) % count;
            var l0 = (uint)(i * 2);
            var r0 = l0 + 1;
            var l1 = (uint)(next * 2);
            var r1 = l1 + 1;

            var idx = i * 6;
            indices[idx + 0] = l0;
            indices[idx + 1] = r0;
            indices[idx + 2] = r1;
            indices[idx + 3] = l0;
            indices[idx + 4] = r1;
            indices[idx + 5] = l1;
        }

        var geometry = new SceneBufferGeometry();
        geometry.SetAttribute("position", new SceneBufferAttribute("position", positions, 3));
        geometry.SetAttribute("uv", new SceneBufferAttribute("uv", uvs, 2));
        geometry.SetIndices(indices);

        var material = new SceneMeshMaterial(TrackColor) { ShadingMode = SceneMeshShadingMode.Lit };
        return new SceneMesh(geometry, material, "Track");
    }
}
