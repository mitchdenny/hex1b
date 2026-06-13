namespace Hex1b.Scene.Rendering;

using Hex1b.Scene.Core;
using Hex1b.Scene.Geometry;
using Hex1b.Scene.Math;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Objects;

/// <summary>
/// Main renderer for 3D scenes. Orchestrates the rendering pipeline:
/// traverses scene graph, culls objects, renders with shaders.
/// </summary>
public class SceneRenderer
{
    private readonly ISceneShader _wireframeShader;
    private readonly ISceneShader _litShader;
    private readonly ISceneShader _normalShader;
    private readonly ISceneShader _depthShader;

    public SceneRenderer(ISceneShader? wireframeShader = null, ISceneShader? solidShader = null)
    {
        _wireframeShader = wireframeShader ?? new WireframeSceneShader();
        _litShader = solidShader ?? new SolidSceneShader();
        _normalShader = new SolidSceneShader.NormalSceneShader();
        _depthShader = new SolidSceneShader.DepthSceneShader();
    }

    /// <summary>
    /// Render a scene from a camera's perspective.
    /// </summary>
    public void Render(
        Scene scene,
        SceneCamera camera,
        SceneRasterizerContext context)
    {
        context.ClearBuffers();

        var viewProjMatrix = camera.GetViewProjectionMatrix(context.ViewportWidth, context.ViewportHeight);
        var lightingState = BuildLightingState(scene);

        // Traverse scene and render all meshes
        RenderSceneObject(scene, camera, viewProjMatrix, context, lightingState);
    }

    private void RenderSceneObject(
        SceneObject obj,
        SceneCamera camera,
        Matrix4 viewProjMatrix,
        SceneRasterizerContext context,
        SceneLightingState lightingState)
    {
        var worldMatrix = obj.WorldMatrix;

        // Render if it's a mesh
        if (obj is SceneMesh mesh)
        {
            RenderMesh(mesh, worldMatrix, viewProjMatrix, context, lightingState);
        }

        // Recursively render children
        foreach (var child in obj.Children)
        {
            RenderSceneObject(child, camera, viewProjMatrix, context, lightingState);
        }
    }

    private void RenderMesh(
        SceneMesh mesh,
        Matrix4 worldMatrix,
        Matrix4 viewProjMatrix,
        SceneRasterizerContext context,
        SceneLightingState lightingState)
    {
        var mvpMatrix = viewProjMatrix * worldMatrix;

        // Compute normal matrix (for lighting calculations)
        var normalMatrix = worldMatrix.Transposed(); // Simplified; assumes uniform scale

        var shader = SelectShader(mesh.Material);
        shader.Render(context, mesh.Geometry, mesh.Material, mvpMatrix, normalMatrix, lightingState);
    }

    /// <summary>
    /// Extended Render method for shader.
    /// </summary>
    public void RenderMeshDirect(
        SceneBufferGeometry geometry,
        BaseSceneMaterial material,
        Matrix4 mvpMatrix,
        Matrix4 normalMatrix,
        SceneRasterizerContext context)
    {
        var shader = SelectShader(material);
        shader.Render(context, geometry, material, mvpMatrix, normalMatrix, new SceneLightingState());
    }

    private ISceneShader SelectShader(BaseSceneMaterial material)
    {
        if (material is not SceneMeshMaterial meshMaterial)
        {
            return _wireframeShader;
        }

        return meshMaterial.ShadingMode switch
        {
            SceneMeshShadingMode.Normal => _normalShader,
            SceneMeshShadingMode.Depth => _depthShader,
            _ => _litShader
        };
    }

    private static SceneLightingState BuildLightingState(Scene scene)
    {
        var ambientColor = Vector3.One;
        var ambientIntensity = 0.20f;
        var directionalLights = new List<SceneDirectionalLightState>();

        CollectLights(scene, directionalLights, ref ambientColor, ref ambientIntensity);

        return new SceneLightingState
        {
            AmbientColor = ambientColor,
            AmbientIntensity = ambientIntensity,
            DirectionalLights = directionalLights
        };
    }

    private static void CollectLights(
        SceneObject obj,
        List<SceneDirectionalLightState> directionalLights,
        ref Vector3 ambientColor,
        ref float ambientIntensity)
    {
        switch (obj)
        {
            case SceneAmbientLight ambientLight:
                ambientColor = ambientLight.Color;
                ambientIntensity = ambientLight.Intensity;
                break;
            case SceneDirectionalLight directionalLight:
                directionalLights.Add(new SceneDirectionalLightState
                {
                    Direction = directionalLight.GetDirection(),
                    Color = directionalLight.Color,
                    Intensity = directionalLight.Intensity
                });
                break;
        }

        foreach (var child in obj.Children)
        {
            CollectLights(child, directionalLights, ref ambientColor, ref ambientIntensity);
        }
    }
}
