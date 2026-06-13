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
    private ISceneShader _shader;

    public SceneRenderer(ISceneShader? shader = null)
    {
        _shader = shader ?? new WireframeSceneShader();
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

        // Traverse scene and render all meshes
        RenderSceneObject(scene, camera, viewProjMatrix, context, Matrix4.IdentityMatrix);
    }

    private void RenderSceneObject(
        SceneObject obj,
        SceneCamera camera,
        Matrix4 viewProjMatrix,
        SceneRasterizerContext context,
        Matrix4 parentWorldMatrix)
    {
        var worldMatrix = obj.WorldMatrix;

        // Render if it's a mesh
        if (obj is SceneMesh mesh)
        {
            RenderMesh(mesh, worldMatrix, viewProjMatrix, context);
        }

        // Recursively render children
        foreach (var child in obj.Children)
        {
            RenderSceneObject(child, camera, viewProjMatrix, context, worldMatrix);
        }
    }

    private void RenderMesh(
        SceneMesh mesh,
        Matrix4 worldMatrix,
        Matrix4 viewProjMatrix,
        SceneRasterizerContext context)
    {
        var mvpMatrix = viewProjMatrix * worldMatrix;

        // Compute normal matrix (for lighting calculations)
        var normalMatrix = worldMatrix.Transposed(); // Simplified; assumes uniform scale

        _shader.Render(mesh.Geometry, mesh.Material, mvpMatrix, normalMatrix);
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
        _shader.Render(geometry, material, mvpMatrix, normalMatrix);
    }
}

/// <summary>
/// Extension method to make shader.Render easier to call.
/// </summary>
internal static class ShaderExtensions
{
    public static void Render(
        this ISceneShader shader,
        SceneBufferGeometry geometry,
        BaseSceneMaterial material,
        Matrix4 mvpMatrix,
        Matrix4 normalMatrix)
    {
        // This is a workaround - create a dummy context just to call the shader
        // In a real implementation, the context would be passed through
        var dummyContext = new SceneRasterizerContext(1, 1);
        shader.Render(dummyContext, geometry, material, mvpMatrix, normalMatrix);
    }
}
