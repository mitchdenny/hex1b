namespace Hex1b.Scene.Rendering;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Scene.Math;

    /// <summary>
    /// Helper math functions for rendering.
    /// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
internal static class MathHelper
{
    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    public static int Clamp(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }
}
