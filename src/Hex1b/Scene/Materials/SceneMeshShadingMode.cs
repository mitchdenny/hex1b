namespace Hex1b.Scene.Materials;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Scene.Math;

[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public enum SceneMeshShadingMode
{
    Lit,
    Normal,
    Depth
}
