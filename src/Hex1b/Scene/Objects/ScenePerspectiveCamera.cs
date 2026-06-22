namespace Hex1b.Scene.Objects;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Scene.Math;

/// <summary>
/// A perspective camera that mimics real-world camera perspective.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public class ScenePerspectiveCamera : SceneCamera
{
    public float FieldOfView { get; set; } = MathF.PI / 4; // 45 degrees

    public ScenePerspectiveCamera() : base("PerspectiveCamera")
    {
    }

    public ScenePerspectiveCamera(string? name) : base(name)
    {
    }

    public override Matrix4 GetProjectionMatrix(int viewportWidth, int viewportHeight)
    {
        var aspect = (float)viewportWidth / viewportHeight;
        return Matrix4.Perspective(FieldOfView, aspect, Near, Far);
    }
}
