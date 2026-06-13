namespace Hex1b.Scene.Math;

/// <summary>
/// A 3D vector with x, y, z components for 3D coordinate space.
/// </summary>
public struct Vector3 : IEquatable<Vector3>
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public Vector3(float x = 0, float y = 0, float z = 0)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public float Magnitude => MathF.Sqrt(X * X + Y * Y + Z * Z);

    public float SqrMagnitude => X * X + Y * Y + Z * Z;

    public Vector3 Normalized
    {
        get
        {
            var mag = Magnitude;
            if (MathF.Abs(mag) < float.Epsilon)
                return Zero;
            return new Vector3(X / mag, Y / mag, Z / mag);
        }
    }

    public static Vector3 Zero => new(0, 0, 0);
    public static Vector3 One => new(1, 1, 1);
    public static Vector3 UnitX => new(1, 0, 0);
    public static Vector3 UnitY => new(0, 1, 0);
    public static Vector3 UnitZ => new(0, 0, 1);

    public static Vector3 operator +(Vector3 a, Vector3 b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3 operator -(Vector3 a, Vector3 b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3 operator *(Vector3 v, float scalar) =>
        new(v.X * scalar, v.Y * scalar, v.Z * scalar);

    public static Vector3 operator *(float scalar, Vector3 v) =>
        new(v.X * scalar, v.Y * scalar, v.Z * scalar);

    public static Vector3 operator /(Vector3 v, float scalar) =>
        new(v.X / scalar, v.Y / scalar, v.Z / scalar);

    public static Vector3 operator -(Vector3 v) =>
        new(-v.X, -v.Y, -v.Z);

    /// <summary>
    /// Dot product of two vectors.
    /// </summary>
    public static float Dot(Vector3 a, Vector3 b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    /// <summary>
    /// Cross product of two vectors (right-hand rule).
    /// </summary>
    public static Vector3 Cross(Vector3 a, Vector3 b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X
    );

    /// <summary>
    /// Euclidean distance between two points.
    /// </summary>
    public static float Distance(Vector3 a, Vector3 b) => (b - a).Magnitude;

    /// <summary>
    /// Linear interpolation between two vectors.
    /// </summary>
    public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => new(
        a.X + (b.X - a.X) * t,
        a.Y + (b.Y - a.Y) * t,
        a.Z + (b.Z - a.Z) * t
    );

    public override bool Equals(object? obj) => obj is Vector3 v && Equals(v);

    public bool Equals(Vector3 other) =>
        MathF.Abs(X - other.X) < float.Epsilon &&
        MathF.Abs(Y - other.Y) < float.Epsilon &&
        MathF.Abs(Z - other.Z) < float.Epsilon;

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => $"({X}, {Y}, {Z})";
}
