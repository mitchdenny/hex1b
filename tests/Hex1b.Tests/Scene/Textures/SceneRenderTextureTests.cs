namespace Hex1b.Tests.Scene.Textures;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hex1b.Scene.Core;
using Hex1b.Scene.Geometry;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;
using Hex1b.Scene.Textures;

[TestClass]
public class SceneRenderTextureTests
{
    private static Scene CreateSceneWithQuad()
    {
        var scene = new Scene("Render Texture Scene");

        // A large quad straddling the origin in the z = 0 plane, facing +Z.
        var positions = new float[]
        {
            -3, -3, 0,
             3, -3, 0,
             3,  3, 0,
            -3,  3, 0
        };
        var indices = new uint[] { 0, 1, 2, 0, 2, 3 };

        var geometry = new SceneBufferGeometry();
        geometry.SetAttribute("position", new SceneBufferAttribute("position", positions, 3));
        geometry.SetIndices(indices);

        var material = new SceneMeshMaterial(new Vector3(0.2f, 0.7f, 0.9f))
        {
            ShadingMode = SceneMeshShadingMode.Normal
        };

        scene.AddChild(new SceneMesh(geometry, material, "Quad"));
        return scene;
    }

    private static ScenePerspectiveCamera CreateFrontCamera()
    {
        return new ScenePerspectiveCamera("Camera")
        {
            Position = new Vector3(0.0f, 0.0f, 5.0f)
        };
    }

    [TestMethod]
    public void Constructor_AllocatesTextureOfRequestedSize()
    {
        var renderTexture = new SceneRenderTexture(CreateSceneWithQuad(), CreateFrontCamera(), 48, 32);

        Assert.AreEqual(48, renderTexture.Width);
        Assert.AreEqual(32, renderTexture.Height);
        Assert.IsNotNull(renderTexture.Texture);
        Assert.AreEqual(48, renderTexture.Texture.Width);
        Assert.AreEqual(32, renderTexture.Texture.Height);
    }

    [TestMethod]
    public void Constructor_NullScene_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new SceneRenderTexture(null!, CreateFrontCamera()));
    }

    [TestMethod]
    public void Constructor_NullCamera_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new SceneRenderTexture(CreateSceneWithQuad(), null!));
    }

    [TestMethod]
    public void Update_ReturnsTheOwnedTexture()
    {
        var renderTexture = new SceneRenderTexture(CreateSceneWithQuad(), CreateFrontCamera(), 32, 32);

        var result = renderTexture.Update();

        Assert.AreSame(renderTexture.Texture, result);
    }

    [TestMethod]
    public void Update_RendersGeometryIntoTexture()
    {
        var renderTexture = new SceneRenderTexture(CreateSceneWithQuad(), CreateFrontCamera(), 32, 32);

        var texture = renderTexture.Update();

        // The quad covers the center of the view, so the center pixel must be painted.
        var center = texture.GetPixel(16, 16);
        Assert.AreNotEqual(0u, center, "Expected the rendered quad to paint the center pixel.");
    }

    [TestMethod]
    public void Resize_ChangesDimensionsAndReallocatesTexture()
    {
        var renderTexture = new SceneRenderTexture(CreateSceneWithQuad(), CreateFrontCamera(), 32, 32);
        var original = renderTexture.Texture;

        renderTexture.Resize(64, 24);

        Assert.AreEqual(64, renderTexture.Width);
        Assert.AreEqual(24, renderTexture.Height);
        Assert.AreNotSame(original, renderTexture.Texture);
        Assert.AreEqual(64, renderTexture.Texture.Width);
        Assert.AreEqual(24, renderTexture.Texture.Height);
    }

    [TestMethod]
    public void Resize_SameDimensions_KeepsTextureInstance()
    {
        var renderTexture = new SceneRenderTexture(CreateSceneWithQuad(), CreateFrontCamera(), 32, 32);
        var original = renderTexture.Texture;

        renderTexture.Resize(32, 32);

        Assert.AreSame(original, renderTexture.Texture);
    }

    [TestMethod]
    public void Update_AfterResize_RendersAtNewResolution()
    {
        var renderTexture = new SceneRenderTexture(CreateSceneWithQuad(), CreateFrontCamera(), 16, 16);
        renderTexture.Resize(40, 40);

        var texture = renderTexture.Update();

        Assert.AreEqual(40, texture.Width);
        Assert.AreEqual(40, texture.Height);
        Assert.AreNotEqual(0u, texture.GetPixel(20, 20));
    }
}
