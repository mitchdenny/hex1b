namespace Hex1b.Scene.Rendering;

using Hex1b.Scene.Geometry;
using Hex1b.Scene.Math;
using Hex1b.Scene.Materials;

/// <summary>
/// Base interface for rendering shaders.
/// </summary>
public interface ISceneShader
{
    void Render(
        SceneRasterizerContext context,
        SceneBufferGeometry geometry,
        BaseSceneMaterial material,
        Matrix4 modelViewProjectionMatrix,
        Matrix4 normalMatrix);
}

/// <summary>
/// Wireframe shader that renders geometry edges using ASCII characters.
/// </summary>
public class WireframeSceneShader : ISceneShader
{
    public void Render(
        SceneRasterizerContext context,
        SceneBufferGeometry geometry,
        BaseSceneMaterial material,
        Matrix4 modelViewProjectionMatrix,
        Matrix4 normalMatrix)
    {
        if (!geometry.HasAttribute("position"))
            return;

        var posAttr = geometry.GetAttribute("position")!;

        // Project all vertices to screen space
        var projectedVertices = new (int x, int y, float depth)[posAttr.Count];
        for (int i = 0; i < posAttr.Count; i++)
        {
            var px = posAttr.GetComponent(i, 0);
            var py = posAttr.GetComponent(i, 1);
            var pz = posAttr.GetComponent(i, 2);
            var worldPos = new Vector3(px, py, pz);

            var (screenX, screenY, depth) = context.WorldToScreenSpace(worldPos, modelViewProjectionMatrix);
            projectedVertices[i] = (screenX, screenY, depth);
        }

        // Draw lines between connected vertices
        var color = new Vector4(material.Color.X, material.Color.Y, material.Color.Z, 1.0f);

        for (int f = 0; f < geometry.PrimitiveCount; f++)
        {
            var (i0, i1, i2) = geometry.GetFace(f);
            var v0 = projectedVertices[(int)i0];
            var v1 = projectedVertices[(int)i1];
            var v2 = projectedVertices[(int)i2];

            // Draw three edges of the triangle
            if (context.IsInViewport(v0.x, v0.y) && context.IsInViewport(v1.x, v1.y))
                context.DrawLine(v0.x, v0.y, v1.x, v1.y, v0.depth, v1.depth, color);
            
            if (context.IsInViewport(v1.x, v1.y) && context.IsInViewport(v2.x, v2.y))
                context.DrawLine(v1.x, v1.y, v2.x, v2.y, v1.depth, v2.depth, color);
            
            if (context.IsInViewport(v2.x, v2.y) && context.IsInViewport(v0.x, v0.y))
                context.DrawLine(v2.x, v2.y, v0.x, v0.y, v2.depth, v0.depth, color);
        }
    }
}
