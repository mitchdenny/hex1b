namespace DirtRaceDemo.Scenery;

using Hex1b.Scene.Geometry;

/// <summary>
/// Builds the small set of primitive geometries the racer needs. The scene shader
/// derives flat face normals from vertex positions, so these factories only need to
/// emit positions (and UVs where a texture might be sampled) with consistent
/// counter-clockwise outward winding.
/// </summary>
public static class GeometryFactory
{
    /// <summary>
    /// An axis-aligned box centred on the origin with the given full dimensions.
    /// </summary>
    public static SceneBufferGeometry Box(float sizeX, float sizeY, float sizeZ)
    {
        var hx = sizeX * 0.5f;
        var hy = sizeY * 0.5f;
        var hz = sizeZ * 0.5f;

        var positions = new float[]
        {
            -hx, -hy, -hz,  hx, -hy, -hz,  hx,  hy, -hz, -hx,  hy, -hz,
            -hx, -hy,  hz,  hx, -hy,  hz,  hx,  hy,  hz, -hx,  hy,  hz,
        };

        // Same winding as SceneDemo's unit cube so every face normal points outward.
        var indices = new uint[]
        {
            4, 5, 6, 4, 6, 7, // +Z
            0, 2, 1, 0, 3, 2, // -Z
            0, 4, 7, 0, 7, 3, // -X
            1, 2, 6, 1, 6, 5, // +X
            3, 7, 6, 3, 6, 2, // +Y
            0, 1, 5, 0, 5, 4, // -Y
        };

        var geometry = new SceneBufferGeometry();
        geometry.SetAttribute("position", new SceneBufferAttribute("position", positions, 3));
        geometry.SetIndices(indices);
        return geometry;
    }

    /// <summary>
    /// A flat, upward-facing plane lying in the XZ plane at y = 0, with UV coordinates.
    /// </summary>
    public static SceneBufferGeometry Plane(float width, float depth)
    {
        var hw = width * 0.5f;
        var hd = depth * 0.5f;

        var positions = new float[]
        {
            -hw, 0, -hd,  hw, 0, -hd,  hw, 0,  hd, -hw, 0,  hd,
        };

        var uvs = new float[]
        {
            0, 0,  1, 0,  1, 1,  0, 1,
        };

        var indices = new uint[] { 0, 2, 1, 0, 3, 2 };

        var geometry = new SceneBufferGeometry();
        geometry.SetAttribute("position", new SceneBufferAttribute("position", positions, 3));
        geometry.SetAttribute("uv", new SceneBufferAttribute("uv", uvs, 2));
        geometry.SetIndices(indices);
        return geometry;
    }

    /// <summary>
    /// A launch ramp: a wedge that rises from y = 0 at the -Z end to <paramref name="height"/>
    /// at the +Z end, finishing in a vertical lip. A vehicle drives up the slope from -Z
    /// toward +Z and launches off the lip.
    /// </summary>
    public static SceneBufferGeometry RampWedge(float width, float length, float height)
    {
        var hw = width * 0.5f;
        var hl = length * 0.5f;

        // 0..2 left wall (x = -hw), 3..5 right wall (x = +hw)
        var positions = new float[]
        {
            -hw, 0,     -hl,  // 0 left low front
            -hw, 0,      hl,  // 1 left low back
            -hw, height,  hl,  // 2 left high back
             hw, 0,     -hl,  // 3 right low front
             hw, 0,      hl,  // 4 right low back
             hw, height,  hl,  // 5 right high back
        };

        var indices = new uint[]
        {
            // sloped top (front-low to back-high)
            0, 5, 2, 0, 3, 5,
            // base
            0, 1, 4, 0, 4, 3,
            // vertical lip (back face, +Z)
            1, 2, 5, 1, 5, 4,
            // left triangle wall
            0, 2, 1,
            // right triangle wall
            3, 4, 5,
        };

        var geometry = new SceneBufferGeometry();
        geometry.SetAttribute("position", new SceneBufferAttribute("position", positions, 3));
        geometry.SetIndices(indices);
        return geometry;
    }

    /// <summary>
    /// A short cylinder used for wheels, with its circular faces perpendicular to the X axis.
    /// </summary>
    public static SceneBufferGeometry WheelCylinder(float radius, float thickness, int segments = 14)
    {
        var positions = new List<float>();
        var indices = new List<uint>();
        var half = thickness * 0.5f;

        for (var i = 0; i < segments; i++)
        {
            var angle = i * MathF.PI * 2.0f / segments;
            var y = MathF.Cos(angle) * radius;
            var z = MathF.Sin(angle) * radius;

            positions.Add(-half); positions.Add(y); positions.Add(z); // left rim
            positions.Add(half); positions.Add(y); positions.Add(z);  // right rim
        }

        var leftCenter = (uint)(positions.Count / 3);
        positions.Add(-half); positions.Add(0); positions.Add(0);
        var rightCenter = (uint)(positions.Count / 3);
        positions.Add(half); positions.Add(0); positions.Add(0);

        for (var i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;
            var l0 = (uint)(i * 2);
            var r0 = l0 + 1;
            var l1 = (uint)(next * 2);
            var r1 = l1 + 1;

            // side
            indices.Add(l0); indices.Add(r0); indices.Add(r1);
            indices.Add(l0); indices.Add(r1); indices.Add(l1);

            // left cap
            indices.Add(leftCenter); indices.Add(l1); indices.Add(l0);
            // right cap
            indices.Add(rightCenter); indices.Add(r0); indices.Add(r1);
        }

        var geometry = new SceneBufferGeometry();
        geometry.SetAttribute("position", new SceneBufferAttribute("position", [.. positions], 3));
        geometry.SetIndices([.. indices]);
        return geometry;
    }
}
