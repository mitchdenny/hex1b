namespace Hex1b.Scene.Objects;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Scene.Math;

/// <summary>
/// An orthographic camera that projects without perspective distortion.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
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
