namespace Hex1b.Scene.Rendering;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Scene.Geometry;
using Hex1b.Scene.Math;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Textures;

/// <summary>
/// Base interface for rendering shaders.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public interface ISceneShader
{
    void Render(
        SceneRasterizerContext context,
        SceneBufferGeometry geometry,
        BaseSceneMaterial material,
        Matrix4 modelViewProjectionMatrix,
        Matrix4 normalMatrix,
        SceneLightingState lightingState);
}
