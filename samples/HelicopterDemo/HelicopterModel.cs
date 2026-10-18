using Hex1b.Scene.Core;
using Hex1b.Scene.Geometry;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;

namespace HelicopterDemo;

internal sealed class HelicopterModel
{
    public const float GroundClearance = 0.12f;
    public SceneObjectGroup Root { get; } = new("Gunship");
    public SceneObjectGroup MainRotor { get; } = new("Main rotor");
    public SceneObjectGroup TailRotor { get; } = new("Tail rotor");
    public int ImportedTriangleCount { get; }
    public static string AssetPath => Path.Combine(AppContext.BaseDirectory, "Assets", "Helicopter.mesh");

    public HelicopterModel(string path)
    {
        Root.AddChild(MainRotor);
        Root.AddChild(TailRotor);
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (reader.ReadUInt32() != 0x314D3148) // H1M1, little endian.
            throw new InvalidDataException("Not a converted helicopter mesh.");
        var count = reader.ReadUInt32();
        if (count is 0 or > 32)
            throw new InvalidDataException("Invalid helicopter part count.");
        for (var part = 0; part < count; part++)
        {
            var kind = reader.ReadUInt32();
            var color = ReadVector(reader);
            var pivot = ReadVector(reader);
            var vertexCount = reader.ReadInt32();
            if (vertexCount is <= 0 or > 100_000 || vertexCount % 3 != 0)
                throw new InvalidDataException("Invalid helicopter triangle count.");
            var positions = new float[vertexCount * 3];
            for (var i = 0; i < positions.Length; i++)
            {
                positions[i] = reader.ReadSingle();
                if (!float.IsFinite(positions[i]))
                    throw new InvalidDataException("Non-finite helicopter position.");
            }
            var geometry = new SceneBufferGeometry();
            geometry.SetAttribute("position", new SceneBufferAttribute("position", positions, 3));
            var mesh = new SceneMesh(geometry, new SceneMeshMaterial(color));
            var parent = kind switch
            {
                0 => Root,
                1 => MainRotor,
                2 => TailRotor,
                _ => throw new InvalidDataException("Invalid helicopter part kind.")
            };
            if (kind == 0)
                mesh.Position = pivot;
            else
                parent.Position = pivot;
            parent.AddChild(mesh);
            ImportedTriangleCount += vertexCount / 3;
        }
        if (stream.Position != stream.Length || MainRotor.Children.Count == 0 || TailRotor.Children.Count == 0)
            throw new InvalidDataException("Incomplete helicopter asset.");

        var wingColor = new Vector3(0.32f, 0.36f, 0.17f);
        Root.AddChild(WorldGeometry.Box(new Vector3(0, 0.85f, -0.45f), new Vector3(2.8f, 0.12f, 0.5f), wingColor));
        foreach (var side in new[] { -1, 1 })
        {
            Root.AddChild(WorldGeometry.Box(new Vector3(side * 1.15f, 0.7f, -0.45f),
                new Vector3(0.35f, 0.35f, 1.25f), new Vector3(0.22f, 0.25f, 0.12f)));
            Root.AddChild(WorldGeometry.Box(new Vector3(side * 1.15f, 0.7f, -1.09f),
                new Vector3(0.28f, 0.28f, 0.03f), new Vector3(0.07f, 0.09f, 0.07f)));
        }
    }

    public void Update(FlightState flight)
    {
        Root.Position = new Vector3(flight.Position.X, flight.Altitude + GroundClearance, flight.Position.Y);
        var tilt = flight.IsGrounded ? System.Numerics.Vector2.Zero : flight.Tilt;
        Root.Rotation = Quaternion.FromEulerAngles(tilt.Y * FlightState.MaximumTiltAngle, 0, -tilt.X * FlightState.MaximumTiltAngle)
            * Quaternion.FromAxisAngle(Vector3.UnitY, flight.Heading);
        MainRotor.Rotation = Quaternion.FromAxisAngle(Vector3.UnitY, flight.RotorAngle);
        TailRotor.Rotation = Quaternion.FromAxisAngle(Vector3.UnitX, flight.RotorAngle * 1.7f);
    }

    private static Vector3 ReadVector(BinaryReader reader)
    {
        var value = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            throw new InvalidDataException("Non-finite helicopter vector.");
        return value;
    }
}
