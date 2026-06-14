namespace Hex1b.Tests.Scene;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hex1b;
using Hex1b.Layout;
using Hex1b.Scene.Core;
using Hex1b.Scene.Geometry;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;
using Hex1b.Surfaces;
using Hex1b.Widgets;
using SceneClass = Hex1b.Scene.Core.Scene;

[TestClass]
public class SceneNodeWidgetTextureTests
{
    private static SceneBufferGeometry CreateQuad()
    {
        var positions = new float[]
        {
            -2, -2, 0,
             2, -2, 0,
             2,  2, 0,
            -2,  2, 0
        };
        var geometry = new SceneBufferGeometry();
        geometry.SetAttribute("position", new SceneBufferAttribute("position", positions, 3));
        geometry.SetIndices(new uint[] { 0, 1, 2, 0, 2, 3 });
        return geometry;
    }

    private static (SceneWidget widget, SceneTextureMaterial material) BuildScene(int columns, int rows, Hex1bWidget source)
    {
        var scene = new SceneClass("Widget Texture Scene");
        var material = new SceneTextureMaterial(new Vector3(0.8f, 0.8f, 0.8f))
        {
            WidgetSource = source,
            WidgetSourceColumns = columns,
            WidgetSourceRows = rows
        };
        scene.AddChild(new SceneMesh(CreateQuad(), material, "Quad"));

        var camera = new ScenePerspectiveCamera("Camera")
        {
            Position = new Vector3(0, 0, 6)
        };

        return (new SceneWidget(scene, camera), material);
    }

    private static void RenderToSurface(SceneNode node)
    {
        node.Measure(new Constraints(0, 40, 0, 20));
        node.Arrange(new Rect(0, 0, 40, 20));
        var surface = new Surface(40, 20);
        var context = new SurfaceRenderContext(surface);
        node.Render(context);
    }

    [TestMethod]
    public async Task Render_WithWidgetSource_PopulatesMaterialTexture()
    {
        var (widget, material) = BuildScene(8, 4, new BorderWidget(new TextBlockWidget("Hi")));
        var context = ReconcileContext.CreateRoot();

        var node = (SceneNode)await widget.ReconcileAsync(null, context);
        RenderToSurface(node);

        Assert.IsNotNull(material.Texture);
        Assert.AreEqual(8 * 2, material.Texture!.Width);
        Assert.AreEqual(4 * 2, material.Texture.Height);
    }

    [TestMethod]
    public async Task Render_WidgetSourceContent_ProducesNonUniformTexture()
    {
        // A bordered widget draws box-drawing glyphs over a blank interior, so the sampled
        // texture must contain more than a single flat colour.
        var (widget, material) = BuildScene(10, 6, new BorderWidget(new TextBlockWidget("X")));
        var context = ReconcileContext.CreateRoot();

        var node = (SceneNode)await widget.ReconcileAsync(null, context);
        RenderToSurface(node);

        var texture = material.Texture!;
        var first = texture.GetPixel(0, 0);
        var sawDifferent = false;
        for (var y = 0; y < texture.Height && !sawDifferent; y++)
        {
            for (var x = 0; x < texture.Width; x++)
            {
                if (texture.GetPixel(x, y) != first)
                {
                    sawDifferent = true;
                    break;
                }
            }
        }

        Assert.IsTrue(sawDifferent, "Widget-sourced texture should not be a single flat colour.");
    }

    [TestMethod]
    public async Task Render_WithoutWidgetSource_LeavesTextureNull()
    {
        var scene = new SceneClass("Plain Scene");
        var material = new SceneTextureMaterial(new Vector3(0.5f, 0.5f, 0.5f));
        scene.AddChild(new SceneMesh(CreateQuad(), material, "Quad"));
        var camera = new ScenePerspectiveCamera("Camera") { Position = new Vector3(0, 0, 6) };
        var widget = new SceneWidget(scene, camera);
        var context = ReconcileContext.CreateRoot();

        var node = (SceneNode)await widget.ReconcileAsync(null, context);
        RenderToSurface(node);

        Assert.IsNull(material.Texture);
    }
}
