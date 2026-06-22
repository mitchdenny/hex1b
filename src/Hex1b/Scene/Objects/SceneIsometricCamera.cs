namespace Hex1b.Scene.Objects;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Scene.Math;

/// <summary>
/// An isometric camera for fixed 3/4 perspective view.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public class SceneIsometricCamera : SceneCamera
{
    public float Size { get; set; } = 10f;

    public SceneIsometricCamera() : base("IsometricCamera")
    {
        // Set a typical isometric viewing angle (45 degrees around Y, -30 degrees around X)
        Rotation = Quaternion.FromEulerAngles(-MathF.PI / 6, MathF.PI / 4, 0);
    }

    public SceneIsometricCamera(string? name) : base(name)
    {
        Rotation = Quaternion.FromEulerAngles(-MathF.PI / 6, MathF.PI / 4, 0);
    }

    public override Matrix4 GetProjectionMatrix(int viewportWidth, int viewportHeight)
    {
        var halfSize = Size / 2;
        var aspect = (float)viewportWidth / viewportHeight;
        return Matrix4.Orthographic(
            -halfSize * aspect,
            halfSize * aspect,
            -halfSize,
            halfSize,
            Near,
            Far
        );
    }
}
