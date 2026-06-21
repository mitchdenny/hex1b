namespace Hex1b.Scene.Math;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// A 4D vector for homogeneous coordinates (used in matrix transformations).
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public struct Vector4 : IEquatable<Vector4>
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float W { get; set; }

    public Vector4(float x = 0, float y = 0, float z = 0, float w = 1)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public Vector4(Vector3 xyz, float w = 1)
    {
        X = xyz.X;
        Y = xyz.Y;
        Z = xyz.Z;
        W = w;
    }

    public float Magnitude => MathF.Sqrt(X * X + Y * Y + Z * Z + W * W);

    public float SqrMagnitude => X * X + Y * Y + Z * Z + W * W;

    public Vector4 Normalized
    {
        get
        {
            var mag = Magnitude;
            if (MathF.Abs(mag) < float.Epsilon)
                return Zero;
            return new Vector4(X / mag, Y / mag, Z / mag, W / mag);
        }
    }

    public static Vector4 Zero => new(0, 0, 0, 0);
    public static Vector4 One => new(1, 1, 1, 1);
    public static Vector4 UnitX => new(1, 0, 0, 0);
    public static Vector4 UnitY => new(0, 1, 0, 0);
    public static Vector4 UnitZ => new(0, 0, 1, 0);
    public static Vector4 UnitW => new(0, 0, 0, 1);

    public static Vector4 operator +(Vector4 a, Vector4 b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);

    public static Vector4 operator -(Vector4 a, Vector4 b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);

    public static Vector4 operator *(Vector4 v, float scalar) =>
        new(v.X * scalar, v.Y * scalar, v.Z * scalar, v.W * scalar);

    public static Vector4 operator *(float scalar, Vector4 v) =>
        new(v.X * scalar, v.Y * scalar, v.Z * scalar, v.W * scalar);

    public static Vector4 operator /(Vector4 v, float scalar) =>
        new(v.X / scalar, v.Y / scalar, v.Z / scalar, v.W / scalar);

    public static Vector4 operator -(Vector4 v) =>
        new(-v.X, -v.Y, -v.Z, -v.W);

    public static float Dot(Vector4 a, Vector4 b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

    public static Vector4 Lerp(Vector4 a, Vector4 b, float t) => new(
        a.X + (b.X - a.X) * t,
        a.Y + (b.Y - a.Y) * t,
        a.Z + (b.Z - a.Z) * t,
        a.W + (b.W - a.W) * t
    );

    public override bool Equals(object? obj) => obj is Vector4 v && Equals(v);

    public bool Equals(Vector4 other) =>
        MathF.Abs(X - other.X) < float.Epsilon &&
        MathF.Abs(Y - other.Y) < float.Epsilon &&
        MathF.Abs(Z - other.Z) < float.Epsilon &&
        MathF.Abs(W - other.W) < float.Epsilon;

    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);

    public override string ToString() => $"({X}, {Y}, {Z}, {W})";
}
