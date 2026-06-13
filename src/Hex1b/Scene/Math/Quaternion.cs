namespace Hex1b.Scene.Math;

/// <summary>
/// A quaternion for 3D rotations (x, y, z, w format).
/// Avoids gimbal lock and enables smooth interpolation (slerp).
/// </summary>
public struct Quaternion : IEquatable<Quaternion>
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float W { get; set; }

    public Quaternion(float x = 0, float y = 0, float z = 0, float w = 1)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public float Magnitude => MathF.Sqrt(X * X + Y * Y + Z * Z + W * W);

    public float SqrMagnitude => X * X + Y * Y + Z * Z + W * W;

    public Quaternion Normalized
    {
        get
        {
            var mag = Magnitude;
            if (MathF.Abs(mag) < float.Epsilon)
                return Identity;
            return new Quaternion(X / mag, Y / mag, Z / mag, W / mag);
        }
    }

    public Quaternion Conjugate => new(-X, -Y, -Z, W);

    public Quaternion Inverse => Conjugate / SqrMagnitude;

    public static Quaternion Identity => new(0, 0, 0, 1);

    /// <summary>
    /// Create a quaternion from an axis and rotation angle (in radians).
    /// </summary>
    public static Quaternion FromAxisAngle(Vector3 axis, float angleRadians)
    {
        var normalizedAxis = axis.Normalized;
        var halfAngle = angleRadians / 2;
        var sin = MathF.Sin(halfAngle);
        return new(
            normalizedAxis.X * sin,
            normalizedAxis.Y * sin,
            normalizedAxis.Z * sin,
            MathF.Cos(halfAngle)
        );
    }

    /// <summary>
    /// Create a quaternion from Euler angles (in radians) using XYZ order.
    /// </summary>
    public static Quaternion FromEulerAngles(float x, float y, float z)
    {
        // Using ZYX order for intrinsic rotations (equivalent to XYZ extrinsic)
        var cx = MathF.Cos(x / 2);
        var sx = MathF.Sin(x / 2);
        var cy = MathF.Cos(y / 2);
        var sy = MathF.Sin(y / 2);
        var cz = MathF.Cos(z / 2);
        var sz = MathF.Sin(z / 2);

        return new(
            sx * cy * cz - cx * sy * sz,
            cx * sy * cz + sx * cy * sz,
            cx * cy * sz - sx * sy * cz,
            cx * cy * cz + sx * sy * sz
        );
    }

    /// <summary>
    /// Create a quaternion from Euler angles as a Vector3 (in radians).
    /// </summary>
    public static Quaternion FromEulerAngles(Vector3 angles) =>
        FromEulerAngles(angles.X, angles.Y, angles.Z);

    /// <summary>
    /// Convert quaternion to Euler angles (in radians) as a Vector3.
    /// </summary>
    public Vector3 ToEulerAngles()
    {
        var q = Normalized;

        // Roll (X-axis rotation)
        var sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
        var cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
        var roll = MathF.Atan2(sinr_cosp, cosr_cosp);

        // Pitch (Y-axis rotation)
        var sinp = 2 * (q.W * q.Y - q.Z * q.X);
        var pitch = MathF.Abs(sinp) >= 1
            ? MathF.CopySign(MathF.PI / 2, sinp)
            : MathF.Asin(sinp);

        // Yaw (Z-axis rotation)
        var siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
        var cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
        var yaw = MathF.Atan2(siny_cosp, cosy_cosp);

        return new(roll, pitch, yaw);
    }

    /// <summary>
    /// Rotate a vector by this quaternion.
    /// </summary>
    public Vector3 RotateVector(Vector3 v)
    {
        var q = Normalized;
        var vq = new Quaternion(v.X, v.Y, v.Z, 0);
        var result = q * vq * q.Conjugate;
        return new(result.X, result.Y, result.Z);
    }

    public static Quaternion operator *(Quaternion a, Quaternion b) => new(
        a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
        a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
        a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
        a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z
    );

    public static Quaternion operator +(Quaternion a, Quaternion b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);

    public static Quaternion operator -(Quaternion a, Quaternion b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);

    public static Quaternion operator *(Quaternion q, float scalar) =>
        new(q.X * scalar, q.Y * scalar, q.Z * scalar, q.W * scalar);

    public static Quaternion operator /(Quaternion q, float scalar) =>
        new(q.X / scalar, q.Y / scalar, q.Z / scalar, q.W / scalar);

    /// <summary>
    /// Spherical linear interpolation between two quaternions.
    /// </summary>
    public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
    {
        var qa = a.Normalized;
        var qb = b.Normalized;

        float dot = qa.X * qb.X + qa.Y * qb.Y + qa.Z * qb.Z + qa.W * qb.W;

        if (dot < 0)
        {
            qb = new(-qb.X, -qb.Y, -qb.Z, -qb.W);
            dot = -dot;
        }

        dot = MathF.Max(-1, MathF.Min(1, dot));

        var theta0 = MathF.Acos(dot);
        var theta = theta0 * t;

        var qc = (qb - qa * dot).Normalized;

        return qa * MathF.Cos(theta) + qc * MathF.Sin(theta);
    }

    public override bool Equals(object? obj) => obj is Quaternion q && Equals(q);

    public bool Equals(Quaternion other) =>
        MathF.Abs(X - other.X) < float.Epsilon &&
        MathF.Abs(Y - other.Y) < float.Epsilon &&
        MathF.Abs(Z - other.Z) < float.Epsilon &&
        MathF.Abs(W - other.W) < float.Epsilon;

    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);

    public override string ToString() => $"({X}, {Y}, {Z}, {W})";
}
