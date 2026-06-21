namespace Hex1b.Scene.Core;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Scene.Math;

/// <summary>
/// Base class for all objects in the 3D scene graph.
/// Supports position, rotation, scale, and parent-child hierarchy.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public class SceneObject
{
    private Vector3 _position = Vector3.Zero;
    private Quaternion _rotation = Quaternion.Identity;
    private Vector3 _scale = Vector3.One;
    private Matrix4? _cachedLocalMatrix;
    private Matrix4? _cachedWorldMatrix;
    private bool _localMatrixDirty = true;
    private bool _worldMatrixDirty = true;

    public string? Name { get; set; }

    public SceneObject? Parent { get; set; }

    private List<SceneObject> _children = new();
    public IReadOnlyList<SceneObject> Children => _children.AsReadOnly();

    public Vector3 Position
    {
        get => _position;
        set
        {
            _position = value;
            InvalidateMatrices();
        }
    }

    public Quaternion Rotation
    {
        get => _rotation;
        set
        {
            _rotation = value.Normalized;
            InvalidateMatrices();
        }
    }

    public Vector3 Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            InvalidateMatrices();
        }
    }

    /// <summary>
    /// Get the local transformation matrix (position, rotation, scale).
    /// </summary>
    public Matrix4 LocalMatrix
    {
        get
        {
            if (_localMatrixDirty)
            {
                _cachedLocalMatrix = ComputeLocalMatrix();
                _localMatrixDirty = false;
            }
            return _cachedLocalMatrix ?? Matrix4.IdentityMatrix;
        }
    }

    /// <summary>
    /// Get the world transformation matrix (accounting for parent transforms).
    /// </summary>
    public Matrix4 WorldMatrix
    {
        get
        {
            if (_worldMatrixDirty)
            {
                if (Parent != null)
                    _cachedWorldMatrix = Parent.WorldMatrix * LocalMatrix;
                else
                    _cachedWorldMatrix = LocalMatrix;
                _worldMatrixDirty = false;
            }
            return _cachedWorldMatrix ?? Matrix4.IdentityMatrix;
        }
    }

    public SceneObject()
    {
    }

    public SceneObject(string? name)
    {
        Name = name;
    }

    public void AddChild(SceneObject child)
    {
        if (child.Parent != null)
            child.Parent.RemoveChild(child);

        _children.Add(child);
        child.Parent = this;
        child.InvalidateMatrices();
    }

    public void RemoveChild(SceneObject child)
    {
        if (_children.Remove(child))
        {
            child.Parent = null;
            child.InvalidateMatrices();
        }
    }

    public void SetPositionWorldSpace(Vector3 worldPosition)
    {
        if (Parent != null)
        {
            // Transform world position to local space
            var parentWorldInverse = Parent.WorldMatrix.Inverse();
            var localPos4 = parentWorldInverse * new Vector4(worldPosition, 1);
            Position = new Vector3(localPos4.X, localPos4.Y, localPos4.Z);
        }
        else
        {
            Position = worldPosition;
        }
    }

    public Vector3 GetPositionWorldSpace()
    {
        var wm = WorldMatrix;
        return new Vector3(wm.Get(0, 3), wm.Get(1, 3), wm.Get(2, 3));
    }

    public Vector3 GetForward()
    {
        // Forward is -Z in local space
        return _rotation.RotateVector(Vector3.UnitZ * -1);
    }

    public Vector3 GetRight()
    {
        // Right is +X in local space
        return _rotation.RotateVector(Vector3.UnitX);
    }

    public Vector3 GetUp()
    {
        // Up is +Y in local space
        return _rotation.RotateVector(Vector3.UnitY);
    }

    protected virtual void InvalidateMatrices()
    {
        _localMatrixDirty = true;
        _worldMatrixDirty = true;
        InvalidateChildMatrices();
    }

    protected void InvalidateChildMatrices()
    {
        foreach (var child in _children)
        {
            child._worldMatrixDirty = true;
            child.InvalidateChildMatrices();
        }
    }

    protected virtual Matrix4 ComputeLocalMatrix()
    {
        // TRS: Translate * Rotate * Scale
        var translation = Matrix4.Translation(_position);
        var rotationMatrix = new Matrix4();
        rotationMatrix.Identity();
        
        // Build rotation matrix from quaternion
        var q = _rotation.Normalized;
        var xx = q.X * q.X;
        var yy = q.Y * q.Y;
        var zz = q.Z * q.Z;
        var xy = q.X * q.Y;
        var zw = q.Z * q.W;
        var zx = q.Z * q.X;
        var yw = q.Y * q.W;
        var yz = q.Y * q.Z;
        var xw = q.X * q.W;

        rotationMatrix.Set(0, 0, 1 - 2 * (yy + zz));
        rotationMatrix.Set(0, 1, 2 * (xy - zw));
        rotationMatrix.Set(0, 2, 2 * (zx + yw));

        rotationMatrix.Set(1, 0, 2 * (xy + zw));
        rotationMatrix.Set(1, 1, 1 - 2 * (zz + xx));
        rotationMatrix.Set(1, 2, 2 * (yz - xw));

        rotationMatrix.Set(2, 0, 2 * (zx - yw));
        rotationMatrix.Set(2, 1, 2 * (yz + xw));
        rotationMatrix.Set(2, 2, 1 - 2 * (yy + xx));

        var scale = Matrix4.Scale(_scale);
        
        // TRS order
        return translation * rotationMatrix * scale;
    }
}
