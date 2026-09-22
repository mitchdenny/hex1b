namespace Hex1b.Scene.Objects;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Scene.Core;
using Hex1b.Scene.Math;

/// <summary>
/// Base class for all lights in the scene.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public abstract class SceneLight : SceneObject
{
    public Vector3 Color { get; set; } = Vector3.One;
    public float Intensity { get; set; } = 1.0f;

    public SceneLight() : base()
    {
    }

    public SceneLight(string? name) : base(name)
    {
    }
}
