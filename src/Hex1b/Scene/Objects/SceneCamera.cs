namespace Hex1b.Scene.Objects;

using Hex1b.Scene.Core;
using Hex1b.Scene.Math;

/// <summary>
/// Base class for all cameras in the 3D scene.
/// Provides view and projection matrix calculations.
/// </summary>
public abstract class SceneCamera : SceneObject
{
    public float Near { get; set; } = 0.1f;
    public float Far { get; set; } = 1000f;

    public SceneCamera() : base()
    {
    }

    public SceneCamera(string? name) : base(name)
    {
    }

    /// <summary>
    /// Get the view matrix (transforms world space to camera space).
    /// </summary>
    public virtual Matrix4 GetViewMatrix()
    {
        var worldMatrix = WorldMatrix;
        var position = new Vector3(worldMatrix.Get(0, 3), worldMatrix.Get(1, 3), worldMatrix.Get(2, 3));
        
        // Forward direction (looking down -Z)
        var forward = GetForward();
        var up = GetUp();
        
        return LookAt(position, position + forward, up);
    }

    /// <summary>
    /// Get the projection matrix (transforms camera space to clip space).
    /// </summary>
    public abstract Matrix4 GetProjectionMatrix(int viewportWidth, int viewportHeight);

    /// <summary>
    /// Create a view matrix looking from position at target with up direction.
    /// </summary>
    private static Matrix4 LookAt(Vector3 position, Vector3 target, Vector3 up)
    {
        var forward = (target - position).Normalized;
        var right = Vector3.Cross(forward, up).Normalized;
        var actualUp = Vector3.Cross(right, forward);

        var result = new Matrix4();
        result.Identity();

        result.Set(0, 0, right.X);
        result.Set(1, 0, right.Y);
        result.Set(2, 0, right.Z);

        result.Set(0, 1, actualUp.X);
        result.Set(1, 1, actualUp.Y);
        result.Set(2, 1, actualUp.Z);

        result.Set(0, 2, -forward.X);
        result.Set(1, 2, -forward.Y);
        result.Set(2, 2, -forward.Z);

        result.Set(0, 3, -Vector3.Dot(right, position));
        result.Set(1, 3, -Vector3.Dot(actualUp, position));
        result.Set(2, 3, Vector3.Dot(forward, position));

        return result;
    }

    /// <summary>
    /// Get the combined view-projection matrix.
    /// </summary>
    public Matrix4 GetViewProjectionMatrix(int viewportWidth, int viewportHeight)
    {
        return GetProjectionMatrix(viewportWidth, viewportHeight) * GetViewMatrix();
    }
}
