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

/// <summary>
/// Wireframe shader that renders geometry edges using ASCII characters.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public class WireframeSceneShader : ISceneShader
{
    public void Render(
        SceneRasterizerContext context,
        SceneBufferGeometry geometry,
        BaseSceneMaterial material,
        Matrix4 modelViewProjectionMatrix,
        Matrix4 normalMatrix,
        SceneLightingState lightingState)
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

/// <summary>
/// Solid shader that fills triangle faces.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public class SolidSceneShader : ISceneShader
{
    public void Render(
        SceneRasterizerContext context,
        SceneBufferGeometry geometry,
        BaseSceneMaterial material,
        Matrix4 modelViewProjectionMatrix,
        Matrix4 normalMatrix,
        SceneLightingState lightingState)
    {
        if (!geometry.HasAttribute("position"))
            return;

        var posAttr = geometry.GetAttribute("position")!;
        var uvAttr = geometry.HasAttribute("uv") ? geometry.GetAttribute("uv") : null;

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

        var textureMaterial = material as SceneTextureMaterial;
        var hasTexture = textureMaterial?.Texture != null && uvAttr != null;

        for (int f = 0; f < geometry.PrimitiveCount; f++)
        {
            var (i0, i1, i2) = geometry.GetFace(f);
            var v0 = projectedVertices[(int)i0];
            var v1 = projectedVertices[(int)i1];
            var v2 = projectedVertices[(int)i2];

            var p0 = new Vector3(
                posAttr.GetComponent((int)i0, 0),
                posAttr.GetComponent((int)i0, 1),
                posAttr.GetComponent((int)i0, 2));
            var p1 = new Vector3(
                posAttr.GetComponent((int)i1, 0),
                posAttr.GetComponent((int)i1, 1),
                posAttr.GetComponent((int)i1, 2));
            var p2 = new Vector3(
                posAttr.GetComponent((int)i2, 0),
                posAttr.GetComponent((int)i2, 1),
                posAttr.GetComponent((int)i2, 2));

            var baseNormal = Vector3.Cross(p1 - p0, p2 - p0).Normalized;
            var worldNormal = TransformDirection(normalMatrix, baseNormal).Normalized;
            var avgDepth = (v0.depth + v1.depth + v2.depth) / 3.0f;

            if (hasTexture)
            {
                // Read UV coordinates
                var u0 = uvAttr!.GetComponent((int)i0, 0);
                var v0_uv = uvAttr.GetComponent((int)i0, 1);
                var u1 = uvAttr.GetComponent((int)i1, 0);
                var v1_uv = uvAttr.GetComponent((int)i1, 1);
                var u2 = uvAttr.GetComponent((int)i2, 0);
                var v2_uv = uvAttr.GetComponent((int)i2, 1);

                context.DrawFilledTriangleWithUV(
                    v0.x, v0.y, v0.depth, u0, v0_uv,
                    v1.x, v1.y, v1.depth, u1, v1_uv,
                    v2.x, v2.y, v2.depth, u2, v2_uv,
                    (u, v) =>
                    {
                        var pixelUint = textureMaterial!.Texture!.SampleBilinear(u, v, textureMaterial.WrapMode);
                        var r = ((pixelUint >> 24) & 0xFF) / 255.0f;
                        var g = ((pixelUint >> 16) & 0xFF) / 255.0f;
                        var b = ((pixelUint >> 8) & 0xFF) / 255.0f;
                        var a = (pixelUint & 0xFF) / 255.0f;
                        var texColor = new Vector3(r, g, b);
                        var litColor = ApplyLighting(texColor, worldNormal, avgDepth, lightingState);
                        return new Vector4(litColor.X, litColor.Y, litColor.Z, a);
                    });
            }
            else
            {
                var litColor = ApplyLighting(material.Color, worldNormal, avgDepth, lightingState);
                var shadedColor = new Vector4(litColor.X, litColor.Y, litColor.Z, 1.0f);

                context.DrawFilledTriangle(
                    v0.x, v0.y, v0.depth,
                    v1.x, v1.y, v1.depth,
                    v2.x, v2.y, v2.depth,
                    shadedColor);
            }
        }
    }

    /// <summary>
    /// Diagnostic shader that visualizes face normal direction as RGB.
    /// </summary>
    public class NormalSceneShader : ISceneShader
    {
        public void Render(
            SceneRasterizerContext context,
            SceneBufferGeometry geometry,
            BaseSceneMaterial material,
            Matrix4 modelViewProjectionMatrix,
            Matrix4 normalMatrix,
            SceneLightingState lightingState)
        {
            if (!geometry.HasAttribute("position"))
                return;

            var posAttr = geometry.GetAttribute("position")!;
            var projectedVertices = SceneShaderUtils.ProjectVertices(context, posAttr, modelViewProjectionMatrix);

            for (int f = 0; f < geometry.PrimitiveCount; f++)
            {
                var (i0, i1, i2) = geometry.GetFace(f);
                var v0 = projectedVertices[(int)i0];
                var v1 = projectedVertices[(int)i1];
                var v2 = projectedVertices[(int)i2];

                var p0 = SceneShaderUtils.ReadVertex(posAttr, i0);
                var p1 = SceneShaderUtils.ReadVertex(posAttr, i1);
                var p2 = SceneShaderUtils.ReadVertex(posAttr, i2);

                var baseNormal = Vector3.Cross(p1 - p0, p2 - p0).Normalized;
                var worldNormal = SceneShaderUtils.TransformDirection(normalMatrix, baseNormal).Normalized;
                var normalColor = new Vector3(
                    SceneShaderUtils.Clamp01((worldNormal.X * 0.5f) + 0.5f),
                    SceneShaderUtils.Clamp01((worldNormal.Y * 0.5f) + 0.5f),
                    SceneShaderUtils.Clamp01((worldNormal.Z * 0.5f) + 0.5f));

                context.DrawFilledTriangle(
                    v0.x, v0.y, v0.depth,
                    v1.x, v1.y, v1.depth,
                    v2.x, v2.y, v2.depth,
                    new Vector4(normalColor.X, normalColor.Y, normalColor.Z, 1.0f));
            }
        }
    }

    /// <summary>
    /// Diagnostic shader that visualizes camera depth as grayscale.
    /// </summary>
    public class DepthSceneShader : ISceneShader
    {
        public void Render(
            SceneRasterizerContext context,
            SceneBufferGeometry geometry,
            BaseSceneMaterial material,
            Matrix4 modelViewProjectionMatrix,
            Matrix4 normalMatrix,
            SceneLightingState lightingState)
        {
            if (!geometry.HasAttribute("position"))
                return;

            var posAttr = geometry.GetAttribute("position")!;
            var projectedVertices = SceneShaderUtils.ProjectVertices(context, posAttr, modelViewProjectionMatrix);

            for (int f = 0; f < geometry.PrimitiveCount; f++)
            {
                var (i0, i1, i2) = geometry.GetFace(f);
                var v0 = projectedVertices[(int)i0];
                var v1 = projectedVertices[(int)i1];
                var v2 = projectedVertices[(int)i2];

                var averageDepth = (v0.depth + v1.depth + v2.depth) / 3.0f;
                var normalizedDepth = SceneShaderUtils.Clamp01((averageDepth + 1.0f) * 0.5f);
                var intensity = 1.0f - normalizedDepth;

                context.DrawFilledTriangle(
                    v0.x, v0.y, v0.depth,
                    v1.x, v1.y, v1.depth,
                    v2.x, v2.y, v2.depth,
                    new Vector4(intensity, intensity, intensity, 1.0f));
            }
        }
    }

    internal static class SceneShaderUtils
    {
        public static (int x, int y, float depth)[] ProjectVertices(
            SceneRasterizerContext context,
            SceneBufferAttribute positionAttribute,
            Matrix4 modelViewProjectionMatrix)
        {
            var projectedVertices = new (int x, int y, float depth)[positionAttribute.Count];
            for (int i = 0; i < positionAttribute.Count; i++)
            {
                var worldPos = new Vector3(
                    positionAttribute.GetComponent(i, 0),
                    positionAttribute.GetComponent(i, 1),
                    positionAttribute.GetComponent(i, 2));

                var (screenX, screenY, depth) = context.WorldToScreenSpace(worldPos, modelViewProjectionMatrix);
                projectedVertices[i] = (screenX, screenY, depth);
            }

            return projectedVertices;
        }

        public static Vector3 ReadVertex(SceneBufferAttribute positionAttribute, uint index)
        {
            return new Vector3(
                positionAttribute.GetComponent((int)index, 0),
                positionAttribute.GetComponent((int)index, 1),
                positionAttribute.GetComponent((int)index, 2));
        }

        public static Vector3 TransformDirection(Matrix4 matrix, Vector3 direction)
        {
            var transformed = matrix * new Vector4(direction.X, direction.Y, direction.Z, 0.0f);
            return new Vector3(transformed.X, transformed.Y, transformed.Z);
        }

        public static float Clamp01(float value)
        {
            if (value < 0.0f) return 0.0f;
            if (value > 1.0f) return 1.0f;
            return value;
        }
    }

    private static Vector3 TransformDirection(Matrix4 m, Vector3 v)
    {
        var transformed = m * new Vector4(v.X, v.Y, v.Z, 0.0f);
        return new Vector3(transformed.X, transformed.Y, transformed.Z);
    }

    private static Vector3 ApplyLighting(Vector3 albedo, Vector3 normal, float averageDepth, SceneLightingState lightingState)
    {
        var ambient = Multiply(lightingState.AmbientColor, albedo) * lightingState.AmbientIntensity;
        var directional = Vector3.Zero;

        foreach (var light in lightingState.DirectionalLights)
        {
            var lambert = MathF.Max(0.0f, Vector3.Dot(normal, light.Direction * -1.0f));
            directional += Multiply(albedo, light.Color) * (light.Intensity * lambert);
        }

        // Add depth cueing so distant geometry appears dimmer.
        var normalizedDepth = Clamp01((averageDepth + 1.0f) * 0.5f);
        var depthFalloff = 1.0f - (normalizedDepth * 0.45f);

        var lit = (ambient + directional) * depthFalloff;
        return new Vector3(Clamp01(lit.X), Clamp01(lit.Y), Clamp01(lit.Z));
    }

    private static Vector3 Multiply(Vector3 a, Vector3 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

    private static float Clamp01(float value)
    {
        if (value < 0.0f) return 0.0f;
        if (value > 1.0f) return 1.0f;
        return value;
    }
}
