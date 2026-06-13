namespace Hex1b.Tests.Scene;

using Hex1b.Layout;
using Hex1b.Scene.Core;
using Hex1b.Scene.Objects;
using Hex1b.Widgets;
using SceneClass = Hex1b.Scene.Core.Scene;

[TestClass]
public class SceneWidgetTests
{
    [TestMethod]
    public void Create_SceneWidget_WithValidSceneAndCamera()
    {
        // Arrange
        var scene = new SceneClass();
        var camera = new ScenePerspectiveCamera();

        // Act
        var widget = new SceneWidget(scene, camera);

        // Assert
        Assert.IsNotNull(widget);
        Assert.AreSame(scene, widget.Scene);
        Assert.AreSame(camera, widget.Camera);
    }

    [TestMethod]
    public void Create_SceneWidget_WithNullScene_ThrowsException()
    {
        // Arrange
        var camera = new ScenePerspectiveCamera();

        // Act & Assert
        try
        {
            new SceneWidget(null!, camera);
            Assert.Fail("Expected ArgumentNullException");
        }
        catch (ArgumentNullException)
        {
            // Expected
        }
    }

    [TestMethod]
    public void Create_SceneWidget_WithNullCamera_ThrowsException()
    {
        // Arrange
        var scene = new SceneClass();

        // Act & Assert
        try
        {
            new SceneWidget(scene, null!);
            Assert.Fail("Expected ArgumentNullException");
        }
        catch (ArgumentNullException)
        {
            // Expected
        }
    }

    [TestMethod]
    public void SceneNode_GetExpectedNodeType_ReturnsSceneNodeType()
    {
        // Arrange
        var scene = new SceneClass();
        var camera = new ScenePerspectiveCamera();
        var widget = new SceneWidget(scene, camera);

        // Act
        var type = widget.GetExpectedNodeType();

        // Assert
        Assert.AreEqual(typeof(SceneNode), type);
    }
}


