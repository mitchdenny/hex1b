namespace Hex1b.Scene.Math;

/// <summary>
/// A 4x4 matrix for 3D transformations (position, rotation, scale, projection).
/// Row-major layout: elements[row, col]
/// </summary>
public struct Matrix4 : IEquatable<Matrix4>
{
    // Layout: elements[row, col]
    // [0,0] [0,1] [0,2] [0,3]    [M11 M12 M13 M14]
    // [1,0] [1,1] [1,2] [1,3] =  [M21 M22 M23 M24]
    // [2,0] [2,1] [2,2] [2,3]    [M31 M32 M33 M34]
    // [3,0] [3,1] [3,2] [3,3]    [M41 M42 M43 M44]

    private float[] _elements; // 16 elements in row-major order

    public Matrix4()
    {
        _elements = new float[16];
        Identity();
    }

    public Matrix4(float[] elements) : this()
    {
        if (elements.Length != 16)
            throw new ArgumentException("Matrix4 requires exactly 16 elements", nameof(elements));
        Array.Copy(elements, _elements, 16);
    }

    public Matrix4(
        float m11, float m12, float m13, float m14,
        float m21, float m22, float m23, float m24,
        float m31, float m32, float m33, float m34,
        float m41, float m42, float m43, float m44) : this()
    {
        _elements[0] = m11; _elements[1] = m12; _elements[2] = m13; _elements[3] = m14;
        _elements[4] = m21; _elements[5] = m22; _elements[6] = m23; _elements[7] = m24;
        _elements[8] = m31; _elements[9] = m32; _elements[10] = m33; _elements[11] = m34;
        _elements[12] = m41; _elements[13] = m42; _elements[14] = m43; _elements[15] = m44;
    }

    public float Get(int row, int col) => _elements[row * 4 + col];
    public void Set(int row, int col, float value) => _elements[row * 4 + col] = value;

    public void Identity()
    {
        Array.Clear(_elements, 0, 16);
        _elements[0] = _elements[5] = _elements[10] = _elements[15] = 1;
    }

    public static Matrix4 IdentityMatrix => new();

    public Matrix4 Transposed()
    {
        return new(
            Get(0, 0), Get(1, 0), Get(2, 0), Get(3, 0),
            Get(0, 1), Get(1, 1), Get(2, 1), Get(3, 1),
            Get(0, 2), Get(1, 2), Get(2, 2), Get(3, 2),
            Get(0, 3), Get(1, 3), Get(2, 3), Get(3, 3)
        );
    }

    public float Determinant()
    {
        var m = this;
        return
            m.Get(0, 0) * (m.Get(1, 1) * (m.Get(2, 2) * m.Get(3, 3) - m.Get(2, 3) * m.Get(3, 2)) -
                           m.Get(1, 2) * (m.Get(2, 1) * m.Get(3, 3) - m.Get(2, 3) * m.Get(3, 1)) +
                           m.Get(1, 3) * (m.Get(2, 1) * m.Get(3, 2) - m.Get(2, 2) * m.Get(3, 1))) -
            m.Get(0, 1) * (m.Get(1, 0) * (m.Get(2, 2) * m.Get(3, 3) - m.Get(2, 3) * m.Get(3, 2)) -
                           m.Get(1, 2) * (m.Get(2, 0) * m.Get(3, 3) - m.Get(2, 3) * m.Get(3, 0)) +
                           m.Get(1, 3) * (m.Get(2, 0) * m.Get(3, 2) - m.Get(2, 2) * m.Get(3, 0))) +
            m.Get(0, 2) * (m.Get(1, 0) * (m.Get(2, 1) * m.Get(3, 3) - m.Get(2, 3) * m.Get(3, 1)) -
                           m.Get(1, 1) * (m.Get(2, 0) * m.Get(3, 3) - m.Get(2, 3) * m.Get(3, 0)) +
                           m.Get(1, 3) * (m.Get(2, 0) * m.Get(3, 1) - m.Get(2, 1) * m.Get(3, 0))) -
            m.Get(0, 3) * (m.Get(1, 0) * (m.Get(2, 1) * m.Get(3, 2) - m.Get(2, 2) * m.Get(3, 1)) -
                           m.Get(1, 1) * (m.Get(2, 0) * m.Get(3, 2) - m.Get(2, 2) * m.Get(3, 0)) +
                           m.Get(1, 2) * (m.Get(2, 0) * m.Get(3, 1) - m.Get(2, 1) * m.Get(3, 0)));
    }

    public Matrix4 Inverse()
    {
        var det = Determinant();
        if (MathF.Abs(det) < float.Epsilon)
            throw new InvalidOperationException("Matrix is not invertible (determinant is zero)");

        // TODO: Implement full matrix inversion
        // For now, return identity as placeholder
        return IdentityMatrix;
    }

    public static Matrix4 operator *(Matrix4 a, Matrix4 b)
    {
        var result = new Matrix4();
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                float sum = 0;
                for (int i = 0; i < 4; i++)
                    sum += a.Get(row, i) * b.Get(i, col);
                result.Set(row, col, sum);
            }
        }
        return result;
    }

    public static Vector4 operator *(Matrix4 m, Vector4 v)
    {
        return new(
            m.Get(0, 0) * v.X + m.Get(0, 1) * v.Y + m.Get(0, 2) * v.Z + m.Get(0, 3) * v.W,
            m.Get(1, 0) * v.X + m.Get(1, 1) * v.Y + m.Get(1, 2) * v.Z + m.Get(1, 3) * v.W,
            m.Get(2, 0) * v.X + m.Get(2, 1) * v.Y + m.Get(2, 2) * v.Z + m.Get(2, 3) * v.W,
            m.Get(3, 0) * v.X + m.Get(3, 1) * v.Y + m.Get(3, 2) * v.Z + m.Get(3, 3) * v.W
        );
    }

    public static Matrix4 Translation(Vector3 t)
    {
        var m = IdentityMatrix;
        m.Set(0, 3, t.X);
        m.Set(1, 3, t.Y);
        m.Set(2, 3, t.Z);
        return m;
    }

    public static Matrix4 Scale(Vector3 s)
    {
        var m = IdentityMatrix;
        m.Set(0, 0, s.X);
        m.Set(1, 1, s.Y);
        m.Set(2, 2, s.Z);
        return m;
    }

    public static Matrix4 RotationX(float radians)
    {
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var m = IdentityMatrix;
        m.Set(1, 1, cos);
        m.Set(1, 2, -sin);
        m.Set(2, 1, sin);
        m.Set(2, 2, cos);
        return m;
    }

    public static Matrix4 RotationY(float radians)
    {
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var m = IdentityMatrix;
        m.Set(0, 0, cos);
        m.Set(0, 2, sin);
        m.Set(2, 0, -sin);
        m.Set(2, 2, cos);
        return m;
    }

    public static Matrix4 RotationZ(float radians)
    {
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var m = IdentityMatrix;
        m.Set(0, 0, cos);
        m.Set(0, 1, -sin);
        m.Set(1, 0, sin);
        m.Set(1, 1, cos);
        return m;
    }

    /// <summary>
    /// Create a perspective projection matrix.
    /// </summary>
    public static Matrix4 Perspective(float fovRadians, float aspect, float near, float far)
    {
        var f = 1 / MathF.Tan(fovRadians / 2);
        var m = new Matrix4();
        m.Set(0, 0, f / aspect);
        m.Set(1, 1, f);
        m.Set(2, 2, (far + near) / (near - far));
        m.Set(2, 3, (2 * far * near) / (near - far));
        m.Set(3, 2, -1);
        return m;
    }

    /// <summary>
    /// Create an orthographic projection matrix.
    /// </summary>
    public static Matrix4 Orthographic(float left, float right, float bottom, float top, float near, float far)
    {
        var m = IdentityMatrix;
        m.Set(0, 0, 2 / (right - left));
        m.Set(1, 1, 2 / (top - bottom));
        m.Set(2, 2, -2 / (far - near));
        m.Set(0, 3, -(right + left) / (right - left));
        m.Set(1, 3, -(top + bottom) / (top - bottom));
        m.Set(2, 3, -(far + near) / (far - near));
        return m;
    }

    public override bool Equals(object? obj) => obj is Matrix4 m && Equals(m);

    public bool Equals(Matrix4 other)
    {
        for (int i = 0; i < 16; i++)
            if (MathF.Abs(_elements[i] - other._elements[i]) >= float.Epsilon)
                return false;
        return true;
    }

    public override int GetHashCode() => _elements.GetHashCode();
}
