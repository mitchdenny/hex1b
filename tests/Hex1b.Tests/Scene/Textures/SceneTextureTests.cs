namespace Hex1b.Tests.Scene.Textures;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hex1b.Scene.Textures;

[TestClass]
public class SceneTextureTests
{
    [TestMethod]
    public void SceneTexture2D_CreateAndSetPixels()
    {
        // Arrange
        var texture = new SceneTexture2D(64, 32);

        // Act
        texture.SetPixel(0, 0, 255, 0, 0, 255);
        texture.SetPixel(10, 10, 0, 255, 0, 255);

        // Assert
        var pixel00 = texture.GetPixel(0, 0);
        var pixel1010 = texture.GetPixel(10, 10);
        
        Assert.AreEqual(0xFF0000FFu, pixel00);
        Assert.AreEqual(0x00FF00FFu, pixel1010);
    }

    [TestMethod]
    public void SceneTexture2D_SampleBilinearClamp()
    {
        // Arrange
        var texture = new SceneTexture2D(4, 4);
        
        // Fill with white
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                texture.SetPixel(x, y, 255, 255, 255, 255);

        // Act - Sample outside bounds with clamp mode
        var sample = texture.SampleBilinear(1.5f, 0.5f, TextureWrapMode.Clamp);

        // Assert - Should clamp to valid range
        Assert.AreNotEqual(0u, sample);
    }

    [TestMethod]
    public void SceneTexture2D_SampleBilinearRepeat()
    {
        // Arrange
        var texture = new SceneTexture2D(2, 2);
        texture.SetPixel(0, 0, 255, 0, 0, 255);
        texture.SetPixel(1, 0, 0, 255, 0, 255);
        texture.SetPixel(0, 1, 0, 0, 255, 255);
        texture.SetPixel(1, 1, 255, 255, 0, 255);

        // Act - Sample with repeat mode outside [0,1] bounds
        var sample = texture.SampleBilinear(1.5f, 0.5f, TextureWrapMode.Repeat);

        // Assert - Should repeat the texture
        Assert.AreNotEqual(0u, sample);
    }

    [TestMethod]
    public void SceneTexture2D_SetPixelsFromBuffer()
    {
        // Arrange
        var texture = new SceneTexture2D(2, 2);
        var pixels = new uint[]
        {
            0xFF0000FFu, 0x00FF00FFu,
            0x0000FFFFu, 0xFFFF00FFu
        };

        // Act
        texture.SetPixels(pixels);

        // Assert
        Assert.AreEqual(0xFF0000FFu, texture.GetPixel(0, 0));
        Assert.AreEqual(0x00FF00FFu, texture.GetPixel(1, 0));
        Assert.AreEqual(0x0000FFFFu, texture.GetPixel(0, 1));
        Assert.AreEqual(0xFFFF00FFu, texture.GetPixel(1, 1));
    }
}
