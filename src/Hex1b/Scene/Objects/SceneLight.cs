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

/// <summary>
/// Ambient light that illuminates all surfaces uniformly.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public class SceneAmbientLight : SceneLight
{
    public SceneAmbientLight() : base("AmbientLight")
    {
    }

    public SceneAmbientLight(string? name) : base(name)
    {
    }
}

/// <summary>
/// Directional light that casts parallel rays (like sunlight).
/// Direction is determined by the light's forward vector.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public class SceneDirectionalLight : SceneLight
{
    public SceneDirectionalLight() : base("DirectionalLight")
    {
    }

    public SceneDirectionalLight(string? name) : base(name)
    {
    }

    /// <summary>
    /// Get the light direction (normalized forward vector).
    /// </summary>
    public Vector3 GetDirection()
    {
        return GetForward().Normalized;
    }
}
