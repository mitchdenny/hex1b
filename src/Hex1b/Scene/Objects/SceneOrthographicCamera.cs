namespace Hex1b.Scene.Objects;

using Hex1b.Scene.Math;

/// <summary>
/// An orthographic camera that projects without perspective distortion.
/// </summary>
public class SceneOrthographicCamera : SceneCamera
{
    public float Width { get; set; } = 10f;
    public float Height { get; set; } = 10f;

    public SceneOrthographicCamera() : base("OrthographicCamera")
    {
    }

    public SceneOrthographicCamera(string? name) : base(name)
    {
    }

    public override Matrix4 GetProjectionMatrix(int viewportWidth, int viewportHeight)
    {
        var halfWidth = Width / 2;
        var halfHeight = Height / 2;
        return Matrix4.Orthographic(-halfWidth, halfWidth, -halfHeight, halfHeight, Near, Far);
    }
}
