namespace Hex1b.Scene.Math;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// A 2D vector with x and y components.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public struct Vector2 : IEquatable<Vector2>
{
    public float X { get; set; }
    public float Y { get; set; }

    public Vector2(float x = 0, float y = 0)
    {
        X = x;
        Y = y;
    }

    public float Magnitude => MathF.Sqrt(X * X + Y * Y);

    public float SqrMagnitude => X * X + Y * Y;

    public Vector2 Normalized
    {
        get
        {
            var mag = Magnitude;
            if (MathF.Abs(mag) < float.Epsilon)
                return Zero;
            return new Vector2(X / mag, Y / mag);
        }
    }

    public static Vector2 Zero => new(0, 0);
    public static Vector2 One => new(1, 1);
    public static Vector2 UnitX => new(1, 0);
    public static Vector2 UnitY => new(0, 1);

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 v, float scalar) => new(v.X * scalar, v.Y * scalar);
    public static Vector2 operator *(float scalar, Vector2 v) => new(v.X * scalar, v.Y * scalar);
    public static Vector2 operator /(Vector2 v, float scalar) => new(v.X / scalar, v.Y / scalar);
    public static Vector2 operator -(Vector2 v) => new(-v.X, -v.Y);

    public static float Dot(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;

    public static float Distance(Vector2 a, Vector2 b) => (b - a).Magnitude;

    public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => new(
        a.X + (b.X - a.X) * t,
        a.Y + (b.Y - a.Y) * t
    );

    public override bool Equals(object? obj) => obj is Vector2 v && Equals(v);

    public bool Equals(Vector2 other) =>
        MathF.Abs(X - other.X) < float.Epsilon &&
        MathF.Abs(Y - other.Y) < float.Epsilon;

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => $"({X}, {Y})";
}
