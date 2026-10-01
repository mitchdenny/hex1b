namespace Hex1b.Scene.Rendering;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Scene.Geometry;
using Hex1b.Scene.Math;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Textures;

/// <summary>
/// Directional light snapshot used during a render pass.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public sealed class SceneDirectionalLightState
{
    public Vector3 Direction { get; init; } = new(0, -1, 0);
    public Vector3 Color { get; init; } = Vector3.One;
    public float Intensity { get; init; } = 1.0f;
}
