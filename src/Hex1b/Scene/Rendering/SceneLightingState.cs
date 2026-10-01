namespace Hex1b.Scene.Rendering;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Scene.Geometry;
using Hex1b.Scene.Math;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Textures;

/// <summary>
/// Lightweight lighting data passed from renderer to shaders.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public sealed class SceneLightingState
{
    public Vector3 AmbientColor { get; init; } = Vector3.One;
    public float AmbientIntensity { get; init; } = 0.25f;
    public IReadOnlyList<SceneDirectionalLightState> DirectionalLights { get; init; } = [];
}
