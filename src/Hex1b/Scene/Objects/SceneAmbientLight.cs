namespace Hex1b.Scene.Objects;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Scene.Core;
using Hex1b.Scene.Math;

/// <summary>
/// Ambient light that illuminates all surfaces uniformly.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public class SceneAmbientLight : SceneLight
{
    public SceneAmbientLight() : base("AmbientLight")
    {
    }

    public SceneAmbientLight(string? name) : base(name)
    {
    }
}
